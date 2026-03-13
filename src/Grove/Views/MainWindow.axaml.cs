using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Grove.ViewModels;
using ReactiveUI;

namespace Grove.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            if (ViewModel is null) return;

            // Register the folder-picker interaction handler — view-layer concern
            d(ViewModel.PickFolder.RegisterHandler(async interaction =>
            {
                var result = await StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions
                    {
                        Title = "Select a git repository root",
                        AllowMultiple = false
                    });

                var path = result.Count > 0 ? result[0].Path.LocalPath : null;
                interaction.SetOutput(path);
            }));
        });
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Double-click toggles maximize/restore
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                BeginMoveDrag(e);
            }
        }
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestore_Click(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            if (MaximizeIcon is not null)
                MaximizeIcon.Text = "\uE922"; // maximize icon
        }
        else
        {
            WindowState = WindowState.Maximized;
            if (MaximizeIcon is not null)
                MaximizeIcon.Text = "\uE923"; // restore icon
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Check if we're actually quitting (via tray menu) or just closing
        var trayVm = App.TrayViewModel;
        if (trayVm is not null && !trayVm.IsQuitting)
        {
            // Minimize to tray instead of closing
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
