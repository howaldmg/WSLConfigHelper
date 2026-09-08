using WSLConfigHelper.Automation.DesktopEnvironments;
using WSLConfigHelper.Automation.DistroBases;

namespace WSLConfigHelper.Automation.Workstations;

public class WorkstationRegistry
{
    private readonly Dictionary<string, IDistroBase> _distroBases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IDesktopEnvironment> _desktopEnvironments = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorkstationProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public WorkstationRegistry()
    {
        // Register default distro bases
        RegisterDistroBase(new FedoraDistroBase());
        RegisterDistroBase(new UbuntuDistroBase());

        // Register default desktop environments
        RegisterDesktopEnvironment(new KdePlasmaDesktopEnvironment());
        RegisterDesktopEnvironment(new XfceDesktopEnvironment());

        // Register default curated profiles
        RegisterProfile(new WorkstationProfile(
            Id: "fedora-kde",
            DisplayName: "Fedora 43 + KDE Plasma 6 (Default)",
            DistroName: "Fedora-Desktop",
            DistroBase: _distroBases["fedora"],
            DesktopEnvironment: _desktopEnvironments["kde-plasma"],
            IsCuratedDefault: true
        ));

        RegisterProfile(new WorkstationProfile(
            Id: "fedora-xfce",
            DisplayName: "Fedora 43 + XFCE 4",
            DistroName: "Fedora-XFCE",
            DistroBase: _distroBases["fedora"],
            DesktopEnvironment: _desktopEnvironments["xfce"],
            IsCuratedDefault: true
        ));

        RegisterProfile(new WorkstationProfile(
            Id: "ubuntu-xfce",
            DisplayName: "Ubuntu 24.04 + XFCE 4",
            DistroName: "Ubuntu-XFCE",
            DistroBase: _distroBases["ubuntu"],
            DesktopEnvironment: _desktopEnvironments["xfce"],
            IsCuratedDefault: true
        ));

        RegisterProfile(new WorkstationProfile(
            Id: "ubuntu-kde",
            DisplayName: "Ubuntu 24.04 + KDE Plasma",
            DistroName: "Ubuntu-KDE",
            DistroBase: _distroBases["ubuntu"],
            DesktopEnvironment: _desktopEnvironments["kde-plasma"],
            IsCuratedDefault: true
        ));
    }

    public void RegisterDistroBase(IDistroBase distroBase)
    {
        _distroBases[distroBase.Id] = distroBase;
    }

    public void RegisterDesktopEnvironment(IDesktopEnvironment desktopEnv)
    {
        _desktopEnvironments[desktopEnv.Id] = desktopEnv;
    }

    public void RegisterProfile(WorkstationProfile profile)
    {
        _profiles[profile.Id] = profile;
    }

    public IReadOnlyCollection<IDistroBase> GetAvailableDistroBases() => _distroBases.Values;

    public IReadOnlyCollection<IDesktopEnvironment> GetAvailableDesktopEnvironments() => _desktopEnvironments.Values;

    public IReadOnlyCollection<WorkstationProfile> GetAllProfiles() => _profiles.Values;

    public IReadOnlyCollection<WorkstationProfile> GetCuratedProfiles() =>
        _profiles.Values.Where(p => p.IsCuratedDefault).ToList();

    public WorkstationProfile? GetProfile(string id)
    {
        _profiles.TryGetValue(id, out var profile);
        return profile;
    }

    public WorkstationProfile CreateCustomProfile(
        IDistroBase distroBase,
        IDesktopEnvironment desktopEnv,
        string customDistroName)
    {
        var id = $"custom-{distroBase.Id}-{desktopEnv.Id}-{customDistroName.ToLowerInvariant()}";
        var profile = new WorkstationProfile(
            Id: id,
            DisplayName: $"{distroBase.DisplayName} + {desktopEnv.DisplayName} ({customDistroName})",
            DistroName: customDistroName,
            DistroBase: distroBase,
            DesktopEnvironment: desktopEnv,
            IsCuratedDefault: false
        );

        RegisterProfile(profile);
        return profile;
    }
}
