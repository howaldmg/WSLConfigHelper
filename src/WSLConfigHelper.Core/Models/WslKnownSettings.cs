namespace WSLConfigHelper.Core.Models;

public enum SettingType
{
    String,
    Boolean,
    Integer,
    MemorySize,
    Enum,
    FilePath
}

public record WslSettingDefinition(
    string Section,
    string Key,
    SettingType Type,
    string DisplayName,
    string Description,
    string? DefaultValue = null,
    string[]? AllowedValues = null,
    string? RecommendedValue = null,
    string? Category = null
);

public static class WslKnownSettings
{
    public const string SectionWsl2 = "wsl2";
    public const string SectionExperimental = "experimental";

    public static readonly IReadOnlyList<WslSettingDefinition> All = new List<WslSettingDefinition>
    {
        // [wsl2] Settings
        new(
            SectionWsl2,
            "memory",
            SettingType.MemorySize,
            "VM Memory Limit",
            "Maximum amount of physical memory assigned to the WSL2 virtual machine (e.g. 16GB, 4096MB). Defaults to 50% of host RAM on Windows 11.",
            DefaultValue: "50% host RAM",
            Category: "Hardware Allocation"
        ),
        new(
            SectionWsl2,
            "processors",
            SettingType.Integer,
            "Logical Processors (vCPUs)",
            "Number of logical processors assigned to the WSL2 VM. Leaving at least 1-2 cores free ensures smooth Windows host performance.",
            DefaultValue: "All host cores",
            Category: "Hardware Allocation"
        ),
        new(
            SectionWsl2,
            "swap",
            SettingType.MemorySize,
            "Swap Space Size",
            "Amount of swap space to add to the WSL2 VM (e.g. 8GB, 0 to disable). Defaults to 25% of memory size on Windows.",
            DefaultValue: "25% of memory",
            Category: "Hardware Allocation"
        ),
        new(
            SectionWsl2,
            "swapFile",
            SettingType.FilePath,
            "Swap File Location",
            "Absolute Windows path to the swap virtual hard disk (e.g. C:\\temp\\swap.vhdx).",
            DefaultValue: "%USERPROFILE%\\AppData\\Local\\Temp\\swap.vhdx",
            Category: "Storage"
        ),
        new(
            SectionWsl2,
            "localhostForwarding",
            SettingType.Boolean,
            "Localhost Forwarding",
            "Allows ports opened inside WSL2 to be bound and reached from Windows via localhost.",
            DefaultValue: "true",
            Category: "Networking"
        ),
        new(
            SectionWsl2,
            "nestedVirtualization",
            SettingType.Boolean,
            "Nested Virtualization",
            "Enables nested virtualization (KVM inside WSL2), required for running Docker VMs, Podman, or Android emulators inside Linux.",
            DefaultValue: "true",
            Category: "Advanced"
        ),
        new(
            SectionWsl2,
            "pageReporting",
            SettingType.Boolean,
            "Page Reporting (Memory Reclamation)",
            "Enables the Linux kernel to report unused memory pages back to the Windows host so host memory can be recovered.",
            DefaultValue: "true",
            Category: "Performance"
        ),
        new(
            SectionWsl2,
            "guiApplications",
            SettingType.Boolean,
            "WSLg GUI Support",
            "Enables WSLg (Windows Subsystem for Linux GUI) support for running Linux X11 and Wayland graphical desktop applications.",
            DefaultValue: "true",
            Category: "Display & GUI"
        ),
        new(
            SectionWsl2,
            "debugConsole",
            SettingType.Boolean,
            "Debug Console",
            "Enables an output console window showing dmesg kernel logs upon launching a WSL2 distribution.",
            DefaultValue: "false",
            Category: "Debugging"
        ),
        new(
            SectionWsl2,
            "safeMode",
            SettingType.Boolean,
            "Safe Mode",
            "Launches WSL2 in safe mode, disabling many non-essential drivers and integrations for troubleshooting.",
            DefaultValue: "false",
            Category: "Debugging"
        ),
        new(
            SectionWsl2,
            "kernel",
            SettingType.FilePath,
            "Custom Kernel Path",
            "Absolute Windows path to a custom Linux kernel image.",
            Category: "Advanced"
        ),
        new(
            SectionWsl2,
            "kernelCommandLine",
            SettingType.String,
            "Kernel Command Line",
            "Additional kernel boot arguments passed to the WSL2 Linux kernel.",
            Category: "Advanced"
        ),

        // [experimental] Settings
        new(
            SectionExperimental,
            "networkingMode",
            SettingType.Enum,
            "Networking Mode",
            "Networking architecture for WSL2. 'mirrored' mirrors host network interfaces, offering full IPv6, VPN compatibility, and native localhost access.",
            DefaultValue: "nat",
            AllowedValues: ["nat", "mirrored", "bridged"],
            RecommendedValue: "mirrored",
            Category: "Networking"
        ),
        new(
            SectionExperimental,
            "autoMemoryReclaim",
            SettingType.Enum,
            "Auto Memory Reclaim",
            "Automatically reclaims cached memory from the WSL VM back to Windows. 'gradual' slowly trims cache, 'dropcache' releases aggressively.",
            DefaultValue: "disabled",
            AllowedValues: ["disabled", "gradual", "dropcache"],
            RecommendedValue: "gradual",
            Category: "Performance"
        ),
        new(
            SectionExperimental,
            "sparseVhd",
            SettingType.Boolean,
            "Sparse VHD",
            "Enables automatic shrinking of the WSL2 ext4.vhdx virtual hard drive as files are deleted inside Linux.",
            DefaultValue: "false",
            RecommendedValue: "true",
            Category: "Storage"
        ),
        new(
            SectionExperimental,
            "dnsTunneling",
            SettingType.Boolean,
            "DNS Tunneling",
            "Routes DNS requests through the Windows DNS client rather than virtual NAT networking. Fixes DNS issues on corporate VPNs.",
            DefaultValue: "false",
            RecommendedValue: "true",
            Category: "Networking"
        ),
        new(
            SectionExperimental,
            "firewall",
            SettingType.Boolean,
            "Hyper-V Firewall",
            "Applies Windows Firewall rules to WSL2 network traffic.",
            DefaultValue: "false",
            Category: "Networking"
        ),
        new(
            SectionExperimental,
            "autoProxy",
            SettingType.Boolean,
            "Auto Proxy Sync",
            "Automatically propagates HTTP and HTTPS proxy settings from Windows into WSL.",
            DefaultValue: "false",
            Category: "Networking"
        ),
        new(
            SectionExperimental,
            "hostAddressLoopback",
            SettingType.Boolean,
            "Host Address Loopback",
            "Allows connections from WSL to Windows host using the host's loopback addresses (127.0.0.1).",
            DefaultValue: "false",
            Category: "Networking"
        ),
        new(
            SectionExperimental,
            "bestEffortDnsParsing",
            SettingType.Boolean,
            "Best Effort DNS Parsing",
            "Attempts to parse unknown DNS responses rather than immediately returning an error.",
            DefaultValue: "false",
            Category: "Networking"
        ),
        new(
            SectionExperimental,
            "useWindowsDnsCache",
            SettingType.Boolean,
            "Use Windows DNS Cache",
            "Leverages the host Windows DNS client cache for faster domain resolution in WSL.",
            DefaultValue: "false",
            Category: "Networking"
        )
    };

    public static WslSettingDefinition? Find(string section, string key)
    {
        return All.FirstOrDefault(s =>
            string.Equals(s.Section, section, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
