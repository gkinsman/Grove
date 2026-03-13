namespace Grove.Models;

public readonly record struct AnsiColor(byte R, byte G, byte B)
{
    // Standard 16 ANSI colors (dark variants, 30-37)
    public static readonly AnsiColor Black = new(0, 0, 0);
    public static readonly AnsiColor Red = new(205, 49, 49);
    public static readonly AnsiColor Green = new(13, 188, 121);
    public static readonly AnsiColor Yellow = new(229, 229, 16);
    public static readonly AnsiColor Blue = new(36, 114, 200);
    public static readonly AnsiColor Magenta = new(188, 63, 188);
    public static readonly AnsiColor Cyan = new(17, 168, 205);
    public static readonly AnsiColor White = new(229, 229, 229);

    // Bright variants (90-97)
    public static readonly AnsiColor BrightBlack = new(102, 102, 102);
    public static readonly AnsiColor BrightRed = new(241, 76, 76);
    public static readonly AnsiColor BrightGreen = new(35, 209, 139);
    public static readonly AnsiColor BrightYellow = new(245, 245, 67);
    public static readonly AnsiColor BrightBlue = new(59, 142, 234);
    public static readonly AnsiColor BrightMagenta = new(214, 112, 214);
    public static readonly AnsiColor BrightCyan = new(41, 184, 219);
    public static readonly AnsiColor BrightWhite = new(229, 229, 229);

    public static AnsiColor FromAnsiCode(int code) => code switch
    {
        30 => Black, 31 => Red, 32 => Green, 33 => Yellow,
        34 => Blue, 35 => Magenta, 36 => Cyan, 37 => White,
        90 => BrightBlack, 91 => BrightRed, 92 => BrightGreen, 93 => BrightYellow,
        94 => BrightBlue, 95 => BrightMagenta, 96 => BrightCyan, 97 => BrightWhite,
        _ => White
    };

    public static AnsiColor From256(int index)
    {
        if (index < 16) return FromAnsiCode(index < 8 ? index + 30 : index + 82);
        if (index < 232)
        {
            var i = index - 16;
            var b = (byte)((i % 6) * 51);
            var g = (byte)(((i / 6) % 6) * 51);
            var r = (byte)((i / 36) * 51);
            return new AnsiColor(r, g, b);
        }
        // Grayscale 232-255
        var gray = (byte)(8 + (index - 232) * 10);
        return new AnsiColor(gray, gray, gray);
    }
}
