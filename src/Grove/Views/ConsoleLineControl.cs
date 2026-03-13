using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Grove.Core.Models;

namespace Grove.Views;

/// <summary>
/// Custom control that renders a single ConsoleLine with ANSI-colored spans.
/// Uses direct DrawingContext rendering for performance with thousands of lines.
/// </summary>
public class ConsoleLineControl : Control
{
    public static readonly StyledProperty<ConsoleLine?> LineProperty =
        AvaloniaProperty.Register<ConsoleLineControl, ConsoleLine?>(nameof(Line));

    public ConsoleLine? Line
    {
        get => GetValue(LineProperty);
        set => SetValue(LineProperty, value);
    }

    static ConsoleLineControl()
    {
        AffectsRender<ConsoleLineControl>(LineProperty);
        AffectsMeasure<ConsoleLineControl>(LineProperty);
    }

    private static readonly Typeface ConsoleTypeface =
        new("Cascadia Code,Cascadia Mono,Consolas,Courier New,monospace");

    private const double FontSize = 13.0;
    private const double LineHeight = 20.0;
    private const double LeftPadding = 16.0;

    private static readonly IBrush DefaultForeground =
        new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)); // #CDD6F4

    public override void Render(DrawingContext context)
    {
        if (Line is null) return;

        double x = LeftPadding;

        foreach (var span in Line.Spans)
        {
            if (string.IsNullOrEmpty(span.Text)) continue;

            IBrush brush = span.Foreground is { } fg
                ? new SolidColorBrush(Color.FromRgb(fg.R, fg.G, fg.B))
                : DefaultForeground;

            var formattedText = new FormattedText(
                span.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ConsoleTypeface,
                FontSize,
                brush);

            if (span.IsBold)
                formattedText.SetFontWeight(FontWeight.Bold, 0, span.Text.Length);

            context.DrawText(formattedText, new Point(x, 2));
            x += formattedText.Width;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(availableSize.Width, LineHeight);
    }
}
