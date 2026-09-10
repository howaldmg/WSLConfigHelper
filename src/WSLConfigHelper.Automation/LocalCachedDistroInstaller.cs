namespace WSLConfigHelper.Automation;

public class LocalCachedDistroInstaller : IDistroInstaller
{
    private readonly IWslProcessRunner _runner;
    private readonly IDistroInstaller _fallbackInstaller;

    public string CacheDirectory { get; }
    public string DistroInstallRoot { get; }

    public LocalCachedDistroInstaller(
        IWslProcessRunner? runner = null,
        IDistroInstaller? fallbackInstaller = null,
        string? cacheDirectory = null,
        string? distroInstallRoot = null)
    {
        _runner = runner ?? new WslProcessRunner();
        _fallbackInstaller = fallbackInstaller ?? new StandardWslDistroInstaller(_runner);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        CacheDirectory = cacheDirectory ?? Path.Combine(localAppData, "WSLConfigHelper", "cache", "images");
        DistroInstallRoot = distroInstallRoot ?? Path.Combine(localAppData, "WSLConfigHelper", "distros");
    }

    public bool IsImageCached(string baseDistro)
    {
        return File.Exists(GetCachedImagePath(baseDistro));
    }

    public string GetCachedImagePath(string baseDistro)
    {
        // Support standard .tar, or existing compressed formats if present
        var tarPath = Path.Combine(CacheDirectory, $"{baseDistro}.tar");
        if (File.Exists(tarPath)) return tarPath;

        var gzPath = Path.Combine(CacheDirectory, $"{baseDistro}.tar.gz");
        if (File.Exists(gzPath)) return gzPath;

        var xzPath = Path.Combine(CacheDirectory, $"{baseDistro}.tar.xz");
        if (File.Exists(xzPath)) return xzPath;

        return tarPath;
    }

    public async Task<WslExecutionResult> InstallDistroAsync(
        string baseDistro,
        string targetName,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(CacheDirectory);
        var cachedImagePath = GetCachedImagePath(baseDistro);

        if (File.Exists(cachedImagePath))
        {
            onOutputLine?.Invoke($"[Cache Hit] Using local base image: {cachedImagePath}");

            var targetInstallDir = Path.Combine(DistroInstallRoot, targetName);
            Directory.CreateDirectory(targetInstallDir);

            var importArgs = $"--import {targetName} \"{targetInstallDir}\" \"{cachedImagePath}\" --version 2";
            return await _runner.ExecuteAsync(importArgs, onOutputLine: onOutputLine, cancellationToken: cancellationToken);
        }

        onOutputLine?.Invoke($"[Cache Miss] Base image for '{baseDistro}' not found in cache. Downloading from upstream...");
        var installResult = await _fallbackInstaller.InstallDistroAsync(baseDistro, targetName, onOutputLine, cancellationToken);

        if (installResult.Success)
        {
            onOutputLine?.Invoke($"[Cache Seed] Caching base image '{targetName}' to '{cachedImagePath}' for future test runs...");
            try
            {
                var exportArgs = $"--export {targetName} \"{cachedImagePath}\"";
                var exportResult = await _runner.ExecuteAsync(exportArgs, onOutputLine: onOutputLine, cancellationToken: cancellationToken);
                if (exportResult.Success)
                {
                    onOutputLine?.Invoke($"[Cache Seed] Base image successfully cached ({new FileInfo(cachedImagePath).Length / (1024 * 1024)} MB).");
                }
                else
                {
                    onOutputLine?.Invoke($"[Cache Seed Warning] Failed to export base image: {exportResult.StandardError}");
                }
            }
            catch (Exception ex)
            {
                onOutputLine?.Invoke($"[Cache Seed Warning] Exception while exporting image: {ex.Message}");
            }
        }

        return installResult;
    }
}
