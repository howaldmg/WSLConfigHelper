using WSLConfigHelper.Automation.DistroBases;

namespace WSLConfigHelper.Automation.DesktopEnvironments;

public class KdePlasmaDesktopEnvironment : IDesktopEnvironment
{
    public string Id => "kde-plasma";
    public string DisplayName => "KDE Plasma 6";
    public int DefaultRdpPort => 3390;
    public DesktopProtocol Protocol => DesktopProtocol.Xrdp;

    public IReadOnlyList<DesktopAppShortcut> RecommendedApps => new List<DesktopAppShortcut>
    {
        new("Dolphin", "dolphin", "File Manager"),
        new("Konsole", "konsole", "Terminal"),
        new("Kate", "kate", "Text Editor"),
        new("System Settings", "systemsettings", "Settings")
    };

    public IReadOnlyList<string> GetPackageList(PackageManagerType packageManager)
    {
        return packageManager switch
        {
            PackageManagerType.Dnf => new[]
            {
                "@kde-desktop-environment",
                "plasma-workspace-x11",
                "xrdp",
                "xorgxrdp",
                "pipewire",
                "wireplumber",
                "google-noto-sans-fonts",
                "jetbrains-mono-fonts"
            },
            PackageManagerType.Apt => new[]
            {
                "kde-standard",
                "xrdp",
                "xorgxrdp",
                "pipewire",
                "wireplumber",
                "fonts-noto"
            },
            _ => new[] { "plasma-workspace-x11", "xrdp", "xorgxrdp", "pipewire" }
        };
    }

    public async Task<DesktopProbeResult> ProbeDesktopAsync(
        string distro,
        IWslProcessRunner runner,
        CancellationToken ct = default)
    {
        var script = """
            if command -v startplasma-x11 >/dev/null 2>&1; then
                echo "INSTALLED:$(startplasma-x11 --version 2>&1 | head -n 1)"
            elif command -v kwin_x11 >/dev/null 2>&1; then
                echo "INSTALLED:$(kwin_x11 --version 2>&1 | head -n 1)"
            else
                echo "NOT_INSTALLED"
            fi
            systemctl is-active xrdp 2>&1 || true
            systemctl --user is-active pipewire 2>&1 || true
            """;
        var result = await runner.ExecuteInDistroAsync(distro, script, user: "developer", cancellationToken: ct);

        bool installed = result.StandardOutput.Contains("INSTALLED:") && !result.StandardOutput.Contains("NOT_INSTALLED");
        string? version = null;
        if (installed)
        {
            var line = result.StandardOutput.Split('\n').FirstOrDefault(l => l.StartsWith("INSTALLED:"));
            version = line?.Replace("INSTALLED:", "").Trim();
        }

        bool audio = result.StandardOutput.Contains("active");

        return new DesktopProbeResult(
            IsInstalled: installed,
            CompositorOrWm: "kwin_x11",
            Version: version,
            AudioReady: audio,
            RawOutput: result.StandardOutput);
    }

    public async Task<WslExecutionResult> ConfigureViewportServiceAsync(
        string distro,
        IWslProcessRunner runner,
        ViewportOptions options,
        CancellationToken ct = default)
    {
        var script = $"""
            # 1. Set xrdp port in /etc/xrdp/xrdp.ini (first occurrence under [Globals])
            if [ -f /etc/xrdp/xrdp.ini ]; then
                sed -i '0,/^port=/s/^port=.*/port={options.Port}/' /etc/xrdp/xrdp.ini
            fi

            # 2. Configure .xsession for KDE Plasma 6
            cat << 'EOF' > "/home/{options.User}/.xsession"
            #!/bin/bash
            export LIBGL_ALWAYS_SOFTWARE=0
            export MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA
            export GALLIUM_DRIVER=d3d12
            export XDG_CURRENT_DESKTOP=KDE
            export XDG_SESSION_DESKTOP=KDE
            export KDE_SESSION_VERSION=6
            export PULSE_SERVER=unix:/mnt/wslg/PulseServer
            exec dbus-run-session startplasma-x11
            EOF
            chown {options.User}:{options.User} "/home/{options.User}/.xsession"
            chmod +x "/home/{options.User}/.xsession"

            # 3. Ensure user .config directory exists and is owned by the user
            mkdir -p "/home/{options.User}/.config"
            chown -R {options.User}:{options.User} "/home/{options.User}/.config"

            # 4. Silence missing bluetooth hardware errors (mask systemd service and remove D-Bus auto-activator)
            systemctl --global mask obex.service dbus-org.bluez.obex.service 2>/dev/null || true
            rm -f /usr/share/dbus-1/services/org.bluez.obex.service 2>/dev/null || true
            killall -9 obexd 2>/dev/null || true

            # 5. Enable and start xrdp system service
            systemctl enable xrdp 2>/dev/null || true
            systemctl restart xrdp 2>/dev/null || true
            """;

        return await runner.ExecuteInDistroAsync(distro, script, user: "root", cancellationToken: ct);
    }
}
