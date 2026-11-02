using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace RansomKitV2
{
    internal static class ScannerAsset
    {
        // Public entry for the crypto / primary image.
        public static Image? Load() => LoadAsset("scanner");

        // Shared loader used by both scanner and binance images.
        public static Image? LoadAsset(string baseName)
        {
            var embedded = LoadEmbedded(baseName);
            if (embedded != null) return embedded;

            return LoadFromDisk(baseName);
        }

        // Reads from the exe's embedded resource table.
        public static Image? LoadEmbedded(string baseName)
        {
            var asm = Assembly.GetExecutingAssembly();
            string prefix = asm.GetName().Name + "."; // "RansomKitV2."
            string[] exts = { "png", "jpg", "jpeg" };

            var allNames = asm.GetManifestResourceNames();

            foreach (var ext in exts)
            {
                string target = prefix + baseName + "." + ext;

                foreach (var name in allNames)
                {
                    if (!string.Equals(name, target, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        using var stream = asm.GetManifestResourceStream(name);
                        if (stream == null) continue;

                        using var tmp = Image.FromStream(stream,
                            useEmbeddedColorManagement: false,
                            validateImageData: false);
                        return new Bitmap(tmp);
                    }
                    catch { }
                }
            }
            return null;
        }

        // Fallback: look next to the exe (dev convenience / override).
        public static Image? LoadFromDisk(string baseName)
        {
            string baseDir = AppContext.BaseDirectory;
            string[] exts = { "png", "jpg", "jpeg" };

            foreach (var ext in exts)
            {
                string full = Path.Combine(baseDir, baseName + "." + ext);
                if (!File.Exists(full)) continue;

                try
                {
                    using var fs = new FileStream(full, FileMode.Open,
                        FileAccess.Read, FileShare.ReadWrite);
                    using var ms = new MemoryStream();
                    fs.CopyTo(ms);
                    ms.Position = 0;

                    using var tmp = Image.FromStream(ms,
                        useEmbeddedColorManagement: false,
                        validateImageData: false);
                    return new Bitmap(tmp);
                }
                catch { }
            }
            return null;
        }
    }
}