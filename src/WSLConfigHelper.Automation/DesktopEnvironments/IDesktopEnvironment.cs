using WSLConfigHelper.Automation.DistroBases;

namespace WSLConfigHelper.Automation.DesktopEnvironments;

public interface IDesktopEnvironment
{
    string Id { get; }
    string DisplayName { get; }
    int DefaultRdpPort { get; }
    DesktopProtocol Protocol { get; }

    IReadOnlyList<DesktopAppShortcut> RecommendedApps { get; }

    IReadOnlyList<string> GetPackageList(PackageManagerType packageManager);

    Task<DesktopProbeResult> ProbeDesktopAsync(
        string distro,
        IWslProcessRunner runner,
        CancellationToken ct = default);

    string NativeWslgScriptPath { get; }

    Task<WslExecutionResult> ConfigureViewportServiceAsync(
        string distro,
        IWslProcessRunner runner,
        ViewportOptions options,
        CancellationToken ct = default);

    Task<WslExecutionResult> ConfigureNativeWslgViewportAsync(
        string distro,
        IWslProcessRunner runner,
        ViewportOptions options,
        CancellationToken ct = default);
}
