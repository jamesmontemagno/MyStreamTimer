using System.Diagnostics;
using Windows.Storage;

namespace MyStreamTimer.WinUI.Services;

/// <summary>
/// Resolves the default output folder. The legacy UWP app used
/// <c>Path.Combine(Environment.GetFolderPath(SpecialFolder.CommonApplicationData), "MyStreamTimer")</c>, which in a
/// packaged process is virtualised to <c>%LOCALAPPDATA%\Packages\&lt;PFN&gt;\LocalState\ProgramData\MyStreamTimer</c>.
/// We compute that physical path explicitly so OBS text sources keep pointing at the same files.
/// </summary>
public sealed class DefaultFolderProvider
{
    /// <summary>Physical default output directory (same path the legacy UWP build wrote to).</summary>
    public string DefaultDirectoryPath { get; } =
        Path.Combine(ApplicationData.Current.LocalFolder.Path, "ProgramData", "MyStreamTimer");

    /// <summary>What <c>Environment.GetFolderPath(CommonApplicationData)</c> resolves to in this process (diagnostics only).</summary>
    public static string LegacyCommonAppDataPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MyStreamTimer");

    /// <summary>Writes both candidate paths to the debug output so folder resolution can be verified at startup.</summary>
    public void LogDiagnostics()
    {
        Debug.WriteLine($"[DefaultFolderProvider] DefaultDirectoryPath = {DefaultDirectoryPath}");
        Debug.WriteLine($"[DefaultFolderProvider] Environment CommonApplicationData = {LegacyCommonAppDataPath}");
    }
}
