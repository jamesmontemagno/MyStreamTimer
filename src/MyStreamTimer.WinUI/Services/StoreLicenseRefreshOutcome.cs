namespace MyStreamTimer.WinUI.Services;

public enum StoreLicenseRefreshOutcome
{
    ActiveEntitlements,
    NoActiveEntitlements,
    Unavailable,
    Failed,
}

public static class StoreLicenseRefreshMessages
{
    // Like the other Pro page text, these messages are currently English literals.
    public static string? GetRestoreMessage(this StoreLicenseRefreshOutcome outcome) => outcome switch
    {
        StoreLicenseRefreshOutcome.ActiveEntitlements => null,
        StoreLicenseRefreshOutcome.NoActiveEntitlements =>
            "No active purchases or subscriptions were found for this app. Your cached access has not been cleared.",
        StoreLicenseRefreshOutcome.Unavailable =>
            "The Microsoft Store is unavailable. Try restoring purchases again later. Your cached access has not been cleared.",
        StoreLicenseRefreshOutcome.Failed =>
            "Couldn't check your purchases with the Microsoft Store. Please try again. Your cached access has not been cleared.",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
