using System.Diagnostics;

namespace MyStreamTimer.WinUI.Helpers;

/// <summary>Sorted, cached list of installed font family names (Win2D/DirectWrite, with a curated fallback).</summary>
public static class FontFamilies
{
    private static readonly string[] Fallback =
    [
        "Arial", "Bahnschrift", "Calibri", "Cambria", "Cascadia Code", "Cascadia Mono", "Comic Sans MS", "Consolas",
        "Courier New", "Georgia", "Impact", "Lucida Console", "Segoe UI", "Segoe UI Variable", "Tahoma",
        "Times New Roman", "Trebuchet MS", "Verdana",
    ];

    private static IReadOnlyList<string>? _cache;

    public static IReadOnlyList<string> GetSystemFontFamilies()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        try
        {
            var names = Microsoft.Graphics.Canvas.Text.CanvasTextFormat.GetSystemFontFamilies();
            _cache = names
                .Where(n => !string.IsNullOrWhiteSpace(n) && !n.StartsWith('@'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FontFamilies] Win2D enumeration failed, using fallback: {ex.Message}");
            _cache = Fallback;
        }

        if (_cache.Count == 0)
        {
            _cache = Fallback;
        }

        return _cache;
    }
}
