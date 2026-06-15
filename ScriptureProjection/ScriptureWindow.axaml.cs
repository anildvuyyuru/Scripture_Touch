using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ScriptureFlow_Touch.Entities;
using ScriptureFlow_Touch.Interfaces;

namespace ScriptureFlow_Touch;

public partial class ScriptureWindow : Window
{
    private IScriptureService? _scriptureService;
    private IFileService? _fileService;
    private ITimerService? _timerService;
    private Scripture _data = new();
    private bool _isPopupVisible = false;
    private Animation _showAnimation = null!;
    private Animation _hideAnimation = null!;
    private bool _settingsOpen = false;
    private bool _isUpdatingToggles = false;
    private bool _isUpdatingInterval = false;

    private bool _isRunning = true;
    private string _group = "Promises";
    private readonly int[] _intervals = [5, 10, 20, 30, 60]; // seconds
    private int _intervalIndex = 0;
    private int _verseIndex = 0;

    // serialize operations so timer + buttons don’t overlap
    private readonly SemaphoreSlim _opLock = new(1, 1);

    public ScriptureWindow()
    {
        InitializeComponent();
        InitializeAnimations(); // <- ensure _showAnimation and _hideAnimation are initialized
    }

    public void Initialize(IServiceProvider serviceProvider)
    {
        _scriptureService = serviceProvider.GetService<IScriptureService>();
        _fileService = serviceProvider.GetService<IFileService>();
        _timerService = serviceProvider.GetService<ITimerService>();

        OpenFullScreen();

        _ = LoadVerseDataAsync();
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

    private async void OnScreenTapped(object? sender, PointerPressedEventArgs e)
    {
        Console.WriteLine($"OnScreenTapped, _settingsOpen={_settingsOpen}, visible={SettingsPanel.IsVisible}");

        // If settings panel is open or visible, ignore this tap
        if (_settingsOpen || SettingsPanel.IsVisible)
        {
            e.Handled = true;
            await HideSettingsPanel();
            return;
        }

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

    private async Task SetGroupAsync(string? g)
    {
        if (string.IsNullOrWhiteSpace(g) || !_data.Groups.ContainsKey(g))
            return;

        _group = g;
        _verseIndex = 0;
        await UpdateBackgroundAsync();
        await UpdateVerseAsync();
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

    // Ensure the second parameter is exactly 'RoutedEventArgs e'
    private async void OnPrevTapped(object? sender, RoutedEventArgs e)
    {
        await RunSerialized(PrevVerseAsync);
    }

    // Ensure the second parameter is exactly 'RoutedEventArgs e'
    private async void OnNextTapped(object? sender, RoutedEventArgs e)
    {
        await RunSerialized(NextVerseAsync);
    }

    private async void OnPlayPauseTapped(object? sender, RoutedEventArgs e)
    {
        if (_scriptureService == null)
            return;

        var interval = await _scriptureService.ToggleRun(_intervalIndex, _intervals, _isRunning);
        _isRunning = !_isRunning;

        if (_isRunning && interval != null)
        {
            _timerService?.Start(interval, () => _ = RunSerialized(NextVerseAsync));

            var grid = new Grid { Width = 64, Height = 64 };
            grid.Children.Add(new Avalonia.Controls.Shapes.Path
            {
                Data = StreamGeometry.Parse("M 16,12 L 26,12 L 26,52 L 16,52 Z M 38,12 L 48,12 L 48,52 L 38,52 Z"),
                Fill = Brushes.White,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(16), // Sized down to sit nicely within the ring
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });

            PlayPauseIcon.Content = grid;
        }
        else
        {
            _timerService?.Stop();

            var grid = new Grid { Width = 64, Height = 64 };
            grid.Children.Add(new Avalonia.Controls.Shapes.Path
            {
                Data = StreamGeometry.Parse("M 20,12 L 52,32 L 20,52 Z"),
                Fill = Brushes.White,
                Stretch = Stretch.Uniform,
                // Sized down + extra left offset padding (22 instead of 16) to lock visual center
                Margin = new Thickness(22, 16, 16, 16),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });

            PlayPauseIcon.Content = grid;
        }

        await UpdateInfoAsync();
    }

    private async void OnSettingsTapped(object? sender, RoutedEventArgs e)
    {
        if (!_settingsOpen)
        {
            // Clear slideOut if it exists, apply slideIn
            SettingsPanel.Classes.Remove("slideOut");
            SettingsPanel.Classes.Add("slideIn");
            SettingsPanel.IsVisible = true;
            _settingsOpen = true;
        }
        else
        {
            // Clear slideIn, apply slideOut
            await HideSettingsPanel();
        }
    }

    private async Task HideSettingsPanel()
    {
        SettingsPanel.Classes.Remove("slideIn");
        SettingsPanel.Classes.Add("slideOut");

        _settingsOpen = false;

        // Wait for 300ms animation duration before toggling visibility off 
        // so layout doesn't hide it instantly mid-slide.
        await Task.Delay(300);

        // Only hide it if the user didn't instantly try reopening it during the delay
        if (!_settingsOpen)
        {
            SettingsPanel.IsVisible = false;
        }
    }

    private void SettingsButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private async void OnCategoryCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // Prevent recursive loops when updating programmatically
        if (_isUpdatingToggles) return;

        if (sender is ToggleSwitch clickedVerseCategory)
        {
            // Safety check: If the user tries to turn off the ONLY active switch,
            // force it back to True (ensuring at least one category is always selected).
            if (clickedVerseCategory.IsChecked == false)
            {
                _isUpdatingToggles = true;
                clickedVerseCategory.IsChecked = true;
                _isUpdatingToggles = false;
                return;
            }

            // Reversal logic: If a switch was turned ON, turn all others OFF
            _isUpdatingToggles = true;

            if (clickedVerseCategory != Promises) Promises.IsChecked = false;
            if (clickedVerseCategory != Proverbs) Proverbs.IsChecked = false;
            if (clickedVerseCategory != Psalms) Psalms.IsChecked = false;
            if (clickedVerseCategory != Encouragement) Encouragement.IsChecked = false;

            _isUpdatingToggles = false;

            await SetGroupAsync(clickedVerseCategory.Name);
        }
    }

    private async void OnIntervalCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // Prevent event recursion loop
        if (_isUpdatingInterval) return;

        if (sender is ToggleButton clickedButton)
        {
            // Prevent unchecking the currently active item (forces at least one selection)
            if (clickedButton.IsChecked == false)
            {
                _isUpdatingInterval = true;
                clickedButton.IsChecked = true;
                _isUpdatingInterval = false;
                return;
            }

            _isUpdatingInterval = true;
            TurnOffOtherIntervals(clickedButton);

            _isUpdatingInterval = false;

            // Parse out the timing value from the Tag property
            if (clickedButton.Tag is string tagValue && int.TryParse(tagValue, out int intervalIndex))
            {
                _intervalIndex = intervalIndex;
                System.Diagnostics.Debug.WriteLine($"Interval changed to: {_intervals[_intervalIndex]} seconds");

                // Update your slideshow playback timer interval here
                if (_isRunning)
                {
                    _timerService?.Stop();
                    _timerService?.Start(_intervals[_intervalIndex] * 1000, () => _ = RunSerialized(NextVerseAsync));
                }

                await UpdateInfoAsync();
            }
        }
    }

    private void TurnOffOtherIntervals(ToggleButton clickedButton)
    {
        // Turn off all other segment tabs
        if (clickedButton != Btn5s) Btn5s.IsChecked = false;
        if (clickedButton != Btn10s) Btn10s.IsChecked = false;
        if (clickedButton != Btn20s) Btn20s.IsChecked = false;
        if (clickedButton != Btn30s) Btn30s.IsChecked = false;
        if (clickedButton != Btn60s) Btn60s.IsChecked = false;
    }

    
}
