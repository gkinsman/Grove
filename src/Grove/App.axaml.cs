using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
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

            // Wire tray icon in code-behind (AXAML binding fails because TrayViewModel isn't set during Initialize)
            WireTrayIcon();

            // Apply initial theme from config
            ApplyTheme(Services.GetRequiredService<Grove.Core.Services.Abstractions.IConfigService>().Config.Theme);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void WireTrayIcon()
    {
        var trayVm = TrayViewModel;

        var showItem = new NativeMenuItem("Show Grove");
        showItem.Click += (_, _) => trayVm.ShowWindowCommand.Execute().Subscribe();

        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => trayVm.QuitCommand.Execute().Subscribe();

        var menu = new NativeMenu();
        menu.Add(showItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(quitItem);

        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Grove/Assets/grove-logo.ico"))),
            ToolTipText = "Grove",
            Menu = menu
        };
        trayIcon.Clicked += (_, _) => trayVm.ShowWindowCommand.Execute().Subscribe();

        var icons = new TrayIcons { trayIcon };
        TrayIcon.SetIcons(this, icons);
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
