namespace ScriptureFlow_Touch.Interfaces
{
    public interface ITimerService
    {
        void Start(int? intervalMs, Action onElapsed);
        void Stop();
        void UpdateInterval(int? interval);
    }
}
