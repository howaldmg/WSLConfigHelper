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
                "wireplumber",
                "pipewire-module-xrdp",
                "pulseaudio-utils"
            },
            PackageManagerType.Apt => new[]
            {
                "xfce4",
                "xfce4-goodies",
                "xrdp",
                "xorgxrdp",
                "pipewire",
                "wireplumber"
            },
            _ => new[] { "xfce4", "xrdp", "xorgxrdp" }
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
            # Force X11 backend for XRDP session (prevents WSLg wayland-0 socket conflict)
            unset WAYLAND_DISPLAY
            export GDK_BACKEND=x11
            export QT_QPA_PLATFORM=xcb
            export XDG_CURRENT_DESKTOP=XFCE
            export XDG_SESSION_DESKTOP=xfce

            # Ensure runtime directory exists
            export XDG_RUNTIME_DIR="/run/user/$(id -u)"

            # If runtime pulse dir is symlinked to WSLg, break symlink for dedicated PipeWire socket
            if [ -L "$XDG_RUNTIME_DIR/pulse" ]; then
                rm -f "$XDG_RUNTIME_DIR/pulse"
            fi
            mkdir -p "$XDG_RUNTIME_DIR/pulse"

            # If pipewire-module-xrdp is installed, initialize PipeWire stack with XRDP audio
            if [ -x /usr/libexec/pipewire-module-xrdp/load_pw_modules.sh ]; then
                if [ -z "$DBUS_SESSION_BUS_ADDRESS" ]; then
                    eval $(dbus-launch --sh-syntax --exit-with-session)
                fi

                # Sync activation environment for D-Bus services (prevents xfce4-notifyd Wayland errors)
                if command -v dbus-update-activation-environment >/dev/null 2>&1; then
                    dbus-update-activation-environment --systemd GDK_BACKEND=x11 QT_QPA_PLATFORM=xcb DISPLAY XAUTHORITY XDG_CURRENT_DESKTOP 2>/dev/null || true
                    dbus-update-activation-environment --systemd -u WAYLAND_DISPLAY 2>/dev/null || true
                fi
                if command -v systemctl >/dev/null 2>&1; then
                    systemctl --user unset-environment WAYLAND_DISPLAY 2>/dev/null || true
                    systemctl --user set-environment GDK_BACKEND=x11 QT_QPA_PLATFORM=xcb DISPLAY="$DISPLAY" XDG_CURRENT_DESKTOP=XFCE 2>/dev/null || true
                fi

                if ! pgrep -u "$USER" -x pipewire >/dev/null; then
                    pipewire &
                    sleep 0.5
                    wireplumber &
                    sleep 0.5
                    pipewire-pulse &
                    sleep 0.5
                fi

                export PULSE_SERVER="unix:$XDG_RUNTIME_DIR/pulse/native"
                /usr/libexec/pipewire-module-xrdp/load_pw_modules.sh &
            elif [ -S /mnt/wslg/PulseServer ]; then
                export PULSE_SERVER=unix:/mnt/wslg/PulseServer
            fi

            if [ -n "$DBUS_SESSION_BUS_ADDRESS" ]; then
                exec startxfce4
            else
                exec dbus-run-session startxfce4
            fi
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
