namespace WSLConfigHelper.Automation.DistroBases;

public class UbuntuDistroBase : IDistroBase
{
    public string Id => "ubuntu";
    public string DisplayName => "Ubuntu 24.04 LTS";
    public string DefaultWslImage => "Ubuntu-24.04";
    public PackageManagerType PackageManager => PackageManagerType.Apt;

    public async Task<WslExecutionResult> ConfigureGpuAccelerationAsync(
        string distro,
        IWslProcessRunner runner,
        CancellationToken ct = default)
    {
        var script = """
            # 1. Wire WSL dynamic linker paths
            echo "/usr/lib/wsl/lib" > /etc/ld.so.conf.d/ld.wsl.conf
            ldconfig

            # 2. Hardware acceleration environment variables
            cat << 'EOF' > /etc/profile.d/wsl-d3d12.sh
            export LIBGL_ALWAYS_SOFTWARE=0
            export MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA
            export GALLIUM_DRIVER=d3d12
            EOF
            chmod +x /etc/profile.d/wsl-d3d12.sh

            # 3. Install Mesa drivers, OpenGL utilities, and virtual framebuffer for diagnostics
            apt-get update -y
            DEBIAN_FRONTEND=noninteractive apt-get install -y mesa-va-drivers mesa-vulkan-drivers libgl1-mesa-dri mesa-utils xvfb
            """;

        return await runner.ExecuteInDistroAsync(distro, script, user: "root", cancellationToken: ct);
    }

    public async Task<WslExecutionResult> EnsureUserAsync(
        string distro,
        string username,
        IWslProcessRunner runner,
        CancellationToken ct = default)
    {
        var script = $"""
            if ! id -u "{username}" >/dev/null 2>&1; then
                useradd -m -s /bin/bash "{username}"
            fi

            # Add to sudo, input, and ssl-cert groups (xrdp on Ubuntu requires ssl-cert membership)
            usermod -aG sudo,input,ssl-cert "{username}" 2>/dev/null || true

            # Set initial password matching username
            echo "{username}:{username}" | chpasswd

            # Passwordless sudo
            echo "{username} ALL=(ALL) NOPASSWD:ALL" > "/etc/sudoers.d/99-{username}-nopasswd"
            chmod 0440 "/etc/sudoers.d/99-{username}-nopasswd"

            # Enable lingering for systemd user services
            loginctl enable-linger "{username}" 2>/dev/null || true

            # Ensure user ~/.config is pre-created with user ownership
            mkdir -p "/home/{username}/.config"
            chown -R "{username}:{username}" "/home/{username}/.config"
            """;

        return await runner.ExecuteInDistroAsync(distro, script, user: "root", cancellationToken: ct);
    }

    public async Task<WslExecutionResult> ConfigureWslConfAsync(
        string distro,
        string defaultUser,
        IWslProcessRunner runner,
        CancellationToken ct = default)
    {
        var script = $"""
            cat << 'EOF' > /etc/wsl.conf
            [boot]
            systemd=true

            [user]
            default={defaultUser}

            [automount]
            enabled=true
            mountFsTab=true

            [interop]
            enabled=true
            appendWindowsPath=true
            EOF
            """;

        return await runner.ExecuteInDistroAsync(distro, script, user: "root", cancellationToken: ct);
    }

    public async Task<WslExecutionResult> InstallPackagesAsync(
        string distro,
        IEnumerable<string> packages,
        IWslProcessRunner runner,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        var packageArgs = string.Join(" ", packages);
        var script = $"DEBIAN_FRONTEND=noninteractive apt-get install -y {packageArgs}";
        return await runner.ExecuteInDistroAsync(distro, script, user: "root", onOutputLine: onProgress, cancellationToken: ct);
    }
}
