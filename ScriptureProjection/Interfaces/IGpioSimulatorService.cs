using System.Device.Gpio;

namespace ScriptureProjection.Interfaces
{
    public interface IGpioSimulatorService : IDisposable
    {
        bool? IsPinOpen(int pin);
        void OpenPin(int pin, PinMode mode);
        void ClosePin(int pin);
        void RegisterCallback(int pin, Action callback);
    }
}
