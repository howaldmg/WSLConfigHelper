namespace WSLConfigHelper.Automation;

public record GpuDiagnosticReport(
    bool DxgDeviceFound,
    bool WslLibMounted,
    bool DirectRenderingEnabled,
    string? OpenGLRenderer,
    string? OpenGLVersion,
    string RawGlxInfoOutput
);

public class GpuConfigurator
{
    private readonly IWslProcessRunner _runner;

    public GpuConfigurator(IWslProcessRunner? runner = null)
    {
        _runner = runner ?? new WslProcessRunner();
    }

    public async Task<bool> CheckDxgAvailableAsync(string distro, CancellationToken cancellationToken = default)
    {
        var result = await _runner.ExecuteInDistroAsync(distro, "[ -c /dev/dxg ] && echo 'OK'", cancellationToken: cancellationToken);
        return result.StandardOutput.Contains("OK");
    }

    public async Task<WslExecutionResult> ConfigureWslConfAsync(string distro, string? defaultUser = null, CancellationToken cancellationToken = default)
    {
        var wslConfContent = """
            [boot]
            systemd=true

            [automount]
            enabled=true
            mountFsTab=true

            [interop]
            enabled=true
            appendWindowsPath=true
            """;

        if (!string.IsNullOrWhiteSpace(defaultUser))
        {
            wslConfContent += $"\n\n[user]\ndefault={defaultUser}\n";
        }

        var bash = $"""
            cat << 'EOF' > /etc/wsl.conf
            {wslConfContent}
            EOF
            """;

        return await _runner.ExecuteInDistroAsync(distro, bash, cancellationToken: cancellationToken);
    }

    public async Task<WslExecutionResult> ConfigureGpuEnvironmentAsync(string distro, Action<string>? onProgress = null, CancellationToken cancellationToken = default)
    {
        var bash = """
            # 1. Register WSL GPU libraries with the dynamic linker
            mkdir -p /etc/ld.so.conf.d
            echo "/usr/lib/wsl/lib" > /etc/ld.so.conf.d/ld.wsl.conf
            ldconfig 2>/dev/null || true

            # 2. Add Mesa D3D12 environment variables to global profile
            cat << 'EOF' > /etc/profile.d/wsl-gpu.sh
            export MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA
            export LIBGL_ALWAYS_SOFTWARE=0
            export GALLIUM_DRIVER=d3d12
            EOF
            chmod +x /etc/profile.d/wsl-gpu.sh

            # 3. Add to /etc/environment for systemd sessions
            touch /etc/environment
            grep -q "MESA_D3D12_DEFAULT_ADAPTER_NAME" /etc/environment || echo "MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA" >> /etc/environment
            grep -q "LIBGL_ALWAYS_SOFTWARE" /etc/environment || echo "LIBGL_ALWAYS_SOFTWARE=0" >> /etc/environment
            grep -q "GALLIUM_DRIVER" /etc/environment || echo "GALLIUM_DRIVER=d3d12" >> /etc/environment
            """;

        return await _runner.ExecuteInDistroAsync(distro, bash, onOutputLine: onProgress, cancellationToken: cancellationToken);
    }

    public async Task<WslExecutionResult> InstallMesaDriversAsync(string distro, Action<string>? onProgress = null, CancellationToken cancellationToken = default)
    {
        var bash = "dnf install -y mesa-dri-drivers mesa-vulkan-drivers glx-utils xorg-x11-server-Xvfb gawk";
        return await _runner.ExecuteInDistroAsync(distro, bash, onOutputLine: onProgress, cancellationToken: cancellationToken);
    }

    public async Task<GpuDiagnosticReport> RunGpuDiagnosticsAsync(string distro, CancellationToken cancellationToken = default)
    {
        var dxgResult = await _runner.ExecuteInDistroAsync(distro, "[ -c /dev/dxg ] && echo 'YES' || echo 'NO'", cancellationToken: cancellationToken);
        bool dxgFound = dxgResult.StandardOutput.Contains("YES");

        var libResult = await _runner.ExecuteInDistroAsync(distro, "[ -d /usr/lib/wsl/lib ] && echo 'YES' || echo 'NO'", cancellationToken: cancellationToken);
        bool libFound = libResult.StandardOutput.Contains("YES");

        var glxResult = await _runner.ExecuteInDistroAsync(distro, "source /etc/profile.d/wsl-gpu.sh 2>/dev/null; GALLIUM_DRIVER=d3d12 MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA xvfb-run -a glxinfo -B 2>&1", cancellationToken: cancellationToken);
        var glxOutput = glxResult.StandardOutput;

        bool directRendering = glxOutput.Contains("direct rendering: Yes", StringComparison.OrdinalIgnoreCase);
        string? renderer = null;
        string? version = null;

        foreach (var line in glxOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains("OpenGL renderer string:", StringComparison.OrdinalIgnoreCase))
            {
                renderer = line.Split(':', 2)[1].Trim();
            }
            else if (line.Contains("version string:", StringComparison.OrdinalIgnoreCase) && version == null)
            {
                version = line.Split(':', 2)[1].Trim();
            }
        }

        return new GpuDiagnosticReport(
            DxgDeviceFound: dxgFound,
            WslLibMounted: libFound,
            DirectRenderingEnabled: directRendering,
            OpenGLRenderer: renderer,
            OpenGLVersion: version,
            RawGlxInfoOutput: glxOutput
        );
    }
}
