using WSLConfigHelper.Automation.DistroBases;

namespace WSLConfigHelper.Automation.DesktopEnvironments;

public class XfceDesktopEnvironment : IDesktopEnvironment
{
    public string Id => "xfce";
    public string DisplayName => "XFCE 4";
    public int DefaultRdpPort => 3391;
    public DesktopProtocol Protocol => DesktopProtocol.Xrdp;

    public IReadOnlyList<DesktopAppShortcut> RecommendedApps => new List<DesktopAppShortcut>
    {
        new("Thunar", "thunar", "File Manager"),
        new("XFCE Terminal", "xfce4-terminal", "Terminal"),
        new("Mousepad", "mousepad", "Text Editor"),
        new("Settings", "xfce4-settings-manager", "Settings")
    };

    public IReadOnlyList<string> GetPackageList(PackageManagerType packageManager)
    {
        return packageManager switch
        {
            PackageManagerType.Dnf => new[]
            {
                "@xfce-desktop-environment",
                "xrdp",
                "xorgxrdp",
                "pipewire",
                "wireplumber"
            },
            PackageManagerType.Apt => new[]
            {
                "xfce4",
                "xfce4-goodies",
                "xrdp",
                "pipewire"
            },
            _ => new[] { "xfce4", "xrdp" }
        };
    }

    public async Task<DesktopProbeResult> ProbeDesktopAsync(
        string distro,
        IWslProcessRunner runner,
        CancellationToken ct = default)
    {
        var script = """
            if command -v startxfce4 >/dev/null 2>&1; then
                echo "INSTALLED:$(xfwm4 --version 2>&1 | head -n 1 || echo 'XFCE 4')"
            elif command -v xfwm4 >/dev/null 2>&1; then
                echo "INSTALLED:$(xfwm4 --version 2>&1 | head -n 1)"
            else
                echo "NOT_INSTALLED"
            fi
            systemctl is-active xrdp 2>&1 || true
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
            CompositorOrWm: "xfwm4",
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

            # 2. Configure .xsession for XFCE 4
            cat << 'EOF' > "/home/{options.User}/.xsession"
            #!/bin/bash
            export LIBGL_ALWAYS_SOFTWARE=0
            export MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA
            export GALLIUM_DRIVER=d3d12
            export XDG_CURRENT_DESKTOP=XFCE
            export XDG_SESSION_DESKTOP=xfce
            export PULSE_SERVER=unix:/mnt/wslg/PulseServer
            exec startxfce4
            EOF
            chown {options.User}:{options.User} "/home/{options.User}/.xsession"
            chmod +x "/home/{options.User}/.xsession"

            # 3. Ensure user .config directory exists and is owned by user
            mkdir -p "/home/{options.User}/.config"
            chown -R {options.User}:{options.User} "/home/{options.User}/.config"

            # 4. Enable and start xrdp system service
            systemctl enable xrdp 2>/dev/null || true
            systemctl restart xrdp 2>/dev/null || true
            """;

        return await runner.ExecuteInDistroAsync(distro, script, user: "root", cancellationToken: ct);
    }
}
