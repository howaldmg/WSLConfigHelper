namespace WSLConfigHelper.Automation;

/// <summary>
/// Diagnostic report summarizing DirectX 12 GPU paravirtualization (GPU-PV) status inside a WSL2 instance.
/// </summary>
/// <param name="DxgDeviceFound">True if /dev/dxg device character node exists.</param>
/// <param name="WslLibMounted">True if /usr/lib/wsl/lib host driver mount exists.</param>
/// <param name="DirectRenderingEnabled">True if OpenGL direct rendering is operational.</param>
/// <param name="OpenGLRenderer">Hardware adapter name reported by Mesa/GLX.</param>
/// <param name="OpenGLVersion">OpenGL version reported by Mesa.</param>
/// <param name="RawGlxInfoOutput">Raw output from the glxinfo diagnostic run.</param>
public record GpuDiagnosticReport(
    bool DxgDeviceFound,
    bool WslLibMounted,
    bool DirectRenderingEnabled,
    string? OpenGLRenderer,
    string? OpenGLVersion,
    string RawGlxInfoOutput
);

/// <summary>
/// Probes and validates DirectX 12 hardware acceleration inside WSL2 Linux distributions.
/// </summary>
public class GpuConfigurator
{
    private readonly IWslProcessRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuConfigurator"/> class.
    /// </summary>
    /// <param name="runner">The WSL process runner instance.</param>
    public GpuConfigurator(IWslProcessRunner? runner = null)
    {
        _runner = runner ?? new WslProcessRunner();
    }

    /// <summary>
    /// Runs OpenGL/Mesa diagnostics inside the distribution to verify GPU-PV acceleration.
    /// </summary>
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
