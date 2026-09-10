using System.Text.RegularExpressions;

namespace WSLConfigHelper.Automation;

public class PackageCacheHelper
{
    public string BaseHostCacheDirectory { get; }

    public PackageCacheHelper(string? baseHostCacheDirectory = null)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        BaseHostCacheDirectory = baseHostCacheDirectory ?? Path.Combine(localAppData, "WSLConfigHelper", "cache", "packages");
    }

    public string GetHostCacheDirectory(string distroId)
    {
        var subDir = distroId.ToLowerInvariant().Contains("ubuntu") ? "ubuntu" : "fedora";
        var path = Path.Combine(BaseHostCacheDirectory, subDir);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string ToWslPath(string windowsPath)
    {
        if (string.IsNullOrWhiteSpace(windowsPath))
        {
            return windowsPath;
        }

        var match = Regex.Match(windowsPath, @"^([a-zA-Z]):[\\/](.*)");
        if (match.Success)
        {
            var drive = match.Groups[1].Value.ToLowerInvariant();
            var rest = match.Groups[2].Value.Replace('\\', '/');
            return $"/mnt/{drive}/{rest}";
        }

        return windowsPath.Replace('\\', '/');
    }

    public string GetWslCacheDirectory(string distroId)
    {
        return ToWslPath(GetHostCacheDirectory(distroId));
    }

    public static string GetDnfConfigScript()
    {
        return """
            # Ensure DNF retains downloaded packages in cache
            if [ -f /etc/dnf/dnf.conf ]; then
                if ! grep -q "keepcache" /etc/dnf/dnf.conf; then
                    sed -i '/\[main\]/a keepcache=True' /etc/dnf/dnf.conf
                fi
            fi
            """;
    }

    public static string GetAptConfigScript()
    {
        return """
            # Ensure APT retains downloaded packages in cache
            rm -f /etc/apt/apt.conf.d/docker-clean 2>/dev/null || true
            mkdir -p /etc/apt/apt.conf.d
            echo 'APT::Keep-Downloaded-Packages "true";' > /etc/apt/apt.conf.d/01keep-cache
            echo 'Binary::apt::APT::Keep-Downloaded-Packages "true";' >> /etc/apt/apt.conf.d/01keep-cache
            """;
    }

    public string BuildDnfInstallScript(string distroId, IEnumerable<string> packages, string? coprPrefix = null)
    {
        var wslCacheDir = GetWslCacheDirectory(distroId);
        var packageList = packages.ToList();
        var packageArgs = string.Join(" ", packageList);
        var copr = string.IsNullOrWhiteSpace(coprPrefix) ? "" : coprPrefix.TrimEnd() + "\n";

        return $"""
            {copr}mkdir -p "{wslCacheDir}"
            mkdir -p /var/cache/wsl-pkg-staging

            # Stage cached RPM packages if available
            if ls "{wslCacheDir}"/*.rpm >/dev/null 2>&1; then
                cp -u "{wslCacheDir}"/*.rpm /var/cache/wsl-pkg-staging/ 2>/dev/null || true
            fi

            # Install: if staged packages exist, include them for local resolution
            if ls /var/cache/wsl-pkg-staging/*.rpm >/dev/null 2>&1; then
                dnf install -y /var/cache/wsl-pkg-staging/*.rpm {packageArgs}
            else
                dnf install -y {packageArgs}
            fi

            # Persist newly downloaded RPMs back to host cache
            find /var/cache/libdnf5 /var/cache/dnf -type f -name "*.rpm" 2>/dev/null | while read -r rpm; do
                cp -u "$rpm" "{wslCacheDir}/" 2>/dev/null || true
            done

            rm -rf /var/cache/wsl-pkg-staging
            """;
    }

    public string BuildAptInstallScript(string distroId, IEnumerable<string> packages)
    {
        var wslCacheDir = GetWslCacheDirectory(distroId);
        var packageArgs = string.Join(" ", packages);

        return $"""
            mkdir -p "{wslCacheDir}"
            mkdir -p /var/cache/apt/archives

            # Restore cached DEBs into apt archives
            if ls "{wslCacheDir}"/*.deb >/dev/null 2>&1; then
                cp -u "{wslCacheDir}"/*.deb /var/cache/apt/archives/ 2>/dev/null || true
            fi

            DEBIAN_FRONTEND=noninteractive apt-get install -y {packageArgs}

            # Persist newly downloaded DEBs back to host cache
            if ls /var/cache/apt/archives/*.deb >/dev/null 2>&1; then
                cp -u /var/cache/apt/archives/*.deb "{wslCacheDir}/" 2>/dev/null || true
            fi
            """;
    }
}
