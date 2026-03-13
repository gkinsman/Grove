using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using Grove.Models;
using Grove.ViewModels;
using ReactiveUI;

namespace Grove.Views;

public partial class AddWorktreeDialog : ReactiveWindow<AddWorktreeViewModel>
{
    public AddWorktreeDialog()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            ViewModel!.CreateCommand
                .Subscribe(result => Close(result))
                .DisposeWith(d);
            ViewModel!.CancelCommand
                .Subscribe(_ => Close(null))
                .DisposeWith(d);
        });
    }
}
