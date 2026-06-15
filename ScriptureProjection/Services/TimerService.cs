using Avalonia.Threading;
using ScriptureFlow_Touch.Interfaces;
using System.Timers;

namespace ScriptureFlow_Touch.Services
{
    public class TimerService : ITimerService
    {
        private System.Timers.Timer? _timer;
        private ElapsedEventHandler? _handler;

        public void Start(int? intervalMs, Action onElapsed)
        {
            Stop(); // stop and clean up any existing timer

            _timer = new System.Timers.Timer(intervalMs ?? 0);
            _timer.AutoReset = true;

            _handler = (_, __) => Dispatcher.UIThread.Post(onElapsed);

            _timer.Elapsed += _handler;
            _timer.Start();
        }

        public void Stop()
        {
            if (_timer != null)
            {
                if (_handler != null)
                    _timer.Elapsed -= _handler;

                _timer.Stop();
                _timer.Dispose();
            }

            _timer = null;
            _handler = null;
        }

        public void UpdateInterval(int? interval)
        {
            if (_timer != null)
                _timer.Interval = interval ?? 0;
        }
    }
}
