using Avalonia.Threading;
using ScriptureProjection.Interfaces;
using System.Device.Gpio;

namespace ScriptureProjection.Services
{
    public class GpioServiceSimulator : IGpioSimulatorService
    {
        private readonly Dictionary<int, PinMode> _pinModes = new();
        private readonly Dictionary<int, bool> _pinStates = new();
        private readonly Dictionary<int, Action> _callbacks = new();

        public bool? IsPinOpen(int pin) => _pinModes.ContainsKey(pin);

        public void OpenPin(int pin, PinMode mode)
        {
            _pinModes[pin] = mode;
            _pinStates[pin] = false;
        }

        public void ClosePin(int pin)
        {
            _pinModes.Remove(pin);
            _pinStates.Remove(pin);
            _callbacks.Remove(pin);
        }

        public void RegisterCallback(int pin, Action callback)
        {
            _callbacks[pin] = callback;
        }

        public void SimulateFallingEdge(int pin)
        {
            if (_callbacks.TryGetValue(pin, out var callback))
            {
                Dispatcher.UIThread.Post(callback);
            }
        }

        public void Dispose()
        {
            _pinModes.Clear();
            _pinStates.Clear();
            _callbacks.Clear();
        }
    }
}