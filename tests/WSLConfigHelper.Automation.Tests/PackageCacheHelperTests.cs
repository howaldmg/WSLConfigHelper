using WSLConfigHelper.Automation;
using Xunit;

namespace WSLConfigHelper.Automation.Tests;

public class PackageCacheHelperTests : IDisposable
{
    private readonly string _tempCacheDir;
    private readonly PackageCacheHelper _helper;

    public PackageCacheHelperTests()
    {
        _tempCacheDir = Path.Combine(Path.GetTempPath(), "pkg_cache_test_" + Guid.NewGuid().ToString("N"));
        _helper = new PackageCacheHelper(_tempCacheDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempCacheDir)) Directory.Delete(_tempCacheDir, true); } catch { }
    }

    [Theory]
    [InlineData(@"C:\Users\tester\cache", "/mnt/c/Users/tester/cache")]
    [InlineData(@"D:\Data\WSL", "/mnt/d/Data/WSL")]
    [InlineData(@"c:/Users/tester/cache", "/mnt/c/Users/tester/cache")]
    public void ToWslPath_ConvertsWindowsDriveLetterCorrectly(string winPath, string expectedWsl)
    {
        var result = PackageCacheHelper.ToWslPath(winPath);
        Assert.Equal(expectedWsl, result);
    }

    [Fact]
    public void GetHostCacheDirectory_SegregatesDistroFamilies()
    {
        var fedoraDir = _helper.GetHostCacheDirectory("fedora");
        var ubuntuDir = _helper.GetHostCacheDirectory("ubuntu");

        Assert.Contains("fedora", fedoraDir);
        Assert.Contains("ubuntu", ubuntuDir);
        Assert.True(Directory.Exists(fedoraDir));
        Assert.True(Directory.Exists(ubuntuDir));
    }

    [Fact]
    public void GetDnfConfigScript_ContainsKeepcacheOption()
    {
        var script = PackageCacheHelper.GetDnfConfigScript();
        Assert.Contains("keepcache=True", script);
        Assert.Contains("/etc/dnf/dnf.conf", script);
    }

    [Fact]
    public void GetAptConfigScript_ContainsKeepDownloadedPackages()
    {
        var script = PackageCacheHelper.GetAptConfigScript();
        Assert.Contains("APT::Keep-Downloaded-Packages \"true\"", script);
        Assert.Contains("/etc/apt/apt.conf.d", script);
    }

    [Fact]
    public void BuildDnfInstallScript_GeneratesStagingAndSyncHooks()
    {
        var script = _helper.BuildDnfInstallScript("fedora", new[] { "package-a", "package-b" }, "copr-command\n");

        Assert.Contains("copr-command", script);
        Assert.Contains("/var/cache/wsl-pkg-staging", script);
        Assert.Contains("dnf install -y /var/cache/wsl-pkg-staging/*.rpm package-a package-b", script);
        Assert.Contains("find /var/cache/libdnf5", script);
    }

    [Fact]
    public void BuildAptInstallScript_GeneratesArchiveRestoreAndSyncHooks()
    {
        var script = _helper.BuildAptInstallScript("ubuntu", new[] { "pkg-x", "pkg-y" });

        Assert.Contains("/var/cache/apt/archives", script);
        Assert.Contains("DEBIAN_FRONTEND=noninteractive apt-get install -y pkg-x pkg-y", script);
        Assert.Contains("cp -u /var/cache/apt/archives/*.deb", script);
    }
}
