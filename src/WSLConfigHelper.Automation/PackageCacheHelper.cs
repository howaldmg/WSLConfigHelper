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

    public string BuildDnfInstallScript(string distroId, IEnumerable<string> packages, string? coprPrefix = null, bool noCache = false)
    {
        var wslCacheDir = GetWslCacheDirectory(distroId);
        var packageList = packages.ToList();
        var packageArgs = string.Join(" ", packageList);
        var copr = string.IsNullOrWhiteSpace(coprPrefix) ? "" : coprPrefix.TrimEnd() + "\n";

        if (noCache)
        {
            return $"""
                {copr}mkdir -p "{wslCacheDir}"

                # Install directly from online mirrors (bypassing local cache repository)
                dnf install -y --disablerepo=wsl-local {packageArgs} 2>/dev/null || dnf install -y {packageArgs}

                # Update host cache repository with newly downloaded packages
                find /var/cache/libdnf5 /var/cache/dnf -type f -name "*.rpm" 2>/dev/null | while read -r rpm; do
                    cp -u "$rpm" "{wslCacheDir}/" 2>/dev/null || true
                done

                # Index local repository on the host if createrepo_c is available
                if command -v createrepo_c >/dev/null 2>&1; then
                    createrepo_c --update "{wslCacheDir}" >/dev/null 2>&1 || true
                fi

                # Clean package cache in guest to reclaim ext4 disk space
                dnf clean packages -y 2>/dev/null || true
                """;
        }

        return $"""
            {copr}mkdir -p "{wslCacheDir}"

            # 1. Wire local file repository if not present
            if [ ! -f /etc/yum.repos.d/wsl-local.repo ]; then
                cat << 'EOF' > /etc/yum.repos.d/wsl-local.repo
            [wsl-local]
            name=WSL Local Package Cache
            baseurl=file://{wslCacheDir}
            enabled=1
            gpgcheck=0
            cost=50
            EOF
            fi

            # 2. If repodata is not yet initialized and createrepo_c is available, create index
            if [ ! -d "{wslCacheDir}/repodata" ] && command -v createrepo_c >/dev/null 2>&1; then
                createrepo_c "{wslCacheDir}" >/dev/null 2>&1 || true
            fi

            # 3. Install packages (DNF prioritizes wsl-local repo due to cost=50)
            dnf install -y {packageArgs}

            # 4. Sync newly downloaded RPMs back to host cache
            find /var/cache/libdnf5 /var/cache/dnf -type f -name "*.rpm" 2>/dev/null | while read -r rpm; do
                cp -u "$rpm" "{wslCacheDir}/" 2>/dev/null || true
            done

            # 5. Keep local repo index up to date
            if command -v createrepo_c >/dev/null 2>&1; then
                createrepo_c --update "{wslCacheDir}" >/dev/null 2>&1 || true
            fi

            # 6. Clean guest package archives to reclaim ext4 disk space
            dnf clean packages -y 2>/dev/null || true
            """;
    }

    public string BuildAptInstallScript(string distroId, IEnumerable<string> packages, bool noCache = false)
    {
        var wslCacheDir = GetWslCacheDirectory(distroId);
        var packageArgs = string.Join(" ", packages);

        if (noCache)
        {
            return $"""
                mkdir -p "{wslCacheDir}"
                DEBIAN_FRONTEND=noninteractive apt-get install -y {packageArgs}

                # Persist newly downloaded DEBs back to host cache
                if ls /var/cache/apt/archives/*.deb >/dev/null 2>&1; then
                    cp -u /var/cache/apt/archives/*.deb "{wslCacheDir}/" 2>/dev/null || true
                fi

                apt-get clean 2>/dev/null || true
                """;
        }

        return $"""
            mkdir -p "{wslCacheDir}"

            # Configure local APT repository if Packages index exists
            if [ -f "{wslCacheDir}/Packages" ] || [ -f "{wslCacheDir}/Packages.gz" ]; then
                echo "deb [trusted=yes] file:{wslCacheDir} ./" > /etc/apt/sources.list.d/wsl-local.list
                apt-get update -o Dir::Etc::sourcelist="sources.list.d/wsl-local.list" -o Dir::Etc::sourceparts="-" -o APT::Get::List-Cleanup="0" 2>/dev/null || true
            fi

            DEBIAN_FRONTEND=noninteractive apt-get install -y {packageArgs}

            # Persist newly downloaded DEBs back to host cache
            if ls /var/cache/apt/archives/*.deb >/dev/null 2>&1; then
                cp -u /var/cache/apt/archives/*.deb "{wslCacheDir}/" 2>/dev/null || true
            fi

            apt-get clean 2>/dev/null || true
            """;
    }

    public void CleanPackageCache(string? distroId = null)
    {
        try
        {
            if (distroId != null)
            {
                var dir = GetHostCacheDirectory(distroId);
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            else
            {
                if (Directory.Exists(BaseHostCacheDirectory)) Directory.Delete(BaseHostCacheDirectory, recursive: true);
            }
        }
        catch { }
    }
}
