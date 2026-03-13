using System.Reactive.Disposables;
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

    protected override void OnClosing(Avalonia.Controls.WindowClosingEventArgs e)
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
