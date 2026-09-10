using WSLConfigHelper.Automation;
using Xunit;

namespace WSLConfigHelper.Automation.Tests;

public class DistroInstallerTests : IDisposable
{
    private readonly string _tempCacheDir;
    private readonly string _tempInstallDir;

    public DistroInstallerTests()
    {
        _tempCacheDir = Path.Combine(Path.GetTempPath(), "wsl_installer_test_cache_" + Guid.NewGuid().ToString("N"));
        _tempInstallDir = Path.Combine(Path.GetTempPath(), "wsl_installer_test_distros_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempCacheDir);
        Directory.CreateDirectory(_tempInstallDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempCacheDir)) Directory.Delete(_tempCacheDir, true); } catch { }
        try { if (Directory.Exists(_tempInstallDir)) Directory.Delete(_tempInstallDir, true); } catch { }
    }

    [Fact]
    public async Task StandardInstaller_ExecutesWslInstallArgs()
    {
        var runner = new MockWslProcessRunner();
        var installer = new StandardWslDistroInstaller(runner);

        var result = await installer.InstallDistroAsync("FedoraLinux-43", "Fedora-Desktop");

        Assert.True(result.Success);
        Assert.Equal("--install FedoraLinux-43 --name Fedora-Desktop --no-launch --web-download", runner.LastArguments);
    }

    [Fact]
    public async Task CachedInstaller_WhenCacheHit_ImportsWithoutWebDownload()
    {
        var runner = new MockWslProcessRunner();
        var installer = new LocalCachedDistroInstaller(
            runner: runner,
            cacheDirectory: _tempCacheDir,
            distroInstallRoot: _tempInstallDir);

        // Pre-create cached image archive
        var cachedFile = Path.Combine(_tempCacheDir, "FedoraLinux-43.tar");
        await File.WriteAllTextAsync(cachedFile, "dummy tar payload");

        var outputLines = new List<string>();
        var result = await installer.InstallDistroAsync("FedoraLinux-43", "Fedora-Desktop", onOutputLine: outputLines.Add);

        Assert.True(result.Success);
        Assert.Contains("[Cache Hit]", string.Join("\n", outputLines));
        Assert.StartsWith("--import Fedora-Desktop", runner.LastArguments);
        Assert.Contains(cachedFile, runner.LastArguments);
        Assert.DoesNotContain("--install", runner.LastArguments);
    }

    [Fact]
    public async Task CachedInstaller_WhenCacheMiss_InstallsAndExports()
    {
        var runner = new MockWslProcessRunner();
        var installer = new LocalCachedDistroInstaller(
            runner: runner,
            cacheDirectory: _tempCacheDir,
            distroInstallRoot: _tempInstallDir);

        var outputLines = new List<string>();
        var result = await installer.InstallDistroAsync("FedoraLinux-43", "Fedora-Desktop", onOutputLine: outputLines.Add);

        Assert.True(result.Success);
        Assert.Contains("[Cache Miss]", string.Join("\n", outputLines));
        Assert.Contains("[Cache Seed]", string.Join("\n", outputLines));

        // Must have executed --install first, then --export
        Assert.Equal(2, runner.ExecutedArguments.Count);
        Assert.StartsWith("--install FedoraLinux-43", runner.ExecutedArguments[0]);
        Assert.StartsWith("--export Fedora-Desktop", runner.ExecutedArguments[1]);
        Assert.Contains("FedoraLinux-43.tar", runner.ExecutedArguments[1]);
    }

    [Fact]
    public async Task DistroManager_DelegatesToInstaller()
    {
        var runner = new MockWslProcessRunner();
        var standardInstaller = new StandardWslDistroInstaller(runner);
        var manager = new DistroManager(runner, standardInstaller);

        var result = await manager.InstallDistroAsync("Ubuntu-24.04", "Ubuntu-Desktop");

        Assert.True(result.Success);
        Assert.Equal("--install Ubuntu-24.04 --name Ubuntu-Desktop --no-launch --web-download", runner.LastArguments);
    }
}
