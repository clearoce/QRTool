using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace QRTool
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveQRCoder;

            if (args.Length > 0)
            {
                try
                {
                    return RunHeadless(args);
                }
                catch (Exception ex)
                {
                    WriteErrorLog(ex);
                    return 1;
                }
            }

            try
            {
                return RunGui();
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex);
                MessageBox.Show("QRTool 启动失败：\n" + ex.Message + "\n\n日志：" + LogPath(),
                    "QRTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static Assembly ResolveQRCoder(object sender, ResolveEventArgs args)
        {
            if (args.Name == null ||
                !args.Name.StartsWith("QRCoder", StringComparison.OrdinalIgnoreCase))
                return null;

            using (Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("QRCoder.dll"))
            {
                if (stream == null) return null;
                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return Assembly.Load(memory.ToArray());
                }
            }
        }

        private static int RunGui()
        {
            try { SetProcessDPIAware(); }
            catch (Exception) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppConfig cfg = AppConfig.Load();
            Application.Run(new MainForm(cfg));
            return 0;
        }

        private static int RunHeadless(string[] args)
        {
            string command = args[0].ToLowerInvariant();
            AppConfig cfg = new AppConfig();
            cfg.Normalize();

            if (command == "export")
            {
                if (args.Length < 3)
                    throw new ArgumentException("用法：QRTool.exe export <text> <out.prn>");
                BuiltLabel built = Tspl.Build(args[1], cfg);
                EnsureParentDirectory(args[2]);
                File.WriteAllBytes(args[2], built.Data);
                return 0;
            }

            if (command == "preview")
            {
                if (args.Length < 3)
                    throw new ArgumentException("用法：QRTool.exe preview <text> <out.png>");
                BuiltLabel built = Tspl.Build(args[1], cfg);
                EnsureParentDirectory(args[2]);
                using (Bitmap image = QrPreview.Render(built, cfg))
                {
                    image.Save(args[2], ImageFormat.Png);
                }
                return 0;
            }

            if (command == "print")
            {
                if (args.Length < 3)
                    throw new ArgumentException(
                        "用法：QRTool.exe print <text> <printer> [out.pdf]");
                BuiltLabel built = Tspl.Build(args[1], cfg);
                if (PrinterService.IsVirtualPrinter(args[2]))
                {
                    string outputPath = args.Length > 3 ? args[3] : null;
                    using (Bitmap image = QrPreview.Render(built, cfg))
                    {
                        PrinterService.PrintRendered(
                            args[2], image, built.Layout, outputPath);
                    }
                }
                else
                {
                    PrinterService.SendRaw(args[2], built.Data, "QRTool QR Label");
                }
                return 0;
            }

            throw new ArgumentException("未知命令：" + args[0]);
        }

        private static void EnsureParentDirectory(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

        private static string LogPath()
        {
            return Path.Combine(AppConfig.DataDir(), "qrtool.log");
        }

        private static void WriteErrorLog(Exception ex)
        {
            try
            {
                string path = LogPath();
                string dir = Path.GetDirectoryName(path);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    Environment.NewLine + ex.ToString(), new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}
