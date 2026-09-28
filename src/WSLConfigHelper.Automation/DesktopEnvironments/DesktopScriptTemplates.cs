using System.Text;

namespace WSLConfigHelper.Automation.DesktopEnvironments;

/// <summary>
/// Provides reusable shell scripts, templates, and embedded C source shims for desktop environments.
/// </summary>
public static class DesktopScriptTemplates
{
    /// <summary>
    /// C source code for the LD_PRELOAD udev vendor/bus shim that maps Hyper-V synthetic GPU devices to NVIDIA.
    /// This allows tools like KDE's ksystemstats to read NVML hardware sensors for RTX GPUs under WSL2.
    /// </summary>
    public const string FakeGpuVendorCSource = """
        #define _GNU_SOURCE
        #include <dlfcn.h>
        #include <string.h>
        #include <stdio.h>
        #include <stdlib.h>
        #include <unistd.h>

        static char g_pci_bus[32] = "0000:01:00.0";
        static int g_pci_bus_init = 0;

        static void init_pci_bus(void) {
            if (g_pci_bus_init) return;
            g_pci_bus_init = 1;
            FILE *fp = popen("/usr/bin/nvidia-smi --query-gpu=pci.bus_id --format=csv,noheader 2>/dev/null", "r");
            if (fp) {
                char buf[64];
                if (fgets(buf, sizeof(buf), fp)) {
                    char *p = strtok(buf, "\r\n ");
                    if (p) {
                        if (strlen(p) == 16 && strncmp(p, "00000000:", 9) == 0) {
                            snprintf(g_pci_bus, sizeof(g_pci_bus), "0000:%s", p + 9);
                        } else if (strlen(p) == 12 && strncmp(p, "0000:", 5) == 0) {
                            strncpy(g_pci_bus, p, sizeof(g_pci_bus) - 1);
                        }
                    }
                }
                pclose(fp);
            }
        }

        const char *udev_device_get_sysattr_value(void *dev, const char *sysattr) {
            static const char *(*orig)(void *, const char *) = NULL;
            if (!orig) orig = (const char *(*)(void *, const char *))dlsym(RTLD_NEXT, "udev_device_get_sysattr_value");
            const char *val = orig ? orig(dev, sysattr) : NULL;
            if (sysattr && strcmp(sysattr, "vendor") == 0) {
                if (val && strcmp(val, "0x1414") == 0) {
                    return "0x10de";
                }
            }
            return val;
        }

        const char *udev_device_get_sysname(void *dev) {
            static const char *(*orig)(void *) = NULL;
            if (!orig) orig = (const char *(*)(void *))dlsym(RTLD_NEXT, "udev_device_get_sysname");
            const char *name = orig ? orig(dev) : NULL;
            if (name && strstr(name, ":00:00.0")) {
                init_pci_bus();
                return g_pci_bus;
            }
            return name;
        }
        """;

    /// <summary>
    /// Returns the base64-encoded string of <see cref="FakeGpuVendorCSource"/>.
    /// </summary>
    public static string GetFakeGpuVendorBase64()
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(FakeGpuVendorCSource));
    }

    /// <summary>
    /// Shell wrapper script for ksystemstats to inject LD_PRELOAD shim.
    /// </summary>
    public const string KSystemStatsWrapperScript = """
        #!/bin/bash
        export LD_PRELOAD=/usr/local/lib/libfake_gpu.so
        exec /usr/bin/ksystemstats.bin "$@"
        """;

    /// <summary>
    /// Returns the base64-encoded string of <see cref="KSystemStatsWrapperScript"/>.
    /// </summary>
    public static string GetKSystemStatsWrapperBase64()
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(KSystemStatsWrapperScript));
    }

    /// <summary>
    /// Builds the Xephyr startup command line with options tailored for low latency and hardware acceleration.
    /// </summary>
    public static string BuildXephyrCommandLine(string screenFlag, string windowTitle, string refreshRate)
    {
        return $"Xephyr :1 {screenFlag} -title \"{windowTitle}\" -glamor -fakescreenfps \"{refreshRate}\" -ac -br -no-host-grab";
    }
}
