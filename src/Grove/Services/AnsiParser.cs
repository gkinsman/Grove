using System.Text.RegularExpressions;
using Grove.Models;

namespace Grove.Services;

public sealed class AnsiParser
{
    private static readonly Regex SgrRegex = new(@"\x1B\[([0-9;]*)m", RegexOptions.Compiled);
    private static readonly Regex OtherEscapeRegex = new(@"\x1B\[[^m]*[A-Za-z]", RegexOptions.Compiled);

    // Stateful parser — tracks current style across calls
    private AnsiColor? _currentFg;
    private AnsiColor? _currentBg;
    private bool _bold;
    private bool _italic;
    private bool _underline;

    public ConsoleLine Parse(string rawLine)
    {
        var spans = new List<ConsoleSpan>();
        var lastIndex = 0;

        foreach (Match match in SgrRegex.Matches(rawLine))
        {
            // Add text before this escape sequence
            if (match.Index > lastIndex)
            {
                var text = rawLine[lastIndex..match.Index];
                if (text.Length > 0)
                    spans.Add(new ConsoleSpan(text, _currentFg, _currentBg, _bold, _italic, _underline));
            }

            // Process SGR codes
            ProcessSgrCodes(match.Groups[1].Value);
            lastIndex = match.Index + match.Length;
        }

        // Remaining text after last escape sequence
        if (lastIndex < rawLine.Length)
        {
            var remaining = rawLine[lastIndex..];
            // Strip any other escape sequences (cursor movement, etc.)
            remaining = OtherEscapeRegex.Replace(remaining, string.Empty);
            if (remaining.Length > 0)
                spans.Add(new ConsoleSpan(remaining, _currentFg, _currentBg, _bold, _italic, _underline));
        }

        // If no spans were created, add the raw text as a single span
        if (spans.Count == 0 && rawLine.Length > 0)
        {
            var cleaned = OtherEscapeRegex.Replace(SgrRegex.Replace(rawLine, string.Empty), string.Empty);
            if (cleaned.Length > 0)
                spans.Add(new ConsoleSpan(cleaned));
        }

        return new ConsoleLine { Spans = spans, RawText = rawLine };
    }

    private void ProcessSgrCodes(string codes)
    {
        if (string.IsNullOrEmpty(codes))
        {
            // ESC[m = reset
            Reset();
            return;
        }

        var parts = codes.Split(';');
        var i = 0;
        while (i < parts.Length)
        {
            if (!int.TryParse(parts[i], out var code))
            {
                i++;
                continue;
            }

            switch (code)
            {
                case 0: Reset(); break;
                case 1: _bold = true; break;
                case 3: _italic = true; break;
                case 4: _underline = true; break;
                case 22: _bold = false; break;
                case 23: _italic = false; break;
                case 24: _underline = false; break;
                case >= 30 and <= 37: _currentFg = AnsiColor.FromAnsiCode(code); break;
                case 38:
                    // Extended foreground color
                    if (i + 1 < parts.Length && parts[i + 1] == "5" && i + 2 < parts.Length)
                    {
                        if (int.TryParse(parts[i + 2], out var idx))
                            _currentFg = AnsiColor.From256(idx);
                        i += 2;
                    }
                    else if (i + 1 < parts.Length && parts[i + 1] == "2" && i + 4 < parts.Length)
                    {
                        if (int.TryParse(parts[i + 2], out var r) &&
                            int.TryParse(parts[i + 3], out var g) &&
                            int.TryParse(parts[i + 4], out var b))
                            _currentFg = new AnsiColor((byte)r, (byte)g, (byte)b);
                        i += 4;
                    }
                    break;
                case 39: _currentFg = null; break;
                case >= 40 and <= 47: _currentBg = AnsiColor.FromAnsiCode(code - 10); break;
                case 48:
                    // Extended background color
                    if (i + 1 < parts.Length && parts[i + 1] == "5" && i + 2 < parts.Length)
                    {
                        if (int.TryParse(parts[i + 2], out var idx))
                            _currentBg = AnsiColor.From256(idx);
                        i += 2;
                    }
                    else if (i + 1 < parts.Length && parts[i + 1] == "2" && i + 4 < parts.Length)
                    {
                        if (int.TryParse(parts[i + 2], out var r) &&
                            int.TryParse(parts[i + 3], out var g) &&
                            int.TryParse(parts[i + 4], out var b))
                            _currentBg = new AnsiColor((byte)r, (byte)g, (byte)b);
                        i += 4;
                    }
                    break;
                case 49: _currentBg = null; break;
                case >= 90 and <= 97: _currentFg = AnsiColor.FromAnsiCode(code); break;
                case >= 100 and <= 107: _currentBg = AnsiColor.FromAnsiCode(code - 10); break;
            }

            i++;
        }
    }

    private void Reset()
    {
        _currentFg = null;
        _currentBg = null;
        _bold = false;
        _italic = false;
        _underline = false;
    }
}
