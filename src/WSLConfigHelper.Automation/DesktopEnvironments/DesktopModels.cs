namespace WSLConfigHelper.Automation.DesktopEnvironments;

/// <summary>
/// Supported desktop streaming and display protocols for WSL2 graphical sessions.
/// </summary>
public enum DesktopProtocol
{
    /// <summary>
    /// Native Wayland RDP server embedded in modern desktop environments (e.g. KRdp).
    /// </summary>
    NativeRdp,

    /// <summary>
    /// X11 Remote Desktop Protocol server with audio sink modules (XRDP + xorgxrdp).
    /// </summary>
    Xrdp,

    /// <summary>
    /// VNC server designed specifically for Wayland compositors.
    /// </summary>
    Wayvnc,

    /// <summary>
    /// Direct hardware-accelerated nested X11 or Wayland window presented through WSLg.
    /// </summary>
    WslgNested
}

/// <summary>
/// Represents a recommended desktop application launcher shortcut.
/// </summary>
/// <param name="Name">Display name of the application.</param>
/// <param name="Command">Terminal executable or launch command.</param>
/// <param name="Category">Application category (e.g. File Manager, Terminal, Editor).</param>
public record DesktopAppShortcut(string Name, string Command, string Category);

/// <summary>
/// Represents diagnostic results from probing an installed desktop environment inside a distro.
/// </summary>
/// <param name="IsInstalled">True if desktop environment packages are installed and detectable.</param>
/// <param name="CompositorOrWm">Name of the detected window manager or compositor.</param>
/// <param name="Version">Version string if identified.</param>
/// <param name="AudioReady">True if PipeWire or PulseAudio redirection is operational.</param>
/// <param name="RawOutput">Raw shell command output from the probe script.</param>
public record DesktopProbeResult(
    bool IsInstalled,
    string CompositorOrWm,
    string? Version,
    bool AudioReady,
    string RawOutput);

/// <summary>
/// Configuration options for configuring and launching desktop viewports.
/// </summary>
/// <param name="Width">Target horizontal resolution in pixels.</param>
/// <param name="Height">Target vertical resolution in pixels.</param>
/// <param name="Port">RDP or VNC network listener port number.</param>
/// <param name="User">Linux user account owning the desktop session.</param>
/// <param name="Fullscreen">True to run in borderless fullscreen monitor takeover mode.</param>
public record ViewportOptions(
    int Width = 1920,
    int Height = 1080,
    int Port = 3390,
    string User = "developer",
    bool Fullscreen = false);
