namespace Grove.Models;

public sealed record ConsoleSpan(
    string Text,
    AnsiColor? Foreground = null,
    AnsiColor? Background = null,
    bool IsBold = false,
    bool IsItalic = false,
    bool IsUnderline = false
);
