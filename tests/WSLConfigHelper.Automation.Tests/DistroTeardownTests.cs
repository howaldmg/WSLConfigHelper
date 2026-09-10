using WSLConfigHelper.Automation;
using Xunit;

namespace WSLConfigHelper.Automation.Tests;

public class DistroTeardownTests
{
    [Fact]
    public async Task UnregisterDistroAsync_TerminatesThenUnregistersDistro()
    {
        // Arrange
        var distroName = "TestDistro";
        var mockRunner = new MockWslProcessRunner();
        var distroManager = new DistroManager(mockRunner);

        // Act
        var result = await distroManager.UnregisterDistroAsync(distroName);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, mockRunner.ExecutedArguments.Count);
        Assert.Equal($"-t {distroName}", mockRunner.ExecutedArguments[0]);
        Assert.Equal($"--unregister {distroName}", mockRunner.ExecutedArguments[1]);
    }

    [Fact]
    public async Task UnregisterDistroAsync_CleansUpInstallDirectoryIfPresent()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "wslconfig_teardown_test_" + Guid.NewGuid().ToString("N"));
        var distroName = "MyTestDistro";
        var targetDistroDir = Path.Combine(tempRoot, distroName);
        Directory.CreateDirectory(targetDistroDir);
        File.WriteAllText(Path.Combine(targetDistroDir, "ext4.vhdx"), "dummy vhdx");

        var mockRunner = new MockWslProcessRunner();
        var cachedInstaller = new LocalCachedDistroInstaller(
            mockRunner,
            distroInstallRoot: tempRoot);

        var distroManager = new DistroManager(mockRunner, cachedInstaller);

        try
        {
            // Act
            var result = await distroManager.UnregisterDistroAsync(distroName);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.False(Directory.Exists(targetDistroDir), "Install directory should have been deleted");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
