using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using MyStreamTimer.Core.Settings;

namespace MyStreamTimer.WinUI.Views;

/// <summary>
/// One-time "what's new in 3.0" sheet shown to users upgrading from 2.x (never on a fresh install).
/// The caller decides what to do with the result; <see cref="NavigateToProRequested"/> fires for "Learn about Pro".
/// </summary>
public sealed partial class WelcomeBackDialog : ContentDialog
{
    public WelcomeBackDialog()
    {
        InitializeComponent();
    }

    /// <summary>Raised (on the UI thread) when the user chose "Learn about Pro"; the shell navigates to the Pro page.</summary>
    public static event EventHandler? NavigateToProRequested;

    /// <summary>
    /// Shows the dialog when <c>TimesUsed &gt; 1</c> (i.e. the app had been used before this launch) and the flag is unset.
    /// Call after <c>TimesUsed</c> has been incremented for this launch and the main window is activated.
    /// </summary>
    public static async Task<bool> TryShowAsync(GlobalSettings settings)
    {
        if (settings.HasSeenWelcomeBackV1 || settings.TimesUsed <= 1)
        {
            return false;
        }

        if (App.Window?.Content?.XamlRoot is not { } xamlRoot)
        {
            return false;
        }

        settings.HasSeenWelcomeBackV1 = true;
        try
        {
            var dialog = new WelcomeBackDialog { XamlRoot = xamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Secondary)
            {
                NavigateToProRequested?.Invoke(dialog, EventArgs.Empty);
                Services.NavigationService.Default.NavigateTo(Services.NavigationService.ProTag);
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WelcomeBackDialog] Show failed: {ex.Message}");
            return false;
        }
    }
}

