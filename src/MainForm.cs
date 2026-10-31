using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace QRTool
{
    public class MainForm : Form
    {
        private AppConfig _cfg;
        private List<string> _printers = new List<string>();
        private bool _busy;
        private bool _loading;
        private string _pendingAction;
        private BackgroundWorker _worker;
        private Timer _previewTimer;

        private TextBox txtText;
        private Label lblCounter;
        private ComboBox cboPrinter;
        private Label lblPrinterStatus;
        private Button btnRefresh;
        private Button btnTest;
        private NumericUpDown numWidth;
        private NumericUpDown numHeight;
        private NumericUpDown numGap;
        private NumericUpDown numCopies;
        private ComboBox cboDpi;
        private ComboBox cboEcc;
        private ComboBox cboCell;
        private ComboBox cboEncoding;
        private CheckBox chkModel2;
        private CheckBox chkKeepNewlines;
        private CheckBox chkCompat;
        private PictureBox picPreview;
        private Label lblQrSize;
        private Button btnPreview;
        private Button btnPrint;
        private Button btnExport;
        private Button btnReset;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;

        private class PrintCompletion
        {
            public int JobId;
            public string Printer;
            public Exception Error;
        }

        public MainForm(AppConfig cfg)
        {
            _cfg = cfg;
            Text = "QRTool · 二维码标签打印";
            Size = new Size(1040, 720);
            MinimumSize = new Size(940, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9.5F);
            AutoScaleMode = AutoScaleMode.Dpi;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            BuildUi();
            ApplyConfig(cfg);
            LoadPrinters();
            UpdateCounter();

            _previewTimer = new Timer();
            _previewTimer.Interval = 350;
            _previewTimer.Tick += delegate
            {
                _previewTimer.Stop();
                UpdatePreview(false);
            };
            _previewTimer.Start();

            KeyPreview = true;
            KeyDown += OnFormKeyDown;
            FormClosing += OnFormClosing;
            SetStatus("就绪");
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            TableLayoutPanel main = new TableLayoutPanel();
            main.Dock = DockStyle.Fill;
            main.Padding = new Padding(16, 12, 16, 8);
            main.ColumnCount = 2;
            main.RowCount = 2;
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            FlowLayoutPanel header = new FlowLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.FlowDirection = FlowDirection.LeftToRight;
            header.WrapContents = false;
            header.AutoSize = true;
            Label title = new Label();
            title.Text = "QRTool";
            title.AutoSize = true;
            title.Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(16, 42, 67);
            title.Margin = new Padding(0, 0, 12, 0);
            Label subtitle = new Label();
            subtitle.Text = "文本 → TSPL 二维码标签";
            subtitle.AutoSize = true;
            subtitle.ForeColor = Color.FromArgb(98, 125, 152);
            subtitle.Margin = new Padding(0, 12, 0, 0);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            main.Controls.Add(header, 0, 0);
            main.SetColumnSpan(header, 2);

            GroupBox textGroup = new GroupBox();
            textGroup.Text = "文本内容";
            textGroup.Dock = DockStyle.Fill;
            textGroup.Padding = new Padding(10, 8, 10, 10);
            textGroup.Margin = new Padding(0, 8, 8, 0);
            TableLayoutPanel textLayout = new TableLayoutPanel();
            textLayout.Dock = DockStyle.Fill;
            textLayout.ColumnCount = 1;
            textLayout.RowCount = 3;
            textLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            textLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtText = new TextBox();
            txtText.Multiline = true;
            txtText.AcceptsReturn = true;
            txtText.ScrollBars = ScrollBars.Vertical;
            txtText.Dock = DockStyle.Fill;
            txtText.Font = new Font("Microsoft YaHei UI", 10.5F);
            txtText.TextChanged += delegate { OnSettingsChanged(); };

            lblCounter = new Label();
            lblCounter.AutoSize = true;
            lblCounter.ForeColor = Color.FromArgb(98, 125, 152);
            lblCounter.Margin = new Padding(2, 6, 0, 6);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.AutoSize = true;
            actions.WrapContents = false;
            actions.Margin = new Padding(0, 2, 0, 0);
            btnPreview = NewButton("生成预览");
            btnPrint = NewButton("打印");
            btnPrint.Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);
            btnExport = NewButton("导出 TSPL");
            btnReset = NewButton("恢复默认");
            btnPreview.Click += delegate { UpdatePreview(true); };
            btnPrint.Click += delegate { OnPrint(); };
            btnExport.Click += delegate { OnExport(); };
            btnReset.Click += delegate { OnReset(); };
            actions.Controls.Add(btnPreview);
            actions.Controls.Add(btnPrint);
            actions.Controls.Add(btnExport);
            actions.Controls.Add(btnReset);

            textLayout.Controls.Add(txtText, 0, 0);
            textLayout.Controls.Add(lblCounter, 0, 1);
            textLayout.Controls.Add(actions, 0, 2);
            textGroup.Controls.Add(textLayout);
            main.Controls.Add(textGroup, 0, 1);

            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.ColumnCount = 1;
            right.RowCount = 3;
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 135F));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 275F));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            right.Margin = new Padding(8, 8, 0, 0);

            GroupBox printerGroup = new GroupBox();
            printerGroup.Text = "打印机";
            printerGroup.Dock = DockStyle.Fill;
            printerGroup.AutoSize = false;
            printerGroup.Padding = new Padding(10, 8, 10, 10);
            printerGroup.Margin = new Padding(0, 0, 0, 8);
            TableLayoutPanel printerLayout = new TableLayoutPanel();
            printerLayout.Dock = DockStyle.Fill;
            printerLayout.AutoSize = false;
            printerLayout.ColumnCount = 1;
            printerLayout.RowCount = 3;
            printerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            printerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            printerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cboPrinter = new ComboBox();
            cboPrinter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboPrinter.Dock = DockStyle.Fill;
            cboPrinter.Margin = new Padding(0, 3, 0, 3);
            FlowLayoutPanel printerButtons = new FlowLayoutPanel();
            printerButtons.Dock = DockStyle.Fill;
            printerButtons.AutoSize = true;
            printerButtons.WrapContents = false;
            btnRefresh = NewButton("刷新");
            btnTest = NewButton("测试");
            btnRefresh.Margin = new Padding(0, 1, 6, 1);
            btnTest.Margin = new Padding(0, 1, 0, 1);
            btnRefresh.Click += delegate { LoadPrinters(); };
            btnTest.Click += delegate { OnTestPrint(); };
            printerButtons.Controls.Add(btnRefresh);
            printerButtons.Controls.Add(btnTest);
            lblPrinterStatus = new Label();
            lblPrinterStatus.AutoSize = true;
            lblPrinterStatus.ForeColor = Color.FromArgb(98, 125, 152);
            lblPrinterStatus.Margin = new Padding(2, 4, 0, 0);
            printerLayout.Controls.Add(cboPrinter, 0, 0);
            printerLayout.Controls.Add(printerButtons, 0, 1);
            printerLayout.Controls.Add(lblPrinterStatus, 0, 2);
            printerGroup.Controls.Add(printerLayout);
            right.Controls.Add(printerGroup, 0, 0);

            GroupBox settingsGroup = new GroupBox();
            settingsGroup.Text = "标签与二维码";
            settingsGroup.Dock = DockStyle.Fill;
            settingsGroup.AutoSize = false;
            settingsGroup.Padding = new Padding(10, 8, 10, 10);
            settingsGroup.Margin = new Padding(0, 0, 0, 8);
            TableLayoutPanel settings = new TableLayoutPanel();
            settings.Dock = DockStyle.Fill;
            settings.AutoSize = false;
            settings.ColumnCount = 4;
            settings.RowCount = 6;
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (int i = 0; i < 6; i++) settings.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            numWidth = NewNumeric(20M, 98M, 1, 1M, 40M);
            numHeight = NewNumeric(5M, 300M, 1, 1M, 30M);
            numGap = NewNumeric(0M, 25.4M, 1, 0.5M, 2M);
            numCopies = NewNumeric(1M, 999M, 0, 1M, 1M);
            cboDpi = NewCombo(new string[] { "203 DPI", "300 DPI", "600 DPI" });
            cboDpi.SelectedIndex = 1;
            cboEcc = NewCombo(new string[] { "L", "M", "Q", "H" });
            cboEcc.SelectedIndex = 1;
            cboCell = NewCombo(new string[] { "自动", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" });
            cboCell.SelectedIndex = 0;
            cboEncoding = NewCombo(new string[] { "UTF-8", "GB18030" });
            cboEncoding.SelectedIndex = 0;
            chkModel2 = NewCheck("Model 2 / S7", true);
            chkKeepNewlines = NewCheck("保留换行", true);
            chkCompat = NewCheck("兼容模式（省略 Model/Mask）", false);

            AddSetting(settings, "宽 (mm)", numWidth, 0, 0);
            AddSetting(settings, "高 (mm)", numHeight, 2, 0);
            AddSetting(settings, "间距 (mm)", numGap, 0, 1);
            AddSetting(settings, "分辨率", cboDpi, 2, 1);
            AddSetting(settings, "份数", numCopies, 0, 2);
            AddSetting(settings, "ECC", cboEcc, 2, 2);
            AddSetting(settings, "单元宽度", cboCell, 0, 3);
            AddSetting(settings, "编码", cboEncoding, 2, 3);
            settings.Controls.Add(chkKeepNewlines, 0, 4);
            settings.SetColumnSpan(chkKeepNewlines, 2);
            settings.Controls.Add(chkModel2, 2, 4);
            settings.SetColumnSpan(chkModel2, 2);
            settings.Controls.Add(chkCompat, 0, 5);
            settings.SetColumnSpan(chkCompat, 4);
            settingsGroup.Controls.Add(settings);
            right.Controls.Add(settingsGroup, 0, 1);

            GroupBox previewGroup = new GroupBox();
            previewGroup.Text = "标签预览";
            previewGroup.Dock = DockStyle.Fill;
            previewGroup.Padding = new Padding(10, 8, 10, 10);
            previewGroup.Margin = new Padding(0);
            TableLayoutPanel previewLayout = new TableLayoutPanel();
            previewLayout.Dock = DockStyle.Fill;
            previewLayout.ColumnCount = 1;
            previewLayout.RowCount = 2;
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            picPreview = new PictureBox();
            picPreview.Dock = DockStyle.Fill;
            picPreview.SizeMode = PictureBoxSizeMode.Zoom;
            picPreview.BackColor = Color.FromArgb(236, 236, 236);
            picPreview.BorderStyle = BorderStyle.FixedSingle;
            picPreview.Margin = new Padding(0);
            lblQrSize = new Label();
            lblQrSize.AutoSize = true;
            lblQrSize.ForeColor = Color.FromArgb(98, 125, 152);
            lblQrSize.Margin = new Padding(2, 6, 0, 0);
            previewLayout.Controls.Add(picPreview, 0, 0);
            previewLayout.Controls.Add(lblQrSize, 0, 1);
            previewGroup.Controls.Add(previewLayout);
            right.Controls.Add(previewGroup, 0, 2);

            main.Controls.Add(right, 1, 1);

            statusStrip = new StatusStrip();
            statusStrip.SizingGrip = false;
            statusStrip.Dock = DockStyle.Fill;
            lblStatus = new ToolStripStatusLabel("就绪");
            statusStrip.Items.Add(lblStatus);

            root.Controls.Add(main, 0, 0);
            root.Controls.Add(statusStrip, 0, 1);
            Controls.Add(root);

            cboDpi.SelectedIndexChanged += delegate { OnSettingsChanged(); };
            cboEcc.SelectedIndexChanged += delegate { OnSettingsChanged(); };
            cboCell.SelectedIndexChanged += delegate { OnSettingsChanged(); };
            cboEncoding.SelectedIndexChanged += delegate { OnSettingsChanged(); };
            numWidth.ValueChanged += delegate { OnSettingsChanged(); };
            numHeight.ValueChanged += delegate { OnSettingsChanged(); };
            numGap.ValueChanged += delegate { OnSettingsChanged(); };
            numCopies.ValueChanged += delegate { OnSettingsChanged(); };
            chkModel2.CheckedChanged += delegate { OnSettingsChanged(); };
            chkKeepNewlines.CheckedChanged += delegate { OnSettingsChanged(); };
            chkCompat.CheckedChanged += delegate
            {
                chkModel2.Enabled = !chkCompat.Checked;
                OnSettingsChanged();
            };
        }

        private Button NewButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.MinimumSize = new Size(72, 30);
            button.Margin = new Padding(0, 0, 8, 0);
            return button;
        }

        private NumericUpDown NewNumeric(decimal min, decimal max, int decimals, decimal increment, decimal value)
        {
            NumericUpDown control = new NumericUpDown();
            control.Minimum = min;
            control.Maximum = max;
            control.DecimalPlaces = decimals;
            control.Increment = increment;
            control.Value = value;
            control.Dock = DockStyle.Fill;
            control.TextAlign = HorizontalAlignment.Right;
            control.Margin = new Padding(2, 3, 8, 3);
            return control;
        }

        private ComboBox NewCombo(string[] items)
        {
            ComboBox control = new ComboBox();
            control.DropDownStyle = ComboBoxStyle.DropDownList;
            control.Dock = DockStyle.Fill;
            control.Items.AddRange(items);
            control.Margin = new Padding(2, 3, 8, 3);
            return control;
        }

        private CheckBox NewCheck(string text, bool value)
        {
            CheckBox control = new CheckBox();
            control.Text = text;
            control.Checked = value;
            control.AutoSize = true;
            control.Margin = new Padding(4, 6, 8, 3);
            return control;
        }

        private void AddSetting(TableLayoutPanel grid, string labelText, Control control, int labelColumn, int row)
        {
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(4, 6, 2, 3);
            grid.Controls.Add(label, labelColumn, row);
            grid.Controls.Add(control, labelColumn + 1, row);
        }

        private void ApplyConfig(AppConfig cfg)
        {
            _loading = true;
            try
            {
                numWidth.Value = (decimal)cfg.label_width_mm;
                numHeight.Value = (decimal)cfg.label_height_mm;
                numGap.Value = (decimal)cfg.gap_mm;
                numCopies.Value = (decimal)cfg.copies;

                int dpiIndex = Array.IndexOf(Tspl.DpiChoices, cfg.dpi);
                cboDpi.SelectedIndex = dpiIndex < 0 ? 1 : dpiIndex;
                cboEcc.SelectedItem = cfg.ecc;
                if (cboEcc.SelectedIndex < 0) cboEcc.SelectedIndex = 1;
                cboCell.SelectedIndex = cfg.cell_width >= 0 && cfg.cell_width <= 10 ? cfg.cell_width : 0;
                cboEncoding.SelectedIndex = cfg.encoding == "gb18030" ? 1 : 0;

                chkModel2.Checked = cfg.use_model2;
                chkKeepNewlines.Checked = cfg.keep_newlines;
                chkCompat.Checked = cfg.compat_mode;
                chkModel2.Enabled = !chkCompat.Checked;
            }
            finally
            {
                _loading = false;
            }
        }

        private void LoadPrinters()
        {
            try
            {
                _printers = PrinterService.ListPrinters();
            }
            catch (Exception)
            {
                _printers = new List<string>();
            }

            cboPrinter.Items.Clear();
            if (_printers.Count > 0)
            {
                cboPrinter.Items.AddRange(_printers.ToArray());
                int index = _printers.IndexOf(_cfg.printer_name);
                if (index < 0)
                {
                    string defaultPrinter = PrinterService.DefaultPrinter();
                    index = _printers.IndexOf(defaultPrinter);
                }
                if (index < 0) index = 0;
                cboPrinter.SelectedIndex = index;
                cboPrinter.Enabled = !_busy;
                btnPrint.Enabled = !_busy;
                btnTest.Enabled = !_busy;
                lblPrinterStatus.Text = string.Format("已找到 {0} 台打印机", _printers.Count);
                lblPrinterStatus.ForeColor = Color.FromArgb(98, 125, 152);
            }
            else
            {
                cboPrinter.Items.Add("未找到打印机（仍可导出 TSPL）");
                cboPrinter.SelectedIndex = 0;
                cboPrinter.Enabled = false;
                btnPrint.Enabled = false;
                btnTest.Enabled = false;
                lblPrinterStatus.Text = "未找到打印机；仍可生成预览和导出 TSPL";
                lblPrinterStatus.ForeColor = Color.FromArgb(179, 107, 0);
            }
        }

        private string SelectedPrinter()
        {
            string name = cboPrinter.SelectedItem as string;
            if (name == null) return "";
            return _printers.Contains(name) ? name : "";
        }

        private AppConfig ReadConfigFromUi()
        {
            AppConfig cfg = new AppConfig();
            cfg.printer_name = SelectedPrinter();
            if (cfg.printer_name.Length == 0) cfg.printer_name = _cfg.printer_name;
            cfg.label_width_mm = (double)numWidth.Value;
            cfg.label_height_mm = (double)numHeight.Value;
            cfg.gap_mm = (double)numGap.Value;
            cfg.dpi = Tspl.DpiChoices[cboDpi.SelectedIndex < 0 ? 1 : cboDpi.SelectedIndex];
            cfg.ecc = cboEcc.SelectedItem == null ? "M" : cboEcc.SelectedItem.ToString();
            cfg.cell_width = cboCell.SelectedIndex < 0 ? 0 : cboCell.SelectedIndex;
            cfg.copies = (int)numCopies.Value;
            cfg.encoding = cboEncoding.SelectedIndex == 1 ? "gb18030" : "utf-8";
            cfg.use_model2 = chkModel2.Checked;
            cfg.keep_newlines = chkKeepNewlines.Checked;
            cfg.compat_mode = chkCompat.Checked;
            cfg.last_export_dir = _cfg.last_export_dir;
            cfg.Normalize();
            return cfg;
        }

        private string RequireText()
        {
            string text = txtText.Text;
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("请输入要生成二维码的文本");
            return text;
        }

        private void OnSettingsChanged()
        {
            if (_loading) return;
            UpdateCounter();
            SchedulePreview();
        }

        private void SchedulePreview()
        {
            if (_busy) return;
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        private void UpdateCounter()
        {
            string text = txtText.Text;
            string label = cboEncoding.SelectedIndex == 1 ? "GB18030" : "UTF-8";
            int max = Tspl.MaxCapacity(cboEcc.SelectedItem == null ? "M" : cboEcc.SelectedItem.ToString());
            try
            {
                Encoding encoding = Tspl.GetEncoding(cboEncoding.SelectedIndex == 1 ? "gb18030" : "utf-8");
                int bytes = encoding.GetByteCount(text);
                lblCounter.Text = string.Format("字符 {0} · {1} {2} 字节 / 上限约 {3} 字节",
                    text.Length, label, bytes, max);
                lblCounter.ForeColor = bytes > max
                    ? Color.FromArgb(176, 0, 32)
                    : Color.FromArgb(98, 125, 152);
            }
            catch (Exception)
            {
                lblCounter.Text = string.Format("字符 {0} · 当前编码无法表示部分字符", text.Length);
                lblCounter.ForeColor = Color.FromArgb(176, 0, 32);
            }
        }

        private void UpdatePreview(bool showErrors)
        {
            if (_busy) return;
            string text = txtText.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                ClearPreview();
                if (showErrors)
                    MessageBox.Show(this, "请输入要生成二维码的文本", "无法生成预览",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                    SetStatus("就绪");
                return;
            }

            try
            {
                AppConfig cfg = ReadConfigFromUi();
                BuiltLabel built = Tspl.Build(text, cfg);
                Bitmap image = QrPreview.Render(built, cfg);
                if (picPreview.Image != null) picPreview.Image.Dispose();
                picPreview.Image = image;
                _cfg = cfg;

                LabelLayout layout = built.Layout;
                lblQrSize.Text = string.Format(
                    "二维码约 {0:F1} × {0:F1} mm · {1}×{1} 模块 · {2} dots/模块",
                    layout.SymbolMm, layout.Modules, layout.CellWidth);
                if (built.Warnings.Count > 0)
                    SetStatus("预览已生成；" + string.Join("；", built.Warnings.ToArray()));
                else
                    SetStatus("预览已生成");
            }
            catch (Exception ex)
            {
                ClearPreview();
                SetStatus("预览失败：" + ex.Message);
                if (showErrors)
                    MessageBox.Show(this, ex.Message, "无法生成预览",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearPreview()
        {
            if (picPreview.Image != null)
            {
                picPreview.Image.Dispose();
                picPreview.Image = null;
            }
            lblQrSize.Text = "";
        }

        private bool ConfirmWarnings(BuiltLabel built, string action)
        {
            if (built.Warnings.Count == 0) return true;
            string message = string.Join("；", built.Warnings.ToArray()) + "\n\n仍要继续？";
            return MessageBox.Show(this, message, action + "前提示",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private void OnPrint()
        {
            if (_busy) return;
            if (_printers.Count == 0)
            {
                MessageBox.Show(this, "未找到可用打印机；仍可导出 TSPL 文件", "无法打印",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string printer = SelectedPrinter();
            if (printer.Length == 0)
            {
                MessageBox.Show(this, "请先选择打印机", "无法打印",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                AppConfig cfg = ReadConfigFromUi();
                cfg.printer_name = printer;
                BuiltLabel built = Tspl.Build(RequireText(), cfg);
                if (!ConfirmWarnings(built, "打印")) return;
                _cfg = cfg;
                Submit(printer, built, "print");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "无法打印",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnTestPrint()
        {
            if (_busy) return;
            if (_printers.Count == 0)
            {
                MessageBox.Show(this, "未找到可用打印机", "无法测试打印",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string printer = SelectedPrinter();
            if (printer.Length == 0)
            {
                MessageBox.Show(this, "请先选择打印机", "无法测试打印",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                AppConfig cfg = ReadConfigFromUi();
                cfg.printer_name = printer;
                cfg.copies = 1;
                BuiltLabel built = Tspl.Build("QRTool TEST", cfg);
                if (!ConfirmWarnings(built, "测试打印")) return;
                if (MessageBox.Show(this,
                        string.Format("将向“{0}”发送一张测试标签。\n\n继续？", printer),
                        "测试打印", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                _cfg = cfg;
                Submit(printer, built, "test");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "无法测试打印",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnExport()
        {
            try
            {
                AppConfig cfg = ReadConfigFromUi();
                BuiltLabel built = Tspl.Build(RequireText(), cfg);
                if (!ConfirmWarnings(built, "导出")) return;

                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Title = "导出 TSPL 原始文件";
                dialog.Filter = "TSPL 原始文件 (*.prn)|*.prn|所有文件 (*.*)|*.*";
                dialog.DefaultExt = "prn";
                dialog.AddExtension = true;
                dialog.FileName = "QR_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".prn";
                if (!string.IsNullOrEmpty(cfg.last_export_dir))
                    dialog.InitialDirectory = cfg.last_export_dir;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                File.WriteAllBytes(dialog.FileName, built.Data);
                string dir = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(dir)) cfg.last_export_dir = dir;
                _cfg = cfg;
                PersistConfig();
                SetStatus(string.Format("已导出 {0}（{1} 字节）", dialog.FileName, built.Data.Length));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "无法导出",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnReset()
        {
            if (_busy) return;
            if (MessageBox.Show(this, "将标签、二维码和打印设置恢复为默认值（不会清空文本）。\n\n继续？",
                    "恢复默认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            AppConfig defaults = new AppConfig();
            defaults.printer_name = _cfg.printer_name;
            defaults.last_export_dir = _cfg.last_export_dir;
            ApplyConfig(defaults);
            _cfg = defaults;
            UpdateCounter();
            _previewTimer.Stop();
            UpdatePreview(false);
            PersistConfig();
            SetStatus("已恢复默认设置");
        }

        private void Submit(string printer, BuiltLabel built, string action)
        {
            _busy = true;
            _pendingAction = action;
            _previewTimer.Stop();
            SetBusy(true);
            SetStatus(string.Format("正在提交到打印队列“{0}”…", printer));

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                object[] args = (object[])e.Argument;
                string target = (string)args[0];
                byte[] data = (byte[])args[1];
                PrintCompletion result = new PrintCompletion();
                result.Printer = target;
                try
                {
                    result.JobId = PrinterService.SendRaw(target, data, "QRTool QR Label");
                }
                catch (Exception ex)
                {
                    result.Error = ex;
                }
                e.Result = result;
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                _busy = false;
                SetBusy(false);
                PrintCompletion result = e.Result as PrintCompletion;
                if (result == null)
                {
                    SetStatus("打印失败");
                    MessageBox.Show(this, "打印任务未返回结果", "打印失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (result.Error != null)
                {
                    SetStatus("打印失败");
                    MessageBox.Show(this, "无法提交打印作业：\n" + result.Error.Message,
                        "打印失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    string message = string.Format("已提交到队列“{0}”，作业 #{1}",
                        result.Printer, result.JobId);
                    SetStatus(message);
                    PersistConfig();
                    if (_pendingAction == "test")
                        MessageBox.Show(this, message, "测试打印",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                worker.Dispose();
                _worker = null;
            };
            _worker = worker;
            worker.RunWorkerAsync(new object[] { printer, built.Data });
        }

        private void SetBusy(bool busy)
        {
            bool hasPrinters = _printers.Count > 0;
            btnPreview.Enabled = !busy;
            btnPrint.Enabled = !busy && hasPrinters;
            btnExport.Enabled = !busy;
            btnRefresh.Enabled = !busy;
            btnTest.Enabled = !busy && hasPrinters;
            btnReset.Enabled = !busy;
            cboPrinter.Enabled = !busy && hasPrinters;
        }

        private void PersistConfig()
        {
            try
            {
                AppConfig cfg = ReadConfigFromUi();
                _cfg = cfg;
                cfg.Save();
            }
            catch (Exception) { }
        }

        private void SetStatus(string text)
        {
            lblStatus.Text = text;
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.P)
            {
                e.SuppressKeyPress = true;
                OnPrint();
            }
            else if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                UpdatePreview(true);
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_busy)
            {
                MessageBox.Show(this, "打印任务仍在提交，请稍后再关闭。", "退出",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            PersistConfig();
        }
    }
}
