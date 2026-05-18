using Avalonia.Threading;
using ScriptureProjection.Interfaces;
using System.Device.Gpio;
using System.Collections.Concurrent;

namespace ScriptureProjection.Services
{
    public class GpioService : IGpioService, IDisposable
    {
        private readonly GpioController _controller = new();
        private readonly ConcurrentDictionary<int, DateTime> _lastTrigger = new();
        private readonly ConcurrentDictionary<int, PinChangeEventHandler> _handlers = new();

        private readonly int _debounceMs = 150; // adjust as needed

        public bool? IsPinOpen(int pin) => _controller.IsPinOpen(pin);

        public void OpenPin(int pin, PinMode mode)
        {
            if (!_controller.IsPinOpen(pin))
                _controller.OpenPin(pin, mode);
        }

        public void ClosePin(int pin)
        {
            if (_controller.IsPinOpen(pin))
                _controller.ClosePin(pin);
        }

        public void RegisterCallback(int pin, Action callback)
        {
            // Remove old handler if exists
            if (_handlers.TryRemove(pin, out var oldHandler))
                _controller.UnregisterCallbackForPinValueChangedEvent(pin, oldHandler);

            PinChangeEventHandler handler = (_, args) =>
            {
                if (args.ChangeType != PinEventTypes.Falling)
                    return;

                var now = DateTime.UtcNow;

                // Debounce check
                if (_lastTrigger.TryGetValue(pin, out var last) &&
                    (now - last).TotalMilliseconds < _debounceMs)
                {
                    return; // ignore bounce
                }

                _lastTrigger[pin] = now;

                Console.WriteLine($"GPIO pin {pin} triggered callback.");
                // Execute on UI thread
                Dispatcher.UIThread.Post(callback);
            };

            _handlers[pin] = handler;

            _controller.RegisterCallbackForPinValueChangedEvent(
                pin,
                PinEventTypes.Falling,
                handler
            );
        }

        public void Dispose()
        {
            foreach (var kvp in _handlers)
            {
                _controller.UnregisterCallbackForPinValueChangedEvent(kvp.Key, kvp.Value);
            }

            _handlers.Clear();
            _controller.Dispose();
        }
    }
}
