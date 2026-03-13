using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Grove.Services.Abstractions;
using ReactiveUI;

namespace Grove.ViewModels;

public class TrayViewModel : ReactiveObject
{
    private readonly IProcessManager _processManager;
    private Window? _mainWindow;
    private bool _isQuitting;

    public ReactiveCommand<Unit, Unit> ShowWindowCommand { get; }
    public ReactiveCommand<Unit, Unit> QuitCommand { get; }

    public TrayViewModel(IProcessManager processManager)
    {
        _processManager = processManager;

        ShowWindowCommand = ReactiveCommand.Create(ShowWindow);
        QuitCommand = ReactiveCommand.CreateFromTask(QuitAsync);
    }

    public void SetMainWindow(Window window)
    {
        _mainWindow = window;
    }

    private void ShowWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private async Task QuitAsync(CancellationToken ct)
    {
        _isQuitting = true;
        await _processManager.StopAllAsync();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public bool IsQuitting => _isQuitting;
}
