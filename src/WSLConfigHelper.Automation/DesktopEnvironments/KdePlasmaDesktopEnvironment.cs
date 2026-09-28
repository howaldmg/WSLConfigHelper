using WSLConfigHelper.Automation.DistroBases;

namespace WSLConfigHelper.Automation.DesktopEnvironments;

/// <summary>
/// Implements provisioning, package management, and remote display configuration for KDE Plasma 6.
/// Supports hardware-accelerated remote desktop sessions via XRDP with DirectX GPU-PV pass-through.
/// </summary>
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
                "gcc",
                "google-noto-sans-fonts",
                "jetbrains-mono-fonts"
            },
            PackageManagerType.Apt => new[]
            {
                "kde-standard",
                "plasma-workspace-x11",
                "xrdp",
                "xorgxrdp",
                "pipewire",
                "wireplumber",
                "fonts-noto",
                "gcc"
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
                echo "INSTALLED: $(plasmashell --version 2>&1 || true)"
            elif [ -f /usr/bin/startplasma-x11 ]; then
                echo "INSTALLED: KDE Plasma (binary present)"
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
            # 1. Set xrdp port and high-performance loopback parameters in /etc/xrdp/xrdp.ini
            if [ -f /etc/xrdp/xrdp.ini ]; then
                sed -i '0,/^port=/s/^port=.*/port={options.Port}/' /etc/xrdp/xrdp.ini
                sed -i 's/^crypt_level=.*/crypt_level=none/' /etc/xrdp/xrdp.ini 2>/dev/null || true
                sed -i 's/^use_compression=.*/use_compression=no/' /etc/xrdp/xrdp.ini 2>/dev/null || true
                sed -i 's/^tcp_nodelay=.*/tcp_nodelay=true/' /etc/xrdp/xrdp.ini 2>/dev/null || true
                sed -i 's/^max_bpp=.*/max_bpp=32/' /etc/xrdp/xrdp.ini 2>/dev/null || true
            fi

            # 2. Ensure nvidia-smi and NVML libraries are discoverable in standard system paths
            if [ -f /usr/lib/wsl/lib/nvidia-smi ]; then
                ln -sf /usr/lib/wsl/lib/nvidia-smi /usr/bin/nvidia-smi 2>/dev/null || true
                ln -sf /usr/lib/wsl/lib/nvidia-smi /usr/local/bin/nvidia-smi 2>/dev/null || true
            fi
            if [ -f /usr/lib/wsl/lib/libnvidia-ml.so.1 ]; then
                ln -sf /usr/lib/wsl/lib/libnvidia-ml.so.1 /usr/lib64/libnvidia-ml.so 2>/dev/null || true
                ln -sf /usr/lib/wsl/lib/libnvidia-ml.so.1 /usr/lib64/libnvidia-ml.so.1 2>/dev/null || true
                ln -sf /usr/lib/wsl/lib/libnvidia-ml.so.1 /usr/lib/x86_64-linux-gnu/libnvidia-ml.so 2>/dev/null || true
                ln -sf /usr/lib/wsl/lib/libnvidia-ml.so.1 /usr/lib/x86_64-linux-gnu/libnvidia-ml.so.1 2>/dev/null || true
            fi
            ldconfig 2>/dev/null || true

            # 3. Compile fake GPU vendor shim so ksystemstats recognizes Hyper-V D3D12 vGPU as NVIDIA
            echo "{DesktopScriptTemplates.GetFakeGpuVendorBase64()}" | base64 -d > /tmp/fake_gpu_vendor.c
            if command -v gcc >/dev/null 2>&1; then
                mkdir -p /usr/local/lib
                gcc -shared -fPIC -o /usr/local/lib/libfake_gpu.so /tmp/fake_gpu_vendor.c -ldl 2>/dev/null || true
                chmod 755 /usr/local/lib/libfake_gpu.so 2>/dev/null || true
                rm -f /tmp/fake_gpu_vendor.c
            fi

            # 4. Wrap ksystemstats to use fake vendor shim
            if [ -f /usr/local/lib/libfake_gpu.so ]; then
                if [ -f /usr/bin/ksystemstats ] && [ ! -f /usr/bin/ksystemstats.bin ]; then
                    mv /usr/bin/ksystemstats /usr/bin/ksystemstats.bin
                fi
                echo "{DesktopScriptTemplates.GetKSystemStatsWrapperBase64()}" | base64 -d > /usr/bin/ksystemstats
                chmod 755 /usr/bin/ksystemstats
                ln -sf /usr/bin/ksystemstats /usr/local/bin/ksystemstats 2>/dev/null || true
            fi

            # 5. Configure KWin to use hardware OpenGL compositing with low latency
            mkdir -p /home/{options.User}/.config
            if command -v kwriteconfig6 >/dev/null 2>&1; then
                kwriteconfig6 --file /home/{options.User}/.config/kwinrc --group Compositing --key Backend OpenGL
                kwriteconfig6 --file /home/{options.User}/.config/kwinrc --group Compositing --key GLCore true
                kwriteconfig6 --file /home/{options.User}/.config/kwinrc --group Compositing --key Enabled true
                kwriteconfig6 --file /home/{options.User}/.config/kwinrc --group Compositing --key LatencyPolicy Low
                kwriteconfig6 --file /home/{options.User}/.config/kwinrc --group Compositing --key WindowsBlockCompositing false
                kwriteconfig6 --file /home/{options.User}/.config/kwinrc --group Plugins --key blurEnabled false
                kwriteconfig6 --file /home/{options.User}/.config/kdeglobals --group KDE --key AnimationDurationFactor 0.0
            fi

            # 6. Configure .xsession for KDE Plasma 6
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

            # 7. Ensure user .config directory exists and is owned by the user
            mkdir -p "/home/{options.User}/.config"
            chown -R {options.User}:{options.User} "/home/{options.User}/.config"

            # 8. Silence missing bluetooth hardware errors
            systemctl --global mask obex.service dbus-org.bluez.obex.service 2>/dev/null || true
            rm -f /usr/share/dbus-1/services/org.bluez.obex.service 2>/dev/null || true
            killall -9 obexd 2>/dev/null || true

            # 9. Disable Discover fwupd plugin in WSL2 virtual environments
            if [ -f /usr/lib64/qt6/plugins/discover/fwupd-backend.so ]; then
                mv -f /usr/lib64/qt6/plugins/discover/fwupd-backend.so /usr/lib64/qt6/plugins/discover/fwupd-backend.so.disabled 2>/dev/null || true
            fi
            if [ -f /usr/lib/x86_64-linux-gnu/qt6/plugins/discover/fwupd-backend.so ]; then
                mv -f /usr/lib/x86_64-linux-gnu/qt6/plugins/discover/fwupd-backend.so /usr/lib/x86_64-linux-gnu/qt6/plugins/discover/fwupd-backend.so.disabled 2>/dev/null || true
            fi

            # 10. Enable and restart xrdp system service
            systemctl enable xrdp 2>/dev/null || true
            systemctl restart xrdp 2>/dev/null || true
            """;

        return await runner.ExecuteInDistroAsync(distro, script, user: "root", cancellationToken: ct);
    }
}
