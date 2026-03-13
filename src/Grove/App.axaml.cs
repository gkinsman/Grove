using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Grove.Core.Models;
using Grove.ViewModels;
using Grove.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Grove;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    // Exposed for tray icon AXAML binding
    public static TrayViewModel TrayViewModel { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = Bootstrapper.Build();
        TrayViewModel = Services.GetRequiredService<TrayViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowVm = Services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = mainWindowVm,
            };

            TrayViewModel.SetMainWindow(mainWindow);
            desktop.MainWindow = mainWindow;

            // Apply initial theme from config
            ApplyTheme(Services.GetRequiredService<Grove.Core.Services.Abstractions.IConfigService>().Config.Theme);
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ApplyTheme(AppTheme theme)
    {
        RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
