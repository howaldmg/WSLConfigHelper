using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WSLConfigHelper.Core.Native;

namespace WSLConfigHelper.Core.SystemInfo;

/// <summary>
/// Represents physical and workspace layout information for a display monitor.
/// </summary>
/// <param name="DisplayNumber">The 1-based display number identified by Windows.</param>
/// <param name="DeviceName">The device name (e.g. \\.\DISPLAY1).</param>
/// <param name="Width">The physical pixel width of the display.</param>
/// <param name="Height">The physical pixel height of the display.</param>
/// <param name="Left">The virtual desktop X offset of the display.</param>
/// <param name="Top">The virtual desktop Y offset of the display.</param>
/// <param name="WorkWidth">The usable work area width excluding taskbars.</param>
/// <param name="WorkHeight">The usable work area height excluding taskbars.</param>
/// <param name="WorkLeft">The work area X offset.</param>
/// <param name="WorkTop">The work area Y offset.</param>
/// <param name="IsPrimary">Indicates if this display is the Windows primary display.</param>
/// <param name="WslgX">The normalized WSLg X coordinate offset.</param>
/// <param name="WslgY">The normalized WSLg Y coordinate offset.</param>
/// <param name="RefreshRate">The hardware refresh rate in Hertz (e.g. 60, 144).</param>
public record DisplayInfo(
    int DisplayNumber,
    string DeviceName,
    int Width,
    int Height,
    int Left,
    int Top,
    int WorkWidth,
    int WorkHeight,
    int WorkLeft,
    int WorkTop,
    bool IsPrimary,
    int WslgX = 0,
    int WslgY = 0,
    int RefreshRate = 60);

/// <summary>
/// Contract for querying connected display monitors and their spatial topology.
/// </summary>
public interface IDisplayProber
{
    /// <summary>
    /// Retrieves a read-only list of all currently connected display monitors.
    /// </summary>
    IReadOnlyList<DisplayInfo> GetConnectedDisplays();
}

/// <summary>
/// Queries display topology and hardware refresh rates using Win32 display APIs.
/// </summary>
public class DisplayProber : IDisplayProber
{
    private static bool _dpiAwareInitialized = false;

    private static void EnsureDpiAwareness()
    {
        if (_dpiAwareInitialized) return;
        _dpiAwareInitialized = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
                Win32Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch
            {
                try { Win32Native.SetProcessDPIAware(); } catch { }
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DisplayInfo> GetConnectedDisplays()
    {
        EnsureDpiAwareness();
        var rawDisplays = new List<DisplayInfo>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            rawDisplays.Add(new DisplayInfo(1, "DISPLAY1", 1920, 1080, 0, 0, 1920, 1040, 0, 0, true));
            return rawDisplays;
        }

        try
        {
            Win32Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref Win32Native.RECT rc, IntPtr data) =>
            {
                var mi = new Win32Native.MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(Win32Native.MONITORINFOEX));
                if (Win32Native.GetMonitorInfo(hMonitor, ref mi))
                {
                    bool isPrimary = (mi.dwFlags & 1) != 0;
                    string devName = mi.szDevice ?? "";

                    int dispNum = 0;
                    var match = Regex.Match(devName, @"DISPLAY(\d+)", RegexOptions.IgnoreCase);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int parsedNum))
                    {
                        dispNum = parsedNum;
                    }

                    var dm = new Win32Native.DEVMODE();
                    dm.dmSize = (ushort)Marshal.SizeOf(typeof(Win32Native.DEVMODE));
                    if (Win32Native.EnumDisplaySettings(devName, Win32Native.ENUM_CURRENT_SETTINGS, ref dm) && dm.dmPelsWidth > 0 && dm.dmPelsHeight > 0)
                    {
                        int physW = (int)dm.dmPelsWidth;
                        int physH = (int)dm.dmPelsHeight;
                        int refresh = dm.dmDisplayFrequency > 0 ? (int)dm.dmDisplayFrequency : 60;

                        rawDisplays.Add(new DisplayInfo(
                            DisplayNumber: dispNum,
                            DeviceName: devName,
                            Width: physW,
                            Height: physH,
                            Left: dm.dmPositionX,
                            Top: dm.dmPositionY,
                            WorkWidth: mi.rcWork.Width,
                            WorkHeight: mi.rcWork.Height,
                            WorkLeft: mi.rcWork.Left,
                            WorkTop: mi.rcWork.Top,
                            IsPrimary: isPrimary,
                            RefreshRate: refresh));
                    }
                    else
                    {
                        rawDisplays.Add(new DisplayInfo(
                            DisplayNumber: dispNum,
                            DeviceName: devName,
                            Width: mi.rcMonitor.Width,
                            Height: mi.rcMonitor.Height,
                            Left: mi.rcMonitor.Left,
                            Top: mi.rcMonitor.Top,
                            WorkWidth: mi.rcWork.Width,
                            WorkHeight: mi.rcWork.Height,
                            WorkLeft: mi.rcWork.Left,
                            WorkTop: mi.rcWork.Top,
                            IsPrimary: isPrimary,
                            RefreshRate: 60));
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // Fallback default
            rawDisplays.Add(new DisplayInfo(1, "DISPLAY1", 1920, 1080, 0, 0, 1920, 1040, 0, 0, true));
        }

        if (rawDisplays.Count == 0)
        {
            rawDisplays.Add(new DisplayInfo(1, "DISPLAY1", 1920, 1080, 0, 0, 1920, 1040, 0, 0, true));
        }

        // Sort displays by Left coordinate, then Top coordinate
        var ordered = rawDisplays.OrderBy(d => d.Left).ThenBy(d => d.Top).ToList();

        // Calculate WSLg coordinate offsets.
        // WSLg maps the virtual desktop coordinate (minLeft, minTop) to (0, 0).
        int minLeft = ordered.Min(d => d.Left);
        int minTop = ordered.Min(d => d.Top);

        var finalDisplays = new List<DisplayInfo>();
        for (int i = 0; i < ordered.Count; i++)
        {
            var d = ordered[i];
            int assignedNumber = d.DisplayNumber > 0 ? d.DisplayNumber : (i + 1);
            int wslgX = d.Left - minLeft;
            int wslgY = d.Top - minTop;

            finalDisplays.Add(d with
            {
                DisplayNumber = assignedNumber,
                WslgX = wslgX,
                WslgY = wslgY
            });
        }

        return finalDisplays.OrderBy(d => d.DisplayNumber).ToList();
    }
}
