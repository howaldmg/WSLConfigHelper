namespace WSLConfigHelper.Automation.DesktopEnvironments;

public enum DesktopProtocol
{
    NativeRdp,
    Xrdp,
    Wayvnc,
    WslgNested
}

public record DesktopAppShortcut(string Name, string Command, string Category);

public record DesktopProbeResult(
    bool IsInstalled,
    string CompositorOrWm,
    string? Version,
    bool AudioReady,
    string RawOutput);

public record ViewportOptions(
    int Width = 1920,
    int Height = 1080,
    int Port = 3390,
    string User = "developer");
