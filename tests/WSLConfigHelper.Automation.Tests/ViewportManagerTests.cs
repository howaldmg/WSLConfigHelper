using WSLConfigHelper.Automation;
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
    public async Task SetupWslgViewportScriptGeneratesCorrectValidationAndSockets()
    {
        var recorder = new ViewportRunnerRecorder();
        var manager = new ViewportManager(recorder);

        var result = await manager.SetupWslgViewportScriptAsync("Fedora-Desktop", 1920, 1080);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Fedora-Desktop", recorder.LastDistro);
        Assert.Contains("start-plasma-wslg", recorder.LastBashCommand);
        Assert.Contains("export XDG_RUNTIME_DIR=\"/run/user/$(id -u)\"", recorder.LastBashCommand);
        Assert.Contains("export WAYLAND_DISPLAY=wayland-0", recorder.LastBashCommand);
        Assert.Contains("--wayland-display wayland-0 -s wayland-1", recorder.LastBashCommand);
        Assert.Contains("guiApplications=true", recorder.LastBashCommand);
    }

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
}
