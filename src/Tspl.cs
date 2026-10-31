using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace QRTool
{
    public class LabelLayout
    {
        public int Dpi;
        public int WidthDots;
        public int HeightDots;
        public int PrintableWidthDots;
        public int PrintableHeightDots;
        public int Version;
        public int Modules;
        public int CellWidth;
        public int QuietZoneModules;
        public int SpanDots;
        public int SpanX;
        public int SpanY;
        public int SymbolX;
        public int SymbolY;

        public double SymbolMm
        {
            get { return Modules * CellWidth * 25.4 / Dpi; }
        }
    }

    public class BuiltLabel
    {
        public byte[] Data;
        public LabelLayout Layout;
        public byte[] PayloadBytes;
        public string PayloadText;
        public List<string> Warnings;
    }

    public static class Tspl
    {
        public const double MaxPrintWidthMm = 80.0;
        public const double SafetyMarginMm = 2.0;
        public const int QuietZoneModules = 4;
        public const int MaxCellWidth = 10;
        public const int MinRecommendedCell = 4;
        public const int MaxCopies = 999;
        public const int LongPayloadWarningBytes = 1500;

        public static readonly int[] DpiChoices = new int[] { 203, 300, 600 };

        // Maximum byte-mode payload for each ECC level and QR version (1..40).
        private static readonly int[,] ByteCapacity = new int[4, 41]
        {
            { 0, 17, 32, 53, 78, 106, 134, 154, 192, 230, 271, 321, 367, 425, 458, 520, 586, 644, 718, 792, 858, 929, 1003, 1091, 1171, 1273, 1367, 1465, 1528, 1628, 1732, 1840, 1952, 2068, 2188, 2303, 2431, 2563, 2699, 2809, 2953 },
            { 0, 14, 26, 42, 62, 84, 106, 122, 152, 180, 213, 251, 287, 331, 362, 412, 450, 504, 560, 624, 666, 711, 779, 857, 911, 997, 1059, 1125, 1190, 1264, 1370, 1452, 1538, 1628, 1722, 1809, 1911, 1989, 2099, 2213, 2331 },
            { 0, 11, 20, 32, 46, 60, 74, 86, 108, 130, 151, 177, 203, 241, 258, 292, 322, 364, 394, 442, 482, 509, 565, 611, 661, 715, 751, 805, 868, 908, 982, 1030, 1112, 1168, 1228, 1283, 1351, 1423, 1499, 1579, 1663 },
            { 0, 7, 14, 24, 34, 44, 58, 64, 84, 98, 119, 137, 155, 177, 194, 220, 250, 280, 310, 338, 382, 403, 439, 461, 511, 535, 593, 625, 658, 698, 742, 790, 842, 898, 958, 983, 1051, 1093, 1139, 1219, 1273 }
        };

        public static int MmToDots(double mm, int dpi)
        {
            if (dpi <= 0) throw new ArgumentException("DPI 必须大于 0");
            return (int)(mm * dpi / 25.4);
        }

        private static double DotsToMm(double dots, int dpi)
        {
            return dots * 25.4 / dpi;
        }

        public static string FormatMm(double value)
        {
            if (value == 0) return "0";
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string NormalizeContent(string text, bool keepNewlines)
        {
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            if (keepNewlines) return normalized.Replace("\n", "\r\n");
            return normalized.Replace("\n", " ");
        }

        private static string EscapeTsplData(string text)
        {
            return text.Replace("\"", "\\\"");
        }

        private static string PreparePayload(string text, bool keepNewlines, out bool padded)
        {
            string payload = NormalizeContent(text, keepNewlines);
            padded = payload.EndsWith("\\", StringComparison.Ordinal);
            if (padded) payload += " ";
            return payload;
        }

        public static Encoding GetEncoding(string name)
        {
            if (name == "gb18030") return Encoding.GetEncoding(54936);
            return new UTF8Encoding(false);
        }

        private static int EccIndex(string ecc)
        {
            switch (ecc)
            {
                case "L": return 0;
                case "M": return 1;
                case "Q": return 2;
                case "H": return 3;
                default: throw new ArgumentException("不支持的纠错级别：" + ecc);
            }
        }

        public static int MaxCapacity(string ecc)
        {
            return ByteCapacity[EccIndex(ecc), 40];
        }

        public static int ChooseVersion(byte[] payload, string ecc)
        {
            int idx = EccIndex(ecc);
            for (int version = 1; version <= 40; version++)
            {
                if (payload.Length <= ByteCapacity[idx, version]) return version;
            }
            throw new ArgumentException(string.Format(
                "内容超出二维码容量：{0} 字节，ECC {1} 最多约 {2} 字节。请缩短内容或降低纠错级别。",
                payload.Length, ecc, MaxCapacity(ecc)));
        }

        private static int ChooseCell(int spanUnits, int printableWidthDots, int printableHeightDots, int marginDots)
        {
            int maxByWidth = (printableWidthDots - 2 * marginDots) / spanUnits;
            int maxByHeight = (printableHeightDots - 2 * marginDots) / spanUnits;
            int cell = Math.Min(MaxCellWidth, Math.Min(maxByWidth, maxByHeight));
            if (cell < 1)
            {
                throw new ArgumentException("内容过长或标签太小，二维码放不下。请增大标签或缩短内容。");
            }
            return cell;
        }

        public static BuiltLabel Build(string text, AppConfig cfg)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("请输入要生成二维码的文本");
            cfg.Normalize();

            List<string> warnings = new List<string>();
            bool padded;
            string payload = PreparePayload(text, cfg.keep_newlines, out padded);
            if (padded) warnings.Add("内容以反斜杠结尾，已自动补一个空格以符合 TSPL 语法");

            Encoding encoding = GetEncoding(cfg.encoding);
            byte[] payloadBytes;
            try
            {
                payloadBytes = encoding.GetBytes(payload);
            }
            catch (EncoderFallbackException ex)
            {
                throw new ArgumentException("当前编码无法表示输入内容，请改用 UTF-8。", ex);
            }

            if (payloadBytes.Length > LongPayloadWarningBytes)
            {
                warnings.Add(string.Format(
                    "内容较长（{0} 字节），部分固件可能限制单条 TSPL 指令长度",
                    payloadBytes.Length));
            }

            int version = ChooseVersion(payloadBytes, cfg.ecc);
            int modules = 17 + 4 * version;
            int widthDots = MmToDots(cfg.label_width_mm, cfg.dpi);
            int heightDots = MmToDots(cfg.label_height_mm, cfg.dpi);
            int printableWidthDots = MmToDots(Math.Min(cfg.label_width_mm, MaxPrintWidthMm), cfg.dpi);
            int printableHeightDots = heightDots;
            int marginDots = MmToDots(SafetyMarginMm, cfg.dpi);
            int spanUnits = modules + 2 * QuietZoneModules;

            int cell;
            if (cfg.cell_width == 0)
            {
                cell = ChooseCell(spanUnits, printableWidthDots, printableHeightDots, marginDots);
            }
            else
            {
                cell = cfg.cell_width;
                int span = spanUnits * cell;
                if (span > printableWidthDots - 2 * marginDots ||
                    span > printableHeightDots - 2 * marginDots)
                {
                    throw new ArgumentException(string.Format(
                        "二维码放不下：cell width {0} 时约 {1} dots，标签可用区域 {2}×{3} dots。",
                        cell, span, printableWidthDots, printableHeightDots));
                }
            }

            int spanDots = spanUnits * cell;
            int spanX = (printableWidthDots - spanDots) / 2;
            int spanY = (printableHeightDots - spanDots) / 2;
            if (spanX < 0 || spanY < 0)
            {
                throw new ArgumentException("二维码放不下：请增大标签尺寸或缩短内容。");
            }

            int symbolX = spanX + QuietZoneModules * cell;
            int symbolY = spanY + QuietZoneModules * cell;

            if (cfg.label_width_mm > MaxPrintWidthMm)
            {
                warnings.Add(string.Format(
                    "标签宽度超过 {0:g} mm，二维码将居中于可打印区域", MaxPrintWidthMm));
            }
            if (cell < MinRecommendedCell)
            {
                warnings.Add(string.Format(
                    "二维码模块仅 {0} dots（约 {1:F2} mm），可能不易扫描",
                    cell, DotsToMm(cell, cfg.dpi)));
            }
            if (cfg.compat_mode)
            {
                warnings.Add("兼容模式未指定 Model 2/S7；若手机无法识别二维码，可尝试关闭兼容模式");
            }
            else if (!cfg.use_model2)
            {
                warnings.Add("未使用 Model 2，部分手机可能无法识别二维码");
            }

            LabelLayout layout = new LabelLayout();
            layout.Dpi = cfg.dpi;
            layout.WidthDots = widthDots;
            layout.HeightDots = heightDots;
            layout.PrintableWidthDots = printableWidthDots;
            layout.PrintableHeightDots = printableHeightDots;
            layout.Version = version;
            layout.Modules = modules;
            layout.CellWidth = cell;
            layout.QuietZoneModules = QuietZoneModules;
            layout.SpanDots = spanDots;
            layout.SpanX = spanX;
            layout.SpanY = spanY;
            layout.SymbolX = symbolX;
            layout.SymbolY = symbolY;

            string content = EscapeTsplData(payload);
            string modelPart = (cfg.compat_mode || !cfg.use_model2) ? "" : ",M2,S7";
            List<string> lines = new List<string>();
            lines.Add(string.Format("SIZE {0} mm,{1} mm",
                FormatMm(cfg.label_width_mm), FormatMm(cfg.label_height_mm)));
            lines.Add(string.Format("GAP {0} mm,0 mm", FormatMm(cfg.gap_mm)));
            lines.Add("DIRECTION 1");
            lines.Add("REFERENCE 0,0");
            lines.Add("CLS");
            lines.Add(string.Format("QRCODE {0},{1},{2},{3},A,0{4},\"{5}\"",
                symbolX, symbolY, cfg.ecc, cell, modelPart, content));
            lines.Add(string.Format("PRINT 1,{0}", cfg.copies));

            string command = string.Join("\r\n", lines.ToArray()) + "\r\n";
            BuiltLabel built = new BuiltLabel();
            built.Data = encoding.GetBytes(command);
            built.Layout = layout;
            built.PayloadBytes = payloadBytes;
            built.PayloadText = payload;
            built.Warnings = warnings;
            return built;
        }
    }
}
