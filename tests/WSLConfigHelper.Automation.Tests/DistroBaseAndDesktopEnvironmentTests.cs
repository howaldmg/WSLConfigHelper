using WSLConfigHelper.Automation.DesktopEnvironments;
using WSLConfigHelper.Automation.DistroBases;
using Xunit;

namespace WSLConfigHelper.Automation.Tests;

public class DistroBaseAndDesktopEnvironmentTests
{
    [Fact]
    public async Task FedoraDistroBaseGeneratesCorrectGpuAndUserCommands()
    {
        var recorder = new ViewportRunnerRecorder();
        var fedora = new FedoraDistroBase();

        await fedora.ConfigureGpuAccelerationAsync("Fedora-Desktop", recorder);
        Assert.Equal("Fedora-Desktop", recorder.LastDistro);
        Assert.Contains("/etc/ld.so.conf.d/ld.wsl.conf", recorder.LastBashCommand);
        Assert.Contains("export GALLIUM_DRIVER=d3d12", recorder.LastBashCommand);
        Assert.Contains("dnf install -y mesa-dri-drivers mesa-vulkan-drivers glx-utils", recorder.LastBashCommand);

        await fedora.EnsureUserAsync("Fedora-Desktop", "developer", recorder);
        Assert.Contains("useradd -m -s /bin/bash \"developer\"", recorder.LastBashCommand);
        Assert.Contains("usermod -aG wheel,input \"developer\"", recorder.LastBashCommand);
        Assert.Contains("echo \"developer:developer\" | chpasswd", recorder.LastBashCommand);
        Assert.Contains("loginctl enable-linger \"developer\"", recorder.LastBashCommand);
    }

    [Fact]
    public async Task UbuntuDistroBaseGeneratesCorrectAptCommands()
    {
        var recorder = new ViewportRunnerRecorder();
        var ubuntu = new UbuntuDistroBase();

        await ubuntu.ConfigureGpuAccelerationAsync("Ubuntu-XFCE", recorder);
        Assert.Equal("Ubuntu-XFCE", recorder.LastDistro);
        Assert.Contains("/etc/ld.so.conf.d/ld.wsl.conf", recorder.LastBashCommand);
        Assert.Contains("export GALLIUM_DRIVER=d3d12", recorder.LastBashCommand);
        Assert.Contains("apt-get update -y", recorder.LastBashCommand);
        Assert.Contains("apt-get install -y mesa-va-drivers mesa-vulkan-drivers libgl1-mesa-dri mesa-utils xvfb", recorder.LastBashCommand);

        await ubuntu.EnsureUserAsync("Ubuntu-XFCE", "developer", recorder);
        Assert.Contains("usermod -aG sudo,input,ssl-cert \"developer\"", recorder.LastBashCommand);
        Assert.Contains("echo \"developer:developer\" | chpasswd", recorder.LastBashCommand);
    }

    [Fact]
    public void DesktopEnvironmentsProvideAppropriatePackageListsForPackageManagers()
    {
        var kde = new KdePlasmaDesktopEnvironment();
        var dnfPackages = kde.GetPackageList(PackageManagerType.Dnf);
        var aptPackages = kde.GetPackageList(PackageManagerType.Apt);

        Assert.Contains("@kde-desktop-environment", dnfPackages);
        Assert.Contains("plasma-workspace-x11", dnfPackages);
        Assert.Contains("xrdp", dnfPackages);
        Assert.Contains("pipewire-module-xrdp", dnfPackages);
        Assert.Contains("pulseaudio-utils", dnfPackages);
        Assert.Contains("kde-standard", aptPackages);
        Assert.Contains("xrdp", aptPackages);

        var xfce = new XfceDesktopEnvironment();
        var xfceDnf = xfce.GetPackageList(PackageManagerType.Dnf);
        var xfceApt = xfce.GetPackageList(PackageManagerType.Apt);

        Assert.Contains("@xfce-desktop-environment", xfceDnf);
        Assert.Contains("xrdp", xfceDnf);
        Assert.Contains("pipewire-module-xrdp", xfceDnf);
        Assert.Contains("pulseaudio-utils", xfceDnf);
        Assert.Contains("xfce4", xfceApt);
    }

    [Fact]
    public async Task FedoraDistroBaseEnablesCoprWhenInstallingAudioRedirectionPackage()
    {
        var recorder = new ViewportRunnerRecorder();
        var fedora = new FedoraDistroBase();

        await fedora.InstallPackagesAsync("Fedora-Desktop", new[] { "xrdp", "pipewire-module-xrdp" }, recorder);
        Assert.Contains("dnf copr enable -y infinality/pipewire-module-xrdp", recorder.LastBashCommand);
        Assert.Contains("dnf install -y xrdp pipewire-module-xrdp", recorder.LastBashCommand);
    }

    [Fact]
    public async Task KdePlasmaDesktopEnvironmentConfiguresXrdpAndXsession()
    {
        var recorder = new ViewportRunnerRecorder();
        var kde = new KdePlasmaDesktopEnvironment();
        var options = new ViewportOptions(Width: 1920, Height: 1080, Port: 3390, User: "developer");

        await kde.ConfigureViewportServiceAsync("Fedora-Desktop", recorder, options);

        Assert.Equal("Fedora-Desktop", recorder.LastDistro);
        Assert.Contains("port=3390", recorder.LastBashCommand);
        Assert.Contains("pipewire-module-xrdp", recorder.LastBashCommand);
        Assert.Contains("startplasma-x11", recorder.LastBashCommand);
        Assert.Contains("systemctl restart xrdp", recorder.LastBashCommand);
    }

    [Fact]
    public async Task XfceDesktopEnvironmentConfiguresXrdpAndXsession()
    {
        var recorder = new ViewportRunnerRecorder();
        var xfce = new XfceDesktopEnvironment();
        var options = new ViewportOptions(Width: 1920, Height: 1080, Port: 3391, User: "developer");

        await xfce.ConfigureViewportServiceAsync("Fedora-XFCE", recorder, options);

        Assert.Equal("Fedora-XFCE", recorder.LastDistro);
        Assert.Contains("port=3391", recorder.LastBashCommand);
        Assert.Contains("pipewire-module-xrdp", recorder.LastBashCommand);
        Assert.Contains("exec dbus-run-session startxfce4", recorder.LastBashCommand);
        Assert.Contains("systemctl restart xrdp", recorder.LastBashCommand);
    }
}
