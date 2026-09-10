namespace WSLConfigHelper.Automation.DistroBases;

public class UbuntuDistroBase : IDistroBase
{
    private readonly PackageCacheHelper _cacheHelper;

    public string Id => "ubuntu";
    public string DisplayName => "Ubuntu 24.04 LTS";
    public string DefaultWslImage => "Ubuntu-24.04";
    public PackageManagerType PackageManager => PackageManagerType.Apt;

    public UbuntuDistroBase(PackageCacheHelper? cacheHelper = null)
    {
        _cacheHelper = cacheHelper ?? new PackageCacheHelper();
    }

    public async Task<WslExecutionResult> ConfigureGpuAccelerationAsync(
        string distro,
        IWslProcessRunner runner,
        CancellationToken ct = default)
    {
        var aptConf = PackageCacheHelper.GetAptConfigScript();
        var mesaPackages = new[] { "mesa-va-drivers", "mesa-vulkan-drivers", "libgl1-mesa-dri", "mesa-utils", "xvfb" };
        var mesaInstall = _cacheHelper.BuildAptInstallScript(Id, mesaPackages);

        var script = $"""
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

            cat << 'EOF' > /etc/environment
            LIBGL_ALWAYS_SOFTWARE=0
            MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA
            GALLIUM_DRIVER=d3d12
            EOF

            # 3. Configure APT cache persistence
            {aptConf}

            # 4. Install Mesa drivers, OpenGL utilities, and virtual framebuffer with package caching
            apt-get update -y
            {mesaInstall}
            """;

        return await runner.ExecuteInDistroAsync(distro, script, user: "root", cancellationToken: ct);
    }

    public async Task<WslExecutionResult> EnsureUserAsync(
        string distro,
        string username,
        IWslProcessRunner runner,
        CancellationToken ct = default)
    {
        var script = $$"""
            if ! id -u "{{username}}" >/dev/null 2>&1; then
                useradd -m -s /bin/bash "{{username}}"
            fi

            # Add to sudo, input, and ssl-cert groups (xrdp on Ubuntu requires ssl-cert membership)
            usermod -aG sudo,input,ssl-cert "{{username}}" 2>/dev/null || true

            # Set initial password matching username
            echo "{{username}}:{{username}}" | chpasswd

            # Passwordless sudo
            echo "{{username}} ALL=(ALL) NOPASSWD:ALL" > "/etc/sudoers.d/99-{{username}}-nopasswd"
            chmod 0440 "/etc/sudoers.d/99-{{username}}-nopasswd"

            # Enable lingering for systemd user services
            loginctl enable-linger "{{username}}" 2>/dev/null || true

            # Polkit passwordless authorization for GUI package management and system actions
            mkdir -p /etc/polkit-1/rules.d
            cat << 'EOF' > "/etc/polkit-1/rules.d/49-{{username}}-nopasswd.rules"
            polkit.addRule(function(action, subject) {
                if (subject.isInGroup("wheel") || subject.isInGroup("sudo") || subject.user === "{{username}}") {
                    return polkit.Result.YES;
                }
            });
            EOF
            chmod 0644 "/etc/polkit-1/rules.d/49-{{username}}-nopasswd.rules"

            # In WSL2 virtual containers, fwupd cannot interact with bare-metal firmware/UEFI
            # Mask the service and remove the D-Bus activation entry
            systemctl mask fwupd.service 2>/dev/null || true
            rm -f /usr/share/dbus-1/system-services/org.freedesktop.fwupd.service 2>/dev/null || true

            # Ensure user ~/.config is pre-created with user ownership
            mkdir -p "/home/{{username}}/.config"
            chown -R "{{username}}:{{username}}" "/home/{{username}}/.config"

            # Configure Google Chrome / Chromium hardware acceleration flags for WSL2 D3D12/WebGL
            if [ -f /usr/share/applications/google-chrome.desktop ]; then
                sed -i 's|^Exec=/usr/bin/google-chrome-stable .*|Exec=/usr/bin/google-chrome-stable --use-gl=angle --use-angle=gl --enable-gpu-rasterization --ignore-gpu-blocklist %U|' /usr/share/applications/google-chrome.desktop 2>/dev/null || true
            fi
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
        var script = _cacheHelper.BuildAptInstallScript(Id, packages);
        return await runner.ExecuteInDistroAsync(distro, script, user: "root", onOutputLine: onProgress, cancellationToken: ct);
    }
}
