namespace WSLConfigHelper.Automation;

public interface IDistroInstaller
{
    Task<WslExecutionResult> InstallDistroAsync(
        string baseDistro,
        string targetName,
        Action<string>? onOutputLine = null,
        bool noCache = false,
        CancellationToken cancellationToken = default);
}
