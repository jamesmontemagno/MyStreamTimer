using System.Globalization;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MyStreamTimer.WinUI.Helpers;

/// <summary>Parses and formats <c>#RRGGBB</c> / <c>#AARRGGBB</c> colour strings (same grammar as the Swift app's ColorHex).</summary>
public static class ColorHex
{
    public static bool TryParse(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var text = hex.Trim().TrimStart('#');
        if (text.Length != 6 && text.Length != 8)
        {
            return false;
        }

        if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        if (text.Length == 6)
        {
            value |= 0xFF000000;
        }

        color = Color.FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    public static Color Parse(string? hex, Color fallback) => TryParse(hex, out var color) ? color : fallback;

    /// <summary>Formats as <c>#RRGGBB</c> when opaque, otherwise <c>#AARRGGBB</c>.</summary>
    public static string ToHex(Color color) => color.A == 0xFF
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    public static SolidColorBrush ToBrush(string? hex, Color fallback) => new(Parse(hex, fallback));
}
