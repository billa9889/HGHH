using System;

namespace RansomKitV2
{
    public class RansomTimer
    {
        private System.Threading.Timer? _timer;
        private DateTime _deadlineUtc;
        private bool _isRunning;

        public event Action<TimeSpan>? Tick;
        public event Action? Expired;

        public void Start(TimeSpan duration)
        {
            _deadlineUtc = DateTime.UtcNow.Add(duration);
            Persistence.SaveDeadline(_deadlineUtc);
            StartInternal();
        }

        public void Resume(DateTime deadlineUtc)
        {
            _deadlineUtc = deadlineUtc;
            StartInternal();
        }

        private void StartInternal()
        {
            _isRunning = true;
            // Fully qualified to avoid WinForms Timer ambiguity
            _timer = new System.Threading.Timer(OnTick, null, 0, 100);
        }

        public void Stop()
        {
            _isRunning = false;
            _timer?.Dispose();
            _timer = null;
            Persistence.ClearDeadline();
        }

        private void OnTick(object? state)
        {
            if (!_isRunning) return;

            var remaining = _deadlineUtc - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                Stop();
                Expired?.Invoke();
            }
            else
            {
                Tick?.Invoke(remaining);
            }
        }
    }
}