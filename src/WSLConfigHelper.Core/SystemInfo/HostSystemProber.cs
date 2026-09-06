using System.Runtime.InteropServices;

namespace WSLConfigHelper.Core.SystemInfo;

public record HostHardwareMetrics(
    ulong TotalPhysicalMemoryBytes,
    ulong AvailablePhysicalMemoryBytes,
    int LogicalProcessors,
    string OsDescription,
    bool IsWindows)
{
    public double TotalPhysicalMemoryGigabytes => (double)TotalPhysicalMemoryBytes / (1024 * 1024 * 1024);
    public double AvailablePhysicalMemoryGigabytes => (double)AvailablePhysicalMemoryBytes / (1024 * 1024 * 1024);
}

public interface IHostSystemProber
{
    HostHardwareMetrics Probe();
}

public class HostSystemProber : IHostSystemProber
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public static MEMORYSTATUSEX Create()
        {
            var result = new MEMORYSTATUSEX();
            result.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            return result;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public HostHardwareMetrics Probe()
    {
        int processors = Environment.ProcessorCount;
        string osDesc = RuntimeInformation.OSDescription;
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        ulong totalPhys = 0;
        ulong availPhys = 0;

        if (isWindows)
        {
            try
            {
                var memStatus = MEMORYSTATUSEX.Create();
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    totalPhys = memStatus.ullTotalPhys;
                    availPhys = memStatus.ullAvailPhys;
                }
            }
            catch
            {
                // Fallback below
            }
        }

        if (totalPhys == 0)
        {
            var gcMem = GC.GetGCMemoryInfo();
            totalPhys = (ulong)gcMem.TotalAvailableMemoryBytes;
            availPhys = totalPhys / 2; // conservative approximation
        }

        return new HostHardwareMetrics(
            totalPhys,
            availPhys,
            processors,
            osDesc,
            isWindows
        );
    }
}
