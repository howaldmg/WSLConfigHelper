namespace WSLConfigHelper.Automation;

public record PlasmaDiagnosticReport(
    bool KWinInstalled,
    string? KWinVersion,
    bool PipeWireInstalled,
    bool SudoConfigured,
    string RawOutput
);

public class PlasmaProvisioner
{
    private readonly IWslProcessRunner _runner;

    public PlasmaProvisioner(IWslProcessRunner? runner = null)
    {
        _runner = runner ?? new WslProcessRunner();
    }

    public async Task<WslExecutionResult> EnsureUserAsync(
        string distro,
        string username = "developer",
        Action<string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var bash = $"""
            if ! id -u {username} >/dev/null 2>&1; then
                useradd -m -s /bin/bash -G wheel {username}
                # Grant passwordless sudo for wheel group in WSL development container
                echo "%wheel ALL=(ALL) NOPASSWD: ALL" > /etc/sudoers.d/99-wheel-nopasswd
                chmod 0440 /etc/sudoers.d/99-wheel-nopasswd
            fi
            """;

        return await _runner.ExecuteInDistroAsync(distro, bash, onOutputLine: onProgress, cancellationToken: cancellationToken);
    }

    public async Task<WslExecutionResult> InstallPlasmaDesktopAsync(
        string distro,
        bool fullEnvironment = true,
        Action<string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        // Full environment installs the complete KDE suite; minimal installs plasma-desktop, kwin, konsole, dolphin
        var packageTarget = fullEnvironment
            ? "@kde-desktop-environment"
            : "plasma-desktop plasma-workspace kwin-wayland konsole dolphin";

        var bash = $"""
            dnf install -y {packageTarget} pipewire wireplumber google-noto-sans-fonts jetbrains-mono-fonts dbus-x11
            """;

        return await _runner.ExecuteInDistroAsync(distro, bash, onOutputLine: onProgress, cancellationToken: cancellationToken);
    }

    public async Task<PlasmaDiagnosticReport> RunPlasmaDiagnosticsAsync(string distro, CancellationToken cancellationToken = default)
    {
        var result = await _runner.ExecuteInDistroAsync(
            distro,
            "which kwin_wayland >/dev/null 2>&1 && kwin_wayland --version 2>&1 || echo 'KWIN_NOT_FOUND'; which pipewire >/dev/null 2>&1 && echo 'PIPEWIRE_OK' || echo 'NO_PIPEWIRE'",
            cancellationToken: cancellationToken);

        var output = result.StandardOutput;
        bool kwinInstalled = !output.Contains("KWIN_NOT_FOUND");
        bool pipewireInstalled = output.Contains("PIPEWIRE_OK");

        string? kwinVersion = null;
        if (kwinInstalled)
        {
            var firstLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            kwinVersion = firstLine?.Trim();
        }

        var sudoResult = await _runner.ExecuteInDistroAsync(distro, "[ -f /etc/sudoers.d/99-wheel-nopasswd ] && echo 'SUDO_OK'", cancellationToken: cancellationToken);
        bool sudoConfigured = sudoResult.StandardOutput.Contains("SUDO_OK");

        return new PlasmaDiagnosticReport(
            KWinInstalled: kwinInstalled,
            KWinVersion: kwinVersion,
            PipeWireInstalled: pipewireInstalled,
            SudoConfigured: sudoConfigured,
            RawOutput: output
        );
    }
}
