using WSLConfigHelper.Automation;
using Xunit;

namespace WSLConfigHelper.Automation.Tests;

public class MockWslProcessRunner : IWslProcessRunner
{
    public string MockOutput { get; set; } = string.Empty;
    public int MockExitCode { get; set; } = 0;
    public string LastArguments { get; private set; } = string.Empty;

    public Task<WslExecutionResult> ExecuteAsync(
        string arguments,
        string? standardInput = null,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        LastArguments = arguments;
        return Task.FromResult(new WslExecutionResult(MockExitCode, MockOutput, string.Empty));
    }

    public Task<WslExecutionResult> ExecuteInDistroAsync(
        string distro,
        string bashCommand,
        string user = "root",
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        LastArguments = $"-d {distro} -u {user}";
        return Task.FromResult(new WslExecutionResult(MockExitCode, MockOutput, string.Empty));
    }
}

public class DistroParserTests
{
    [Fact]
    public async Task ParsesWslListOutputCorrectly()
    {
        var mockRunner = new MockWslProcessRunner
        {
            MockOutput = """
                  NAME                   STATE           VERSION
                * Debian                 Running         2
                  AlmaLinux-10           Stopped         2
                  Fedora-Desktop         Running         2
                """
        };

        var manager = new DistroManager(mockRunner);
        var distros = await manager.ListDistrosAsync();

        Assert.Equal(3, distros.Count);

        var debian = distros.First(d => d.Name == "Debian");
        Assert.True(debian.IsDefault);
        Assert.Equal("Running", debian.State);
        Assert.Equal(2, debian.Version);

        var fedora = distros.First(d => d.Name == "Fedora-Desktop");
        Assert.False(fedora.IsDefault);
        Assert.Equal("Running", fedora.State);
        Assert.Equal(2, fedora.Version);
    }

    [Fact]
    public async Task DetectsDistroExists()
    {
        var mockRunner = new MockWslProcessRunner
        {
            MockOutput = """
                  NAME                   STATE           VERSION
                  Fedora-Desktop         Stopped         2
                """
        };

        var manager = new DistroManager(mockRunner);
        Assert.True(await manager.DistroExistsAsync("Fedora-Desktop"));
        Assert.False(await manager.DistroExistsAsync("Ubuntu-Desktop"));
    }
}
