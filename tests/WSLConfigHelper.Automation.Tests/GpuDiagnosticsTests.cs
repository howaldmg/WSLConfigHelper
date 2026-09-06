using WSLConfigHelper.Automation;
using Xunit;

namespace WSLConfigHelper.Automation.Tests;

public class GpuDiagnosticsTests
{
    [Fact]
    public async Task ParsesGlxInfoOutputCorrectly()
    {
        var sampleGlxInfo = """
            name of display: :0
            display: :0  screen: 0
            direct rendering: Yes
            Extended renderer info (GLX_MESA_query_renderer):
                Vendor: Microsoft Corporation (0xffffffff)
                Device: D3D12 (NVIDIA GeForce RTX 4080) (0xffffffff)
                Version: 24.1.2
                Accelerated: yes
                Video memory: 16376MB
                Unified memory: no
                Preferred profile: core (0x1)
                Max core profile version: 4.6
                Max compat profile version: 4.6
                Max GLES1 profile version: 1.1
                Max GLES[23] profile version: 3.2
            OpenGL vendor string: Microsoft Corporation
            OpenGL renderer string: D3D12 (NVIDIA GeForce RTX 4080)
            OpenGL core profile version string: 4.6 (Core Profile) Mesa 24.1.2
            OpenGL core profile shading language version string: 4.60
            """;

        var mockRunner = new MockWslProcessRunner
        {
            MockOutput = sampleGlxInfo
        };

        var configurator = new GpuConfigurator(mockRunner);
        var report = await configurator.RunGpuDiagnosticsAsync("Fedora-Desktop");

        Assert.True(report.DirectRenderingEnabled);
        Assert.Equal("D3D12 (NVIDIA GeForce RTX 4080)", report.OpenGLRenderer);
        Assert.Contains("4.6", report.OpenGLVersion);
    }
}
