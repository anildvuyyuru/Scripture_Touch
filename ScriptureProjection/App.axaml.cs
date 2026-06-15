using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ScriptureFlow_Touch.Services;

namespace ScriptureFlow_Touch;
public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
        {
            var scriptureWindow = new ScriptureWindow();
            var serviceProvider = StartUp.BuildContainer().BuildServiceProvider();
            scriptureWindow.Initialize(serviceProvider);
            d.MainWindow = scriptureWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}