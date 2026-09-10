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
                "pipewire-module-xrdp",
                "pulseaudio-utils",
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
            # Force X11 backend for XRDP session (prevents WSLg wayland-0 socket conflict)
            unset WAYLAND_DISPLAY
            export GDK_BACKEND=x11
            export QT_QPA_PLATFORM=xcb
            export XDG_CURRENT_DESKTOP=KDE
            export XDG_SESSION_DESKTOP=KDE
            export KDE_SESSION_VERSION=6

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

                # Sync activation environment for D-Bus services
                if command -v dbus-update-activation-environment >/dev/null 2>&1; then
                    dbus-update-activation-environment --systemd GDK_BACKEND=x11 QT_QPA_PLATFORM=xcb DISPLAY XAUTHORITY XDG_CURRENT_DESKTOP 2>/dev/null || true
                    dbus-update-activation-environment --systemd -u WAYLAND_DISPLAY 2>/dev/null || true
                fi
                if command -v systemctl >/dev/null 2>&1; then
                    systemctl --user unset-environment WAYLAND_DISPLAY 2>/dev/null || true
                    systemctl --user set-environment GDK_BACKEND=x11 QT_QPA_PLATFORM=xcb DISPLAY="$DISPLAY" XDG_CURRENT_DESKTOP=KDE 2>/dev/null || true
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
                exec startplasma-x11
            else
                exec dbus-run-session startplasma-x11
            fi
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
