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
        var script = "xfwm4 --version 2>&1 || xfce4-session --version 2>&1 || true";
        var result = await runner.ExecuteInDistroAsync(distro, script, user: "developer", cancellationToken: ct);

        bool installed = result.StandardOutput.Contains("xfwm4") || result.StandardOutput.Contains("xfce4");
        string? version = null;
        if (installed)
        {
            var lines = result.StandardOutput.Split('\n');
            version = lines.FirstOrDefault(l => l.Contains("xfwm4") || l.Contains("xfce4"))?.Trim();
        }

        return new DesktopProbeResult(
            IsInstalled: installed,
            CompositorOrWm: "xfwm4",
            Version: version,
            AudioReady: true,
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

            # 2. Configure .xsession for user
            cat << 'EOF' > "/home/{options.User}/.xsession"
            export LIBGL_ALWAYS_SOFTWARE=0
            export MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA
            export GALLIUM_DRIVER=d3d12
            exec startxfce4
            EOF
            chown {options.User}:{options.User} "/home/{options.User}/.xsession"
            chmod +x "/home/{options.User}/.xsession"

            # 3. Enable and start xrdp system service
            systemctl enable xrdp 2>/dev/null || true
            systemctl restart xrdp 2>/dev/null || true
            """;

        return await runner.ExecuteInDistroAsync(distro, script, user: "root", cancellationToken: ct);
    }
}
