using WSLConfigHelper.Core.Models;
using WSLConfigHelper.Core.Parsing;
using WSLConfigHelper.Core.SystemInfo;

namespace WSLConfigHelper.Core.Guardrails;

public record WslPreset(
    string Id,
    string Name,
    string Description,
    Dictionary<string, Dictionary<string, string>> Settings
);

public class PresetEngine
{
    public IReadOnlyList<WslPreset> GeneratePresets(HostHardwareMetrics host)
    {
        ulong totalBytes = host.TotalPhysicalMemoryBytes;
        int totalCores = host.LogicalProcessors;

        // 1. Conservative
        var conservativeRamBytes = Math.Max(2UL * 1024 * 1024 * 1024, (ulong)(totalBytes * 0.25));
        var conservativeCores = Math.Max(1, Math.Min(2, totalCores));
        var conservative = new WslPreset(
            "conservative",
            "Resource Saver (Lightweight)",
            "Minimizes resource usage for background services or lightweight tasks. Leaves maximum host RAM/CPU for Windows apps.",
            new Dictionary<string, Dictionary<string, string>>
            {
                [WslKnownSettings.SectionWsl2] = new()
                {
                    ["memory"] = MemorySizeHelper.FormatBytes(conservativeRamBytes),
                    ["processors"] = conservativeCores.ToString(),
                    ["swap"] = "2GB",
                    ["localhostForwarding"] = "true"
                },
                [WslKnownSettings.SectionExperimental] = new()
                {
                    ["autoMemoryReclaim"] = "gradual",
                    ["sparseVhd"] = "true"
                }
            }
        );

        // 2. Balanced
        var balancedRamBytes = Math.Max(4UL * 1024 * 1024 * 1024, (ulong)(totalBytes * 0.50));
        var balancedCores = Math.Max(2, totalCores / 2);
        var balanced = new WslPreset(
            "balanced",
            "Balanced (Recommended Daily Driver)",
            "Optimal 50% split. Mirrored networking with DNS tunneling and gradual memory reclamation for seamless VPN and dev work.",
            new Dictionary<string, Dictionary<string, string>>
            {
                [WslKnownSettings.SectionWsl2] = new()
                {
                    ["memory"] = MemorySizeHelper.FormatBytes(balancedRamBytes),
                    ["processors"] = balancedCores.ToString(),
                    ["swap"] = "4GB",
                    ["localhostForwarding"] = "true",
                    ["nestedVirtualization"] = "true"
                },
                [WslKnownSettings.SectionExperimental] = new()
                {
                    ["networkingMode"] = "mirrored",
                    ["dnsTunneling"] = "true",
                    ["autoProxy"] = "true",
                    ["autoMemoryReclaim"] = "gradual",
                    ["sparseVhd"] = "true"
                }
            }
        );

        // 3. Workstation / High Performance
        var powerRamBytes = Math.Max(8UL * 1024 * 1024 * 1024, (ulong)(totalBytes * 0.75));
        var powerCores = Math.Max(2, totalCores > 2 ? totalCores - 2 : totalCores);
        var workstation = new WslPreset(
            "workstation",
            "Workstation (Maximum Performance)",
            "Allocates 75% RAM and almost all cores (leaving 2 for Windows). Ideal for heavy compiling, Docker/Kubernetes, and full dev containers.",
            new Dictionary<string, Dictionary<string, string>>
            {
                [WslKnownSettings.SectionWsl2] = new()
                {
                    ["memory"] = MemorySizeHelper.FormatBytes(powerRamBytes),
                    ["processors"] = powerCores.ToString(),
                    ["swap"] = "8GB",
                    ["localhostForwarding"] = "true",
                    ["nestedVirtualization"] = "true",
                    ["pageReporting"] = "true",
                    ["guiApplications"] = "true"
                },
                [WslKnownSettings.SectionExperimental] = new()
                {
                    ["networkingMode"] = "mirrored",
                    ["dnsTunneling"] = "true",
                    ["autoProxy"] = "true",
                    ["autoMemoryReclaim"] = "gradual",
                    ["sparseVhd"] = "true"
                }
            }
        );

        return [balanced, workstation, conservative];
    }

    public void ApplyPreset(IniDocument doc, WslPreset preset)
    {
        foreach (var (section, settings) in preset.Settings)
        {
            foreach (var (key, val) in settings)
            {
                doc.SetValue(section, key, val);
            }
        }
    }
}
