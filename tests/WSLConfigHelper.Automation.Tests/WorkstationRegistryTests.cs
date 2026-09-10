using WSLConfigHelper.Automation.DesktopEnvironments;
using WSLConfigHelper.Automation.DistroBases;
using WSLConfigHelper.Automation.Workstations;
using Xunit;

namespace WSLConfigHelper.Automation.Tests;

public class WorkstationRegistryTests
{
    [Fact]
    public void WorkstationRegistryContainsCuratedDefaultsWithUniquePorts()
    {
        var registry = new WorkstationRegistry();

        var curated = registry.GetCuratedProfiles();
        Assert.Contains(curated, p => p.Id == "fedora-kde" && p.DistroName == "Fedora-KDE" && p.RdpPort == 3390);
        Assert.Contains(curated, p => p.Id == "fedora-xfce" && p.DistroName == "Fedora-XFCE" && p.RdpPort == 3391);
        Assert.Contains(curated, p => p.Id == "ubuntu-kde" && p.DistroName == "Ubuntu-KDE" && p.RdpPort == 3392);
        Assert.Contains(curated, p => p.Id == "ubuntu-xfce" && p.DistroName == "Ubuntu-XFCE" && p.RdpPort == 3393);

        // Ensure all ports are unique
        var ports = curated.Select(p => p.RdpPort).ToList();
        Assert.Equal(ports.Distinct().Count(), ports.Count);
    }

    [Fact]
    public void GetProfileReturnsCorrectProfile()
    {
        var registry = new WorkstationRegistry();

        var profile = registry.GetProfile("fedora-kde");
        Assert.NotNull(profile);
        Assert.Equal("Fedora-KDE", profile.DistroName);
        Assert.Equal("fedora", profile.DistroBase.Id);
        Assert.Equal("kde-plasma", profile.DesktopEnvironment.Id);
        Assert.Equal(3390, profile.RdpPort);
    }

    [Fact]
    public void CreateCustomProfileRegistersAndAutoAllocatesNextAvailablePort()
    {
        var registry = new WorkstationRegistry();
        var distro = registry.GetAvailableDistroBases().First(b => b.Id == "fedora");
        var de = registry.GetAvailableDesktopEnvironments().First(d => d.Id == "xfce");

        var profile = registry.CreateCustomProfile(distro, de, "Fedora-Custom-XFCE");

        Assert.NotNull(profile);
        Assert.False(profile.IsCuratedDefault);
        Assert.Equal("Fedora-Custom-XFCE", profile.DistroName);
        Assert.Equal(3394, profile.RdpPort);
        Assert.Equal(profile, registry.GetProfile(profile.Id));
    }

    [Fact]
    public void AvailableDistroBasesAndEnvironmentsArePopulated()
    {
        var registry = new WorkstationRegistry();

        var bases = registry.GetAvailableDistroBases();
        Assert.Contains(bases, b => b.Id == "fedora" && b.PackageManager == PackageManagerType.Dnf);
        Assert.Contains(bases, b => b.Id == "ubuntu" && b.PackageManager == PackageManagerType.Apt);

        var des = registry.GetAvailableDesktopEnvironments();
        Assert.Contains(des, d => d.Id == "kde-plasma" && d.Protocol == DesktopProtocol.Xrdp);
        Assert.Contains(des, d => d.Id == "xfce" && d.Protocol == DesktopProtocol.Xrdp);
    }
}
