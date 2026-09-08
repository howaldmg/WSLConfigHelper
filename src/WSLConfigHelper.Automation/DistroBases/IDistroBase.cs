namespace WSLConfigHelper.Automation.DistroBases;

public enum PackageManagerType
{
    Dnf,
    Apt,
    Pacman
}

public interface IDistroBase
{
    string Id { get; }
    string DisplayName { get; }
    string DefaultWslImage { get; }
    PackageManagerType PackageManager { get; }

    Task<WslExecutionResult> ConfigureGpuAccelerationAsync(
        string distro,
        IWslProcessRunner runner,
        CancellationToken ct = default);

    Task<WslExecutionResult> EnsureUserAsync(
        string distro,
        string username,
        IWslProcessRunner runner,
        CancellationToken ct = default);

    Task<WslExecutionResult> ConfigureWslConfAsync(
        string distro,
        string defaultUser,
        IWslProcessRunner runner,
        CancellationToken ct = default);

    Task<WslExecutionResult> InstallPackagesAsync(
        string distro,
        IEnumerable<string> packages,
        IWslProcessRunner runner,
        Action<string>? onProgress = null,
        CancellationToken ct = default);
}
