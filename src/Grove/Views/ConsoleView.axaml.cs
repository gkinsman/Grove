using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Grove.ViewModels;
using ReactiveUI;

namespace Grove.Views;

public partial class ConsoleView : ReactiveUserControl<WorktreeDetailViewModel>
{
    private bool _isAtBottom = true;

    public ConsoleView()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            var scroller = this.FindControl<ScrollViewer>("ConsoleScroller");
            if (scroller is null) return;

            // Track whether user has scrolled up
            scroller.ScrollChanged += (_, _) =>
            {
                _isAtBottom = scroller.Offset.Y >=
                    scroller.Extent.Height - scroller.Viewport.Height - 20;
            };

            // Auto-scroll when new lines arrive (if at bottom)
            this.WhenAnyValue(x => x.ViewModel!.ConsoleLines.Count)
                .Where(_ => _isAtBottom)
                .Throttle(TimeSpan.FromMilliseconds(50))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => scroller.ScrollToEnd())
                .DisposeWith(d);
        });
    }
}
