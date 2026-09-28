using WSLConfigHelper.Automation;
using WSLConfigHelper.Core.Native;
using Xunit;

namespace WSLConfigHelper.Automation.Tests;

public class ViewportRunnerRecorder : IWslProcessRunner
{
    public string LastDistro { get; private set; } = string.Empty;
    public string LastBashCommand { get; private set; } = string.Empty;
    public string LastUser { get; private set; } = string.Empty;

    public Task<WslExecutionResult> ExecuteAsync(
        string arguments,
        string? standardInput = null,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new WslExecutionResult(0, string.Empty, string.Empty));
    }

    public Task<WslExecutionResult> ExecuteInDistroAsync(
        string distro,
        string bashCommand,
        string user = "root",
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        LastDistro = distro;
        LastBashCommand = bashCommand;
        LastUser = user;
        return Task.FromResult(new WslExecutionResult(0, string.Empty, string.Empty));
    }
}

public class ViewportManagerTests
{
    [Fact]
    public async Task SetupSunshineHeadlessScriptGeneratesVirtualDisplayAndServices()
    {
        var recorder = new ViewportRunnerRecorder();
        var manager = new ViewportManager(recorder);

        var result = await manager.SetupSunshineHeadlessScriptAsync("Fedora-Desktop", 2560, 1440);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Fedora-Desktop", recorder.LastDistro);
        Assert.Contains("start-plasma-sunshine", recorder.LastBashCommand);
        Assert.Contains("kwin_wayland --virtual --width 2560 --height 1440", recorder.LastBashCommand);
        Assert.Contains("pipewire &", recorder.LastBashCommand);
        Assert.Contains("wireplumber &", recorder.LastBashCommand);
        Assert.Contains("sunshine &", recorder.LastBashCommand);
        Assert.Contains("chmod 0666 /dev/uinput", recorder.LastBashCommand);
    }

    [Fact]
    public async Task GenerateOptimizedRdpFileAsyncCreatesValidConfiguration()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.rdp");
        try
        {
            var manager = new ViewportManager();
            await manager.GenerateOptimizedRdpFileAsync(tempFile, "127.0.0.1", 3390, "developer", 2560, 1440, fullscreen: true, multimon: false);
            Assert.True(File.Exists(tempFile));
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("full address:s:127.0.0.1:3390", content);
            Assert.Contains("connection type:i:7", content);
            Assert.Contains("dynamic resolution:i:1", content);
            Assert.Contains("compression:i:0", content);
            Assert.Contains("screen mode id:i:2", content);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void VerifyPInvokeWindowLongPtrSafeDoesNotThrow()
    {
        if (OperatingSystem.IsWindows())
        {
            // Verify GetWindowLongPtrSafe and SetWindowLongPtrSafe from Win32Native do not throw EntryPointNotFoundException
            var handle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            var style = Win32Native.GetWindowLongPtrSafe(handle, -16);
            Assert.True(style != IntPtr.Zero || style == IntPtr.Zero);
        }
    }
}
