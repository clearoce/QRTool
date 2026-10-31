using System;
using System.Drawing;
using System.Drawing.Imaging;
using QRCoder;

namespace QRTool
{
    public static class QrPreview
    {
        public static Bitmap Render(BuiltLabel built, AppConfig cfg)
        {
            QRCodeGenerator generator = new QRCodeGenerator();
            QRCodeGenerator.ECCLevel ecc = ToEcc(cfg.ecc);
            QRCodeData data;
            try
            {
                data = generator.CreateQrCode(
                    built.PayloadText,
                    ecc,
                    true,
                    false,
                    QRCodeGenerator.EciMode.Default,
                    built.Layout.Version);
            }
            catch (Exception)
            {
                data = generator.CreateQrCode(built.PayloadBytes, ecc);
            }

            int modules = 17 + 4 * data.Version;
            int cell = built.Layout.CellWidth;
            int span = (modules + 8) * cell;
            int spanX = (built.Layout.PrintableWidthDots - span) / 2;
            int spanY = (built.Layout.PrintableHeightDots - span) / 2;
            if (spanX < 0) spanX = 0;
            if (spanY < 0) spanY = 0;

            QRCode qr = new QRCode(data);
            using (Bitmap qrBitmap = qr.GetGraphic(cell, Color.Black, Color.White, true))
            {
                Bitmap label = new Bitmap(
                    built.Layout.WidthDots,
                    built.Layout.HeightDots,
                    PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(label))
                {
                    g.Clear(Color.White);
                    g.DrawImageUnscaled(qrBitmap, spanX, spanY);
                }
                return label;
            }
        }

        private static QRCodeGenerator.ECCLevel ToEcc(string ecc)
        {
            switch (ecc)
            {
                case "L": return QRCodeGenerator.ECCLevel.L;
                case "M": return QRCodeGenerator.ECCLevel.M;
                case "Q": return QRCodeGenerator.ECCLevel.Q;
                case "H": return QRCodeGenerator.ECCLevel.H;
                default: return QRCodeGenerator.ECCLevel.M;
            }
        }
    }
}
