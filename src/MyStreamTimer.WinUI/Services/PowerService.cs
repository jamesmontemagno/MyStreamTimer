using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MyStreamTimer.WinUI.Services;

/// <summary>
/// Keeps the system and display awake while at least one timer is running, using a Win32 power request
/// (<c>PowerCreateRequest</c> / <c>PowerSetRequest</c>). Visible in <c>powercfg /requests</c>.
/// All calls are marshalled to the UI thread.
/// </summary>
public sealed class PowerService
{
    private const string Reason = "My Stream Timer is running a timer";

    private nint _request;
    private bool _isActive;

    /// <summary>True while a power request is held.</summary>
    public bool IsKeepAwake => _isActive;

    public void Acquire() => RunOnUi(() =>
    {
        if (_isActive)
        {
            return;
        }

        try
        {
            if (_request == nint.Zero)
            {
                var context = new ReasonContext
                {
                    Version = PowerRequestContextVersion,
                    Flags = PowerRequestContextSimpleString,
                    SimpleReasonString = Reason,
                };
                _request = PowerCreateRequest(ref context);
                if (_request == nint.Zero || _request == InvalidHandle)
                {
                    _request = nint.Zero;
                    Debug.WriteLine($"[PowerService] PowerCreateRequest failed: {Marshal.GetLastWin32Error()}");
                    return;
                }
            }

            PowerSetRequest(_request, PowerRequestType.DisplayRequired);
            PowerSetRequest(_request, PowerRequestType.SystemRequired);
            _isActive = true;
            Debug.WriteLine("[PowerService] Power request active");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PowerService] Acquire failed: {ex.Message}");
        }
    });

    public void Release() => RunOnUi(() =>
    {
        if (!_isActive || _request == nint.Zero)
        {
            return;
        }

        try
        {
            PowerClearRequest(_request, PowerRequestType.DisplayRequired);
            PowerClearRequest(_request, PowerRequestType.SystemRequired);
            Debug.WriteLine("[PowerService] Power request released");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PowerService] Release failed: {ex.Message}");
        }
        finally
        {
            _isActive = false;
        }
    });

    private static void RunOnUi(Action action)
    {
        var queue = App.DispatcherQueue;
        if (queue is null || queue.HasThreadAccess)
        {
            action();
        }
        else
        {
            queue.TryEnqueue(() => action());
        }
    }

    // ---------- Win32 interop ----------

    private static readonly nint InvalidHandle = -1;
    private const int PowerRequestContextVersion = 0;
    private const int PowerRequestContextSimpleString = 0x1;

    private enum PowerRequestType
    {
        DisplayRequired = 0,
        SystemRequired = 1,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string SimpleReasonString;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint PowerCreateRequest(ref ReasonContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PowerSetRequest(nint powerRequest, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PowerClearRequest(nint powerRequest, PowerRequestType requestType);
}
