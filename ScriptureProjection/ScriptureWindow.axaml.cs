using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ScriptureProjection.Interfaces;
using System.Device.Gpio;

namespace ScriptureProjection;

public partial class ScriptureWindow : Window
{
    private IScriptureService? _scriptureService;
    private IGpioSimulatorService? _gpioService;
    private IFileService? _fileService;
    private ITimerService? _timerService;
    private Entities.Scripture _data = new();
    private bool _isPopupVisible = false;
    private Animation _showAnimation;
    private Animation _hideAnimation;

    private readonly Dictionary<string, int> _pins = new()
    {
        ["Promises"] = 5,
        ["Encouragement"] = 6,
        ["Proverbs"] = 13,
        ["Psalms"] = 19,
        ["Freq"] = 26,
        ["StartStop"] = 16,
        ["Next"] = 20,
        ["Prev"] = 21,
        ["Shutdown"] = 12
    };

    private bool _isRunning = true;
    private string _group = "Promises";
    private readonly int[] _intervals = [5, 10, 20, 30, 60]; // seconds
    private int _intervalIndex = 2;
    private int _verseIndex = 0;

    // serialize operations so timer + buttons don’t overlap
    private readonly SemaphoreSlim _opLock = new(1, 1);

    public ScriptureWindow()
    {
        InitializeComponent();
        //InitializeAnimations();
    }

    public void Initialize(IServiceProvider serviceProvider)
    {
        _scriptureService = serviceProvider.GetService<IScriptureService>();
        _gpioService = serviceProvider.GetService<IGpioSimulatorService>();
        _fileService = serviceProvider.GetService<IFileService>();
        _timerService = serviceProvider.GetService<ITimerService>();

        OpenFullScreen();

        _ = LoadVerseDataAsync();
        //InitGpio();
        StartTimer();
        _ = UpdateBackgroundAsync();
        _ = UpdateVerseAsync();
    }

    private void InitializeAnimations()
    {
        _showAnimation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(250),
            Easing = new CubicEaseOut(),
            Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0),
                Setters =
                {
                    new Setter(Visual.OpacityProperty, 0.0),
                    new Setter(TranslateTransform.YProperty, 80.0)
                }
            },
            new KeyFrame
            {
                Cue = new Cue(1),
                Setters =
                {
                    new Setter(Visual.OpacityProperty, 1.0),
                    new Setter(TranslateTransform.YProperty, 0.0)
                }
            }
        }
        };

        _hideAnimation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = new CubicEaseIn(),
            Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0),
                Setters =
                {
                    new Setter(Visual.OpacityProperty, 1.0),
                    new Setter(TranslateTransform.YProperty, 0.0)
                }
            },
            new KeyFrame
            {
                Cue = new Cue(1),
                Setters =
                {
                    new Setter(Visual.OpacityProperty, 0.0),
                    new Setter(TranslateTransform.YProperty, 80.0)
                }
            }
        }
        };
    }

    private async void OnScreenTapped(object? sender, RoutedEventArgs e)
    {
        if (!_isPopupVisible)
        {
            // Prepare and show popup           
            BottomPopup.IsVisible = true;
            // Fade in using existing helper
            await AnimateOpacity(BottomPopup, 1, TimeSpan.FromMilliseconds(200));            
            _isPopupVisible = true;
        }
        else
        {
            // Fade out
            await AnimateOpacity(BottomPopup, 0, TimeSpan.FromMilliseconds(200));           
            BottomPopup.IsVisible = false;
            _isPopupVisible = false;
        }

    }


    private async Task RunSerialized(Func<Task> action)
    {
        if (_scriptureService == null)
            return;

        await _opLock.WaitAsync();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            _opLock.Release();
        }
    }

    private async Task LoadVerseDataAsync()
    {
        if (_scriptureService == null)
            return;

        try
        {
            var verseData = await _scriptureService.LoadScripture();
            if (verseData == null)
            {
                ShowError("verses.json not found next to the app.");
                return;
            }
            _data = verseData;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ShowError("Error loading verses.");
        }
    }

    private void InitGpio()
    {
        foreach (var pin in _pins.Values)
        {
            var isPinOpen = _gpioService?.IsPinOpen(pin);
            if (isPinOpen != null && (bool)!isPinOpen)
                _gpioService?.OpenPin(pin, PinMode.InputPullUp);
        }

        Register("Promises", () => _ = RunSerialized(() => SetGroupAsync("Promises")));
        Register("Encouragement", () => _ = RunSerialized(() => SetGroupAsync("Encouragement")));
        Register("Proverbs", () => _ = RunSerialized(() => SetGroupAsync("Proverbs")));
        Register("Psalms", () => _ = RunSerialized(() => SetGroupAsync("Psalms")));
        Register("Freq", () => _ = RunSerialized(CycleFrequencyAsync));
        Register("StartStop", () => _ = RunSerialized(ToggleRunAsync));
        Register("Next", () => _ = RunSerialized(NextVerseAsync));
        Register("Prev", () => _ = RunSerialized(PrevVerseAsync));
        Register("Shutdown", ShutdownPi);
    }

    private void Register(string key, Action action)
    {
        var pin = _pins[key];
        _gpioService?.RegisterCallback(pin, action);
    }

    private async Task SetBackgroundAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || _fileService == null)
        {
            BgImage.Source = null;
            return;
        }

        var exists = _fileService.FileExists(path);
        if (!exists)
        {
            BgImage.Source = null;
            return;
        }

        try
        {
            // load bitmap off UI thread
            var bmp = await Task.Run(() =>
            {
                try { return new Bitmap(path); }
                catch { return null; }
            });

            BgImage.Source = bmp;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            BgImage.Source = null;
        }
    }

    private void OpenFullScreen()
    {
        WindowState = WindowState.Maximized;
        SystemDecorations = SystemDecorations.None;
        Topmost = true;
        Background = Brushes.Black;
    }

    private void ShowError(string message)
    {
        RefBlock.Text = "";
        VerseText.Text = message;
        InfoLeft.Text = "Error";
    }

    private async Task SetGroupAsync(string g)
    {
        if (!_data.Groups.ContainsKey(g))
            return;

        _group = g;
        _verseIndex = 0;
        await UpdateBackgroundAsync();
        await UpdateVerseAsync();
    }

    private async Task CycleFrequencyAsync()
    {
        if (_scriptureService == null)
            return;

        _intervalIndex = (_intervalIndex + 1) % _intervals.Length;

        var interval = await _scriptureService.ChangeFrequency(_intervalIndex, _intervals, _isRunning);
        if (_isRunning && interval != null)
        {
            _timerService?.Stop();
            _timerService?.Start(interval, () => _ = RunSerialized(NextVerseAsync));
        }

        await UpdateInfoAsync();
    }

    private async Task ToggleRunAsync()
    {
        if (_scriptureService == null)
            return;

        var interval = await _scriptureService.ToggleRun(_intervalIndex, _intervals, _isRunning);
        _isRunning = !_isRunning;

        if (_isRunning && interval != null)
        {
            _timerService?.Start(interval, () => _ = RunSerialized(NextVerseAsync));
        }
        else
        {
            _timerService?.Stop();
        }

        await UpdateInfoAsync();
    }

    private async Task NextVerseAsync()
    {
        if (_scriptureService == null)
            return;

        _verseIndex = await _scriptureService.GetNextVerseIndex(_data, _group, _verseIndex);
        await UpdateVerseAsync();
    }

    private async Task PrevVerseAsync()
    {
        if (_scriptureService == null)
            return;

        _verseIndex = await _scriptureService.GetPreviousVerseIndex(_data, _group, _verseIndex);
        await UpdateVerseAsync();
    }

    private void ShutdownPi()
    {
        InfoLeft.Text = "Shutting down…";
        _timerService?.Stop();
        System.Diagnostics.Process.Start("bash", "-lc \"sudo shutdown -h now\"");
    }

    private async Task UpdateVerseAsync()
    {
        if (_scriptureService == null)
            return;

        try
        {
            var verse = await _scriptureService.GetVerse(_group, _verseIndex);
            if (verse != null)
            {
                SetFont(verse.Text);
                RefBlock.Text = await _scriptureService.GetReferenceInfo(_data, verse);
                await UpdateInfoAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private async Task UpdateBackgroundAsync()
    {
        if (_scriptureService == null)
            return;

        try
        {
            var verse = await _scriptureService.GetVerse(_group, _verseIndex);
            if (verse != null)
            {
                var background = await _scriptureService.GetBackgroundPath(verse, _group, _data);
                await SetBackgroundAsync(background);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private void SetFont(string verseText)
    {
        if (string.IsNullOrWhiteSpace(verseText))
            return;

        bool startsWithQuote = verseText.StartsWith("\"");

        string verseInitial = startsWithQuote
            ? verseText.Substring(0, 2)
            : verseText.Substring(0, 1);

        string verseRest = startsWithQuote
            ? verseText.Substring(2)
            : verseText.Substring(1);

        bool isEscapedInitial = verseInitial.Contains("\"");

        int verseLength = verseText.Length;
        int verseFontSize =
            verseLength >= 400 ? 90 :
            verseLength <= 100 ? 120 :
            75;

        VerseText.Inlines?.Clear();

        if (isEscapedInitial)
        {
            VerseText.Inlines?.Add(new Run
            {
                Text = verseInitial[0].ToString(),
                FontFamily = new FontFamily("Pirata One"),
                FontSize = 150
            });

            VerseText.Inlines?.Add(new Run
            {
                Text = verseInitial[1].ToString(),
                FontFamily = new FontFamily("Pirata One"),
                FontSize = 150,
                Foreground = Brushes.DarkGoldenrod
            });
        }
        else
        {
            VerseText.Inlines?.Add(new Run
            {
                Text = verseInitial,
                FontFamily = new FontFamily("Pirata One"),
                FontSize = 150,
                Foreground = Brushes.DarkGoldenrod
            });
        }

        VerseText.Inlines?.Add(new Run
        {
            Text = verseRest,
            FontFamily = new FontFamily("Pirata One"),
            FontSize = verseFontSize,
            Foreground = Brushes.White
        });
    }

    private void StartTimer()
    {
        if (_isRunning)
        {
            _timerService?.Start(_intervals[_intervalIndex] * 1000,
                () => _ = RunSerialized(NextVerseAsync));
        }
    }

    private async Task UpdateInfoAsync()
    {
        if (_scriptureService == null)
            return;

        try
        {
            InfoLeft.Text = await _scriptureService.GetInfo(
                _data, _group, _intervals, _intervalIndex, _isRunning);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    // Simple UI-thread-safe fade helper to replace missing AnimateOpacity extension.
    private async Task AnimateOpacity(Control control, double to, TimeSpan duration)
    {
        if (control == null)
            return;

        double from = control.Opacity;
        if (Math.Abs(to - from) < 0.01)
        {
            // small difference — set directly
            Dispatcher.UIThread.Post(() => control.Opacity = to);
            return;
        }

        const int frameMs = 16;
        int steps = Math.Max(1, (int)(duration.TotalMilliseconds / frameMs));
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double value = from + (to - from) * t;
            Dispatcher.UIThread.Post(() => control.Opacity = value);
            await Task.Delay(frameMs);
        }

        Dispatcher.UIThread.Post(() => control.Opacity = to);
    }

    private async void OnPrevClicked(object? sender, RoutedEventArgs e)
    {
        if (_scriptureService == null)
            return;

        _verseIndex = await _scriptureService.GetPreviousVerseIndex(_data, _group, _verseIndex);
        await UpdateVerseAsync();
    }

    private async void OnNextClicked(object? sender, RoutedEventArgs e)
    {
        if (_scriptureService == null)
            return;

        _verseIndex = await _scriptureService.GetNextVerseIndex(_data, _group, _verseIndex);
        await UpdateVerseAsync();
    }

    private async void OnPlayPauseClicked(object? sender, RoutedEventArgs e)
    {
        if (_scriptureService == null)
            return;

        var interval = await _scriptureService.ToggleRun(_intervalIndex, _intervals, _isRunning);
        _isRunning = !_isRunning;

        if (_isRunning && interval != null)
        {
            _timerService?.Start(interval, () => _ = RunSerialized(NextVerseAsync));
            PlayPauseIcon.Text = "⏸"; // Pause icon
        }
        else
        {
            _timerService?.Stop();
            PlayPauseIcon.Text = "▶"; // Play icon
        }

        await UpdateInfoAsync();
    }


}
