using WSLConfigHelper.Automation.DistroBases;

namespace WSLConfigHelper.Automation.DesktopEnvironments;

/// <summary>
/// Contract defining lifecycle, installation, probing, and viewport scripting for a Linux desktop environment.
/// </summary>
public interface IDesktopEnvironment
{
    /// <summary>
    /// Unique identifier for the desktop environment (e.g. "kde-plasma", "xfce").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable display name (e.g. "KDE Plasma 6", "XFCE 4").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Default TCP listener port for RDP remote desktop sessions.
    /// </summary>
    int DefaultRdpPort { get; }

    /// <summary>
    /// Primary desktop streaming protocol used for remote access.
    /// </summary>
    DesktopProtocol Protocol { get; }

    /// <summary>
    /// List of recommended applications bundled or associated with this desktop.
    /// </summary>
    IReadOnlyList<DesktopAppShortcut> RecommendedApps { get; }

    /// <summary>
    /// Resolves required system packages for the specified package manager.
    /// </summary>
    IReadOnlyList<string> GetPackageList(PackageManagerType packageManager);

    /// <summary>
    /// Probes the specified distro to inspect installation and compositor status.
    /// </summary>
    Task<DesktopProbeResult> ProbeDesktopAsync(
        string distro,
        IWslProcessRunner runner,
        CancellationToken ct = default);

    /// <summary>
    /// Configures the remote display service (e.g. XRDP or KRdp) inside the distro.
    /// </summary>
    Task<WslExecutionResult> ConfigureViewportServiceAsync(
        string distro,
        IWslProcessRunner runner,
        ViewportOptions options,
        CancellationToken ct = default);
}
