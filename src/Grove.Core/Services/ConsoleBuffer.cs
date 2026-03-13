using DynamicData;
using Grove.Core.Models;

namespace Grove.Core.Services;

public sealed class ConsoleBuffer : IDisposable
{
    private readonly SourceList<ConsoleLine> _lines = new();
    private readonly AnsiParser _parser = new();
    private readonly int _maxLines;
    private IDisposable? _subscription;
    private readonly object _lock = new();

    public ConsoleBuffer(int maxLines = 10_000)
    {
        _maxLines = maxLines;
    }

    public IObservable<IChangeSet<ConsoleLine>> Connect() => _lines.Connect();

    public void Attach(IObservable<string> outputStream)
    {
        lock (_lock)
        {
            _subscription?.Dispose();
            _subscription = outputStream.Subscribe(AddLine);
        }
    }

    public void AddLine(string rawLine)
    {
        var parsed = _parser.Parse(rawLine);
        _lines.Edit(list =>
        {
            list.Add(parsed);
            // Trim oldest lines if over capacity
            while (list.Count > _maxLines)
                list.RemoveAt(0);
        });
    }

    public void Clear() => _lines.Clear();

    public void Detach()
    {
        lock (_lock)
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _subscription?.Dispose();
        }
        _lines.Dispose();
    }
}
