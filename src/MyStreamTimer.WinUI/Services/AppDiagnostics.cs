using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32;
using MyStreamTimer.Core.Diagnostics;
using Windows.ApplicationModel;

namespace MyStreamTimer.WinUI.Services;

public static class AppDiagnostics
{
    static DiagnosticLog? log;

    public static string? DirectoryPath => log?.DirectoryPath;

    public static void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Record("managed-unhandled", $"terminating={args.IsTerminating}", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
            Record("unobserved-task", exception: args.Exception);
        try
        {
            uint length = 0;
            var result = GetCurrentPackageFamilyName(ref length, null);
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyStreamTimer");
            if (result == 122)
            {
                var family = new StringBuilder((int)length);
                result = GetCurrentPackageFamilyName(ref length, family);
                if (result != 0)
                {
                    Trace.TraceError($"Local diagnostics package lookup failed: Win32 error {result}.");
                    return;
                }
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Packages", family.ToString(), "LocalState");
            }
            else if (result != 15700)
            {
                Trace.TraceError($"Local diagnostics package lookup failed: Win32 error {result}.");
                return;
            }
            log = new DiagnosticLog(Path.Combine(folder, "logs"));
            Record("process-entry", $"version={Assembly.GetExecutingAssembly().GetName().Version}; " +
                $"build={Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}; " +
                $"architecture={RuntimeInformation.ProcessArchitecture}; os={Environment.OSVersion.Version}; " +
                $"runtime={RuntimeInformation.FrameworkDescription}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or Win32Exception)
        {
            Trace.TraceError($"Local diagnostics initialization failed: {ex}");
        }
    }

    public static void Record(string eventName, string? details = null, Exception? exception = null)
    {
        try
        {
            if (log is null)
            {
                Trace.TraceError($"Diagnostics unavailable: {eventName}; exception={exception?.GetType().FullName}");
                return;
            }
            log.Write(eventName, details, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            Trace.TraceError($"Could not persist diagnostics: {ex}");
        }
    }

    public static void RecordEnvironment()
    {
        try
        {
            var id = Package.Current.Id;
            Record("package", $"name={id.Name}; version={id.Version.Major}.{id.Version.Minor}.{id.Version.Build}.{id.Version.Revision}");
            var revision = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR", null);
            Record("os-revision", $"build={Environment.OSVersion.Version.Build}; revision={revision}");
            foreach (var dependency in Package.Current.Dependencies.Where(p => p.Id.Name.StartsWith("Microsoft.WindowsAppRuntime.", StringComparison.Ordinal)))
            {
                var version = dependency.Id.Version;
                Record("windows-app-runtime", $"{dependency.Id.Name}; version={version.Major}.{version.Minor}.{version.Build}.{version.Revision}");
            }
            using var process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
            {
                if (module.ModuleName is "coreclr.dll" or "Microsoft.UI.Xaml.dll")
                    Record("loaded-module", $"{module.ModuleName}; version={module.FileVersionInfo.FileVersion}");
            }
            var runtimes = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet", "shared", "Microsoft.NETCore.App");
            if (Directory.Exists(runtimes))
                Record("installed-dotnet", string.Join(", ", Directory.EnumerateDirectories(runtimes).Select(Path.GetFileName)));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or IOException or UnauthorizedAccessException or SecurityException or Win32Exception)
        {
            Record("environment-capture-failed", exception: ex);
        }
    }

    public static bool TryExport(string destinationPath, out string? error)
    {
        if (log is null)
        {
            error = "Local diagnostics are unavailable. Try restarting the app.";
            Record("diagnostics-export-unavailable");
            return false;
        }
        var saved = log.TryExport(destinationPath, out error);
        if (!saved)
            Record("diagnostics-export-failed", error);
        return saved;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFamilyName(ref uint packageFamilyNameLength, StringBuilder? packageFamilyName);
}
