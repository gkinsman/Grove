using System.Collections.Specialized;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Grove.Core.Models;
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
            var textBlock = this.FindControl<SelectableTextBlock>("ConsoleTextBlock");
            var copyButton = this.FindControl<Button>("CopyButton");
            if (scroller is null || textBlock is null) return;

            textBlock.Inlines ??= [];

            // Copy button — copies all console text to clipboard
            if (copyButton is not null)
            {
                copyButton.Click += OnCopyClick;
                Disposable.Create(() => copyButton.Click -= OnCopyClick)
                    .DisposeWith(d);
            }

            // Track whether user has scrolled up
            scroller.ScrollChanged += (_, _) =>
            {
                _isAtBottom = scroller.Offset.Y >=
                    scroller.Extent.Height - scroller.Viewport.Height - 20;
            };

            // Populate existing lines
            if (ViewModel?.ConsoleLines is { } lines)
            {
                foreach (var line in lines)
                    AppendLine(textBlock, line);

                // Watch for new/removed lines
                void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
                {
                    if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
                    {
                        foreach (ConsoleLine newLine in e.NewItems)
                            AppendLine(textBlock, newLine);

                        if (_isAtBottom)
                            scroller.ScrollToEnd();
                    }
                    else if (e.Action == NotifyCollectionChangedAction.Reset)
                    {
                        textBlock.Inlines?.Clear();
                    }
                }

                var notifier = (INotifyCollectionChanged)lines;
                notifier.CollectionChanged += OnCollectionChanged;
                Disposable.Create(() => notifier.CollectionChanged -= OnCollectionChanged)
                    .DisposeWith(d);
            }
        });
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.ConsoleLines is not { } lines) return;

        var text = string.Join(Environment.NewLine,
            lines.Select(l => l.RawText));

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }

    private static void AppendLine(SelectableTextBlock textBlock, ConsoleLine line)
    {
        if (textBlock.Inlines is null) return;

        // Add newline before each line (except the first)
        if (textBlock.Inlines.Count > 0)
            textBlock.Inlines.Add(new LineBreak());

        var addedAny = false;
        foreach (var span in line.Spans)
        {
            if (string.IsNullOrEmpty(span.Text))
                continue;

            var run = new Run(span.Text);

            if (span.Foreground is { } fg)
                run.Foreground = new SolidColorBrush(Color.FromRgb(fg.R, fg.G, fg.B));

            if (span.Background is { } bg)
                run.Background = new SolidColorBrush(Color.FromRgb(bg.R, bg.G, bg.B));

            if (span.IsBold)
                run.FontWeight = FontWeight.Bold;

            if (span.IsItalic)
                run.FontStyle = FontStyle.Italic;

            if (span.IsUnderline)
                run.TextDecorations = TextDecorations.Underline;

            textBlock.Inlines.Add(run);
            addedAny = true;
        }

        // Fallback: if no spans, show raw text
        if (!addedAny && !string.IsNullOrEmpty(line.RawText))
            textBlock.Inlines.Add(new Run(line.RawText));
    }
}
