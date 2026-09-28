using System.Diagnostics;
using System.Text;

namespace WSLConfigHelper.Automation;

/// <summary>
/// Manages remote display sessions, launcher generation, and Windows RDP integration for WSL2 desktop workstations.
/// </summary>
public class ViewportManager
{
    private readonly IWslProcessRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewportManager"/> class.
    /// </summary>
    /// <param name="runner">The WSL process runner instance.</param>
    public ViewportManager(IWslProcessRunner? runner = null)
    {
        _runner = runner ?? new WslProcessRunner();
    }

    /// <summary>
    /// Generates a high-performance, tuned Windows Remote Desktop (.rdp) connection file
    /// optimized for low latency, 32-bit true color, and local hypervisor loopback transport.
    /// </summary>
    /// <param name="filePath">Target filesystem path for the .rdp file.</param>
    /// <param name="hostname">Host or loopback address (e.g. 127.0.0.1).</param>
    /// <param name="port">Remote desktop TCP port.</param>
    /// <param name="username">Login username.</param>
    /// <param name="width">Desktop width in pixels (0 for auto / native).</param>
    /// <param name="height">Desktop height in pixels (0 for auto / native).</param>
    /// <param name="fullscreen">True for full screen display mode; false for windowed.</param>
    /// <param name="multimon">True to span across multiple monitors.</param>
    public async Task GenerateOptimizedRdpFileAsync(
        string filePath,
        string hostname = "127.0.0.1",
        int port = 3390,
        string username = "developer",
        int width = 0,
        int height = 0,
        bool fullscreen = true,
        bool multimon = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"full address:s:{hostname}:{port}");
        sb.AppendLine($"username:s:{username}");
        sb.AppendLine($"screen mode id:i:{(fullscreen ? 2 : 1)}");
        sb.AppendLine($"use multimon:i:{(multimon ? 1 : 0)}");

        if (width > 0 && height > 0)
        {
            sb.AppendLine($"desktopwidth:i:{width}");
            sb.AppendLine($"desktopheight:i:{height}");
        }

        // Low-latency LAN loopback profile
        sb.AppendLine("session bpp:i:32");
        sb.AppendLine("compression:i:0");
        sb.AppendLine("keyboardhook:i:2");
        sb.AppendLine("audiomode:i:0");
        sb.AppendLine("videoplaybackmode:i:1");
        sb.AppendLine("connection type:i:7");
        sb.AppendLine("networkautodetect:i:0");
        sb.AppendLine("bandwidthautodetect:i:0");
        sb.AppendLine("displayconnectionbar:i:1");
        sb.AppendLine("enableworkspacereconnect:i:0");
        sb.AppendLine("disable wallpaper:i:0");
        sb.AppendLine("allow font smoothing:i:1");
        sb.AppendLine("allow desktop composition:i:1");
        sb.AppendLine("disable full window drag:i:0");
        sb.AppendLine("disable menu anims:i:0");
        sb.AppendLine("disable themes:i:0");
        sb.AppendLine("disable cursor setting:i:0");
        sb.AppendLine("bitmapcachepersistenable:i:1");
        sb.AppendLine("audiocapturemode:i:0");
        sb.AppendLine("redirectclipboard:i:1");
        sb.AppendLine("prompt for credentials:i:0");
        sb.AppendLine("smart sizing:i:1");
        sb.AppendLine("dynamic resolution:i:1");

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    /// <summary>
    /// Synchronous wrapper for <see cref="GenerateOptimizedRdpFileAsync"/>.
    /// </summary>
    public void GenerateOptimizedRdpFile(
        string filePath,
        string hostname = "127.0.0.1",
        int port = 3390,
        string username = "developer",
        int width = 0,
        int height = 0,
        bool fullscreen = true,
        bool multimon = false)
    {
        GenerateOptimizedRdpFileAsync(filePath, hostname, port, username, width, height, fullscreen, multimon)
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Launches the Windows Remote Desktop Connection client (mstsc.exe) targeting the specified endpoint or .rdp profile.
    /// </summary>
    /// <param name="target">The target .rdp file path or ip:port string.</param>
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

    /// <summary>
    /// Configures Sunshine headless virtual streaming server inside the WSL2 distro.
    /// </summary>
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
}
