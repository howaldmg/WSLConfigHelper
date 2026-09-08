using WSLConfigHelper.Automation.DesktopEnvironments;
using WSLConfigHelper.Automation.DistroBases;

namespace WSLConfigHelper.Automation.Workstations;

public record WorkstationProfile(
    string Id,
    string DisplayName,
    string DistroName,
    IDistroBase DistroBase,
    IDesktopEnvironment DesktopEnvironment,
    bool IsCuratedDefault = true
);
