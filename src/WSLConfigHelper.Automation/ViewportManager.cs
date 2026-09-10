using System.Diagnostics;

namespace WSLConfigHelper.Automation;

public class ViewportManager
{
    private readonly IWslProcessRunner _runner;

    public ViewportManager(IWslProcessRunner? runner = null)
    {
        _runner = runner ?? new WslProcessRunner();
    }

    public async Task<WslExecutionResult> SetupWslgViewportScriptAsync(
        string distro,
        int width = 1920,
        int height = 1080,
        CancellationToken cancellationToken = default)
    {
        var script = $"""
            cat << 'EOF' > /usr/local/bin/start-plasma-wslg
            #!/bin/bash
            set -e

            # 1. Ensure user runtime directory exists
            if [ -z "$XDG_RUNTIME_DIR" ] || [ ! -d "$XDG_RUNTIME_DIR" ]; then
                export XDG_RUNTIME_DIR="/run/user/$(id -u)"
                if [ ! -d "$XDG_RUNTIME_DIR" ]; then
                    export XDG_RUNTIME_DIR="/tmp/runtime-$(id -u)"
                    mkdir -p "$XDG_RUNTIME_DIR"
                    chmod 0700 "$XDG_RUNTIME_DIR"
                fi
            fi

            # 2. Check WSLg host display socket availability
            if [ ! -S /mnt/wslg/runtime-dir/wayland-0 ] && [ ! -S /tmp/.X11-unix/X0 ] && [ ! -S "$XDG_RUNTIME_DIR/wayland-0" ]; then
                echo "================================================================"
                echo " Error: WSLg display socket not detected!"
                echo " Your .wslconfig may have 'guiApplications=false'."
                echo " To use WSLg nested window, enable 'guiApplications=true' in"
                echo " %USERPROFILE%\\.wslconfig and run 'wsl --shutdown'."
                echo " Alternatively, use Sunshine for headless virtual display."
                echo "================================================================"
                exit 1
            fi

            # 3. Hardware acceleration & rendering
            export LIBGL_ALWAYS_SOFTWARE=0
            export MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA
            export GALLIUM_DRIVER=d3d12

            # Point host connection to WSLg wayland-0 and bind nested server to wayland-1
            export WAYLAND_DISPLAY=wayland-0
            export DISPLAY=:0

            echo "Starting KDE Plasma inside WSLg nested Wayland window ({width}x{height})..."
            if [ -S "$XDG_RUNTIME_DIR/bus" ]; then
                export DBUS_SESSION_BUS_ADDRESS="unix:path=$XDG_RUNTIME_DIR/bus"
                exec kwin_wayland --wayland-display wayland-0 -s wayland-1 --width {width} --height {height} --exit-with-session plasma-workspace
            else
                exec dbus-run-session kwin_wayland --wayland-display wayland-0 -s wayland-1 --width {width} --height {height} --exit-with-session plasma-workspace
            fi
            EOF
            chmod +x /usr/local/bin/start-plasma-wslg
            """;

        return await _runner.ExecuteInDistroAsync(distro, script, cancellationToken: cancellationToken);
    }

    public Process LaunchWslgViewport(string distro, string user = "developer", string scriptPath = "/usr/local/bin/start-plasma-wslg")
    {
        var psi = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            Arguments = $"-d {distro} -u {user} -- {scriptPath}",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return Process.Start(psi)!;
    }

    public async Task<WslExecutionResult> SetupSunshineHeadlessScriptAsync(
        string distro,
        int width = 2560,
        int height = 1440,
        CancellationToken cancellationToken = default)
    {
        var script = $"""
            # 1. Install Sunshine from COPR if not installed
            if ! command -v sunshine &>/dev/null; then
                dnf copr enable -y pvermeer/sunshine 2>/dev/null || true
                dnf install -y sunshine 2>/dev/null || true
            fi

            # 2. Configure input emulation permissions
            usermod -aG input developer 2>/dev/null || true
            chmod 0666 /dev/uinput 2>/dev/null || true

            # 3. Create startup script
            cat << 'EOF' > /usr/local/bin/start-plasma-sunshine
            #!/bin/bash
            # Ensure user runtime directory
            if [ -z "$XDG_RUNTIME_DIR" ] || [ ! -d "$XDG_RUNTIME_DIR" ]; then
                export XDG_RUNTIME_DIR="/run/user/$(id -u)"
                if [ ! -d "$XDG_RUNTIME_DIR" ]; then
                    export XDG_RUNTIME_DIR="/tmp/runtime-$(id -u)"
                    mkdir -p "$XDG_RUNTIME_DIR"
                    chmod 0700 "$XDG_RUNTIME_DIR"
                fi
            fi

            # Hardware acceleration
            export LIBGL_ALWAYS_SOFTWARE=0
            export MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA
            export GALLIUM_DRIVER=d3d12

            # Start PipeWire & WirePlumber for Wayland screencasting
            pipewire &
            PIPEWIRE_PID=$!
            wireplumber &
            WIREPLUMBER_PID=$!

            echo "Starting KDE Plasma virtual headless display ({width}x{height})..."
            dbus-run-session kwin_wayland --virtual --width {width} --height {height} --exit-with-session plasma-workspace &
            KWIN_PID=$!

            sleep 2
            echo "Starting Sunshine streaming service..."
            sunshine &
            SUNSHINE_PID=$!

            trap "kill -TERM $KWIN_PID $SUNSHINE_PID $PIPEWIRE_PID $WIREPLUMBER_PID 2>/dev/null" SIGINT SIGTERM EXIT
            wait $KWIN_PID
            EOF
            chmod +x /usr/local/bin/start-plasma-sunshine
            """;

        return await _runner.ExecuteInDistroAsync(distro, script, cancellationToken: cancellationToken);
    }

    public async Task<WslExecutionResult> SetupRdpViewportScriptAsync(
        string distro,
        int width = 1920,
        int height = 1080,
        int port = 3390,
        CancellationToken cancellationToken = default)
    {
        var script = $"""
            cat << 'EOF' > /usr/local/bin/start-plasma-rdp
            #!/bin/bash
            export XDG_RUNTIME_DIR="/run/user/$(id -u)"
            export DBUS_SESSION_BUS_ADDRESS="unix:path=$XDG_RUNTIME_DIR/bus"

            export LIBGL_ALWAYS_SOFTWARE=0
            export MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA
            export GALLIUM_DRIVER=d3d12
            export KWIN_WAYLAND_NO_PERMISSION_CHECKS=1

            if [ ! -f "$HOME/krdp.crt" ] || [ ! -f "$HOME/krdp.key" ]; then
                openssl req -x509 -newkey rsa:2048 -nodes -keyout "$HOME/krdp.key" -out "$HOME/krdp.crt" -days 365 -subj "/CN=Fedora-Desktop"
            fi

            rm -f "$XDG_RUNTIME_DIR/plasma-display"*

            echo "Starting KDE Plasma virtual headless display ({width}x{height})..."
            kwin_wayland --virtual --no-lockscreen --socket plasma-display --width {width} --height {height} --exit-with-session plasma-workspace &
            KWIN_PID=$!
            sleep 2

            echo "Starting KDE native RDP server on port {port}..."
            export WAYLAND_DISPLAY=plasma-display
            export QT_QPA_PLATFORM=wayland
            /usr/bin/krdpserver --port {port} --username developer --password developer --certificate "$HOME/krdp.crt" --certificate-key "$HOME/krdp.key" &
            KRDP_PID=$!

            echo "RDP server ready on port {port}!"
            trap "kill -TERM $KWIN_PID $KRDP_PID 2>/dev/null" SIGINT SIGTERM EXIT
            wait $KWIN_PID
            EOF
            chmod +x /usr/local/bin/start-plasma-rdp
            """;

        return await _runner.ExecuteInDistroAsync(distro, script, cancellationToken: cancellationToken);
    }

    public async Task<WslExecutionResult> EnableSystemdServiceAsync(
        string distro,
        string user = "developer",
        CancellationToken cancellationToken = default)
    {
        var script = """
            loginctl enable-linger developer 2>/dev/null || true
            mkdir -p /home/developer/.config/systemd/user
            cat << 'EOF' > /home/developer/.config/systemd/user/plasma-rdp.service
            [Unit]
            Description=KDE Plasma 6 Headless RDP Session
            After=default.target pipewire.service
            Wants=pipewire.service

            [Service]
            Type=simple
            Environment=LIBGL_ALWAYS_SOFTWARE=0
            Environment=MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA
            Environment=GALLIUM_DRIVER=d3d12
            ExecStart=/usr/local/bin/start-plasma-rdp
            Restart=on-failure
            RestartSec=3

            [Install]
            WantedBy=default.target
            EOF
            chown -R developer:developer /home/developer/.config/systemd
            su - developer -c "systemctl --user daemon-reload && systemctl --user enable plasma-rdp.service && systemctl --user restart plasma-rdp.service"
            """;

        return await _runner.ExecuteInDistroAsync(distro, script, user: "root", cancellationToken: cancellationToken);
    }

    public Process LaunchRdpViewport(string distro, string user = "developer")
    {
        var psi = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            Arguments = $"-d {distro} -u {user} -- /usr/local/bin/start-plasma-rdp",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return Process.Start(psi)!;
    }

    public Process LaunchWindowsMstsc(string target = "127.0.0.1:3390")
    {
        var psi = new ProcessStartInfo
        {
            FileName = "mstsc.exe",
            Arguments = target.EndsWith(".rdp", StringComparison.OrdinalIgnoreCase) ? target : $"/v:{target}",
            UseShellExecute = true
        };

        return Process.Start(psi)!;
    }
}
