using Microsoft.UI.Xaml;

namespace MyStreamTimer.WinUI.Helpers;

/// <summary>Static conversion functions for <c>x:Bind</c> function paths (preferred over IValueConverter).</summary>
public static class Bind
{
    public static Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToVisibilityInverse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility ToVisibility(string? value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility ToVisibilityInverse(string? value) => string.IsNullOrEmpty(value) ? Visibility.Visible : Visibility.Collapsed;

    public static bool Not(bool value) => !value;

    public static bool IsNotNullOrEmpty(string? value) => !string.IsNullOrEmpty(value);
}
