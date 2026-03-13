namespace Grove.Core.Models;

public sealed class ConsoleLine
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public IReadOnlyList<ConsoleSpan> Spans { get; init; } = [];
    public string RawText { get; init; } = string.Empty;
}
