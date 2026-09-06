using WSLConfigHelper.Core.Models;
using WSLConfigHelper.Core.Parsing;
using WSLConfigHelper.Core.SystemInfo;

namespace WSLConfigHelper.Core.Guardrails;

public enum GuardrailSeverity
{
    Info,
    Recommendation,
    Warning,
    Error
}

public record GuardrailIssue(
    string Section,
    string Key,
    GuardrailSeverity Severity,
    string Message,
    string? SuggestedValue = null
);

public class GuardrailEngine
{
    public IReadOnlyList<GuardrailIssue> Evaluate(IniDocument doc, HostHardwareMetrics host)
    {
        var issues = new List<GuardrailIssue>();

        EvaluateMemory(doc, host, issues);
        EvaluateProcessors(doc, host, issues);
        EvaluateSwap(doc, host, issues);
        EvaluateExperimental(doc, host, issues);
        EvaluatePaths(doc, issues);

        return issues;
    }

    private void EvaluateMemory(IniDocument doc, HostHardwareMetrics host, List<GuardrailIssue> issues)
    {
        var memVal = doc.GetValue(WslKnownSettings.SectionWsl2, "memory");
        if (string.IsNullOrWhiteSpace(memVal))
        {
            var defaultRamGb = Math.Round(host.TotalPhysicalMemoryGigabytes * 0.5, 1);
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionWsl2,
                "memory",
                GuardrailSeverity.Info,
                $"Memory not explicitly set. WSL2 will default to 50% of host RAM (~{defaultRamGb} GB).",
                SuggestedValue: $"{Math.Max(4, (int)defaultRamGb)}GB"
            ));
            return;
        }

        if (!MemorySizeHelper.TryParseToBytes(memVal, out ulong memBytes))
        {
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionWsl2,
                "memory",
                GuardrailSeverity.Error,
                $"Invalid memory format '{memVal}'. Use format like '8GB' or '4096MB'."
            ));
            return;
        }

        const ulong twoGb = 2UL * 1024 * 1024 * 1024;
        if (memBytes < twoGb)
        {
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionWsl2,
                "memory",
                GuardrailSeverity.Warning,
                $"Allocating less than 2GB ({MemorySizeHelper.FormatBytes(memBytes)}) may cause system instability or OOM crashes in WSL2.",
                SuggestedValue: "4GB"
            ));
        }

        if (host.TotalPhysicalMemoryBytes > 0)
        {
            double ratio = (double)memBytes / host.TotalPhysicalMemoryBytes;
            if (ratio > 0.85)
            {
                var safeCap = (ulong)(host.TotalPhysicalMemoryBytes * 0.75);
                issues.Add(new GuardrailIssue(
                    WslKnownSettings.SectionWsl2,
                    "memory",
                    GuardrailSeverity.Warning,
                    $"Allocating {ratio:P0} of host physical RAM ({MemorySizeHelper.FormatBytes(memBytes)} of {host.TotalPhysicalMemoryGigabytes:F1} GB) risks starving Windows and causing system-wide thrashing.",
                    SuggestedValue: MemorySizeHelper.FormatBytes(safeCap)
                ));
            }
            else if (ratio < 0.20 && host.TotalPhysicalMemoryGigabytes >= 16)
            {
                issues.Add(new GuardrailIssue(
                    WslKnownSettings.SectionWsl2,
                    "memory",
                    GuardrailSeverity.Recommendation,
                    $"Allocated memory ({MemorySizeHelper.FormatBytes(memBytes)}) is only {ratio:P0} of host RAM. You have ample capacity to allocate more for development.",
                    SuggestedValue: MemorySizeHelper.FormatBytes((ulong)(host.TotalPhysicalMemoryBytes * 0.5))
                ));
            }
        }
    }

    private void EvaluateProcessors(IniDocument doc, HostHardwareMetrics host, List<GuardrailIssue> issues)
    {
        var procVal = doc.GetValue(WslKnownSettings.SectionWsl2, "processors");
        if (string.IsNullOrWhiteSpace(procVal))
        {
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionWsl2,
                "processors",
                GuardrailSeverity.Info,
                $"Processors not explicitly set. WSL2 will default to all {host.LogicalProcessors} logical cores."
            ));
            return;
        }

        if (!int.TryParse(procVal, out int cores) || cores <= 0)
        {
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionWsl2,
                "processors",
                GuardrailSeverity.Error,
                $"Invalid processor count '{procVal}'. Must be a positive integer."
            ));
            return;
        }

        if (cores > host.LogicalProcessors)
        {
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionWsl2,
                "processors",
                GuardrailSeverity.Warning,
                $"Configured processors ({cores}) exceeds total physical host logical cores ({host.LogicalProcessors}).",
                SuggestedValue: Math.Max(1, host.LogicalProcessors - 2).ToString()
            ));
        }
        else if (cores == host.LogicalProcessors && host.LogicalProcessors > 2)
        {
            var suggested = Math.Max(2, host.LogicalProcessors - 2);
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionWsl2,
                "processors",
                GuardrailSeverity.Recommendation,
                $"Assigning all {host.LogicalProcessors} cores can cause Windows host stuttering under heavy compilation. Leaving 1-2 cores for Windows is recommended.",
                SuggestedValue: suggested.ToString()
            ));
        }
    }

    private void EvaluateSwap(IniDocument doc, HostHardwareMetrics host, List<GuardrailIssue> issues)
    {
        var swapVal = doc.GetValue(WslKnownSettings.SectionWsl2, "swap");
        if (string.IsNullOrWhiteSpace(swapVal))
        {
            return;
        }

        if (!MemorySizeHelper.TryParseToBytes(swapVal, out ulong swapBytes))
        {
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionWsl2,
                "swap",
                GuardrailSeverity.Error,
                $"Invalid swap format '{swapVal}'. Use format like '4GB' or '0' to disable."
            ));
            return;
        }

        if (swapBytes == 0)
        {
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionWsl2,
                "swap",
                GuardrailSeverity.Info,
                "Swap is disabled (0). Linux processes will be immediately killed by OOM killer if memory limit is hit."
            ));
            return;
        }

        var memVal = doc.GetValue(WslKnownSettings.SectionWsl2, "memory");
        if (memVal != null && MemorySizeHelper.TryParseToBytes(memVal, out ulong memBytes))
        {
            if (swapBytes > memBytes)
            {
                issues.Add(new GuardrailIssue(
                    WslKnownSettings.SectionWsl2,
                    "swap",
                    GuardrailSeverity.Warning,
                    $"Swap size ({MemorySizeHelper.FormatBytes(swapBytes)}) is larger than VM memory ({MemorySizeHelper.FormatBytes(memBytes)}). High swap activity will severely impact disk I/O.",
                    SuggestedValue: MemorySizeHelper.FormatBytes(Math.Max(1024L * 1024 * 1024, memBytes / 4))
                ));
            }
        }
    }

    private void EvaluateExperimental(IniDocument doc, HostHardwareMetrics host, List<GuardrailIssue> issues)
    {
        var netMode = doc.GetValue(WslKnownSettings.SectionExperimental, "networkingMode");
        var isMirrored = string.Equals(netMode, "mirrored", StringComparison.OrdinalIgnoreCase);

        if (isMirrored)
        {
            var dnsTunneling = doc.GetValue(WslKnownSettings.SectionExperimental, "dnsTunneling");
            if (!string.Equals(dnsTunneling, "true", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new GuardrailIssue(
                    WslKnownSettings.SectionExperimental,
                    "dnsTunneling",
                    GuardrailSeverity.Recommendation,
                    "Mirrored networking mode pairs best with dnsTunneling=true for VPN and corporate network DNS resolution.",
                    SuggestedValue: "true"
                ));
            }

            var autoProxy = doc.GetValue(WslKnownSettings.SectionExperimental, "autoProxy");
            if (!string.Equals(autoProxy, "true", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new GuardrailIssue(
                    WslKnownSettings.SectionExperimental,
                    "autoProxy",
                    GuardrailSeverity.Recommendation,
                    "Mirrored networking mode pairs well with autoProxy=true to synchronize host proxy settings.",
                    SuggestedValue: "true"
                ));
            }
        }

        var autoMem = doc.GetValue(WslKnownSettings.SectionExperimental, "autoMemoryReclaim");
        if (string.IsNullOrWhiteSpace(autoMem) || string.Equals(autoMem, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionExperimental,
                "autoMemoryReclaim",
                GuardrailSeverity.Recommendation,
                "Enabling autoMemoryReclaim=gradual allows Windows to automatically reclaim cached page memory from WSL2.",
                SuggestedValue: "gradual"
            ));
        }

        var sparseVhd = doc.GetValue(WslKnownSettings.SectionExperimental, "sparseVhd");
        if (!string.Equals(sparseVhd, "true", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new GuardrailIssue(
                WslKnownSettings.SectionExperimental,
                "sparseVhd",
                GuardrailSeverity.Recommendation,
                "Enabling sparseVhd=true allows the virtual hard disk to automatically shrink when disk space is freed inside Linux.",
                SuggestedValue: "true"
            ));
        }
    }

    private void EvaluatePaths(IniDocument doc, List<GuardrailIssue> issues)
    {
        var swapFile = doc.GetValue(WslKnownSettings.SectionWsl2, "swapFile");
        if (!string.IsNullOrWhiteSpace(swapFile))
        {
            try
            {
                var dir = Path.GetDirectoryName(swapFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    issues.Add(new GuardrailIssue(
                        WslKnownSettings.SectionWsl2,
                        "swapFile",
                        GuardrailSeverity.Warning,
                        $"Swap directory does not exist: '{dir}'. WSL2 may fail to initialize swap."
                    ));
                }
            }
            catch (Exception ex)
            {
                issues.Add(new GuardrailIssue(
                    WslKnownSettings.SectionWsl2,
                    "swapFile",
                    GuardrailSeverity.Error,
                    $"Invalid swapFile path syntax: {ex.Message}"
                ));
            }
        }

        var kernel = doc.GetValue(WslKnownSettings.SectionWsl2, "kernel");
        if (!string.IsNullOrWhiteSpace(kernel))
        {
            if (!File.Exists(kernel))
            {
                issues.Add(new GuardrailIssue(
                    WslKnownSettings.SectionWsl2,
                    "kernel",
                    GuardrailSeverity.Error,
                    $"Custom kernel file not found: '{kernel}'. WSL2 will fail to start if the kernel is missing."
                ));
            }
        }
    }
}
