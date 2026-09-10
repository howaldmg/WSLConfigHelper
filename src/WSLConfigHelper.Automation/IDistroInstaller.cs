namespace WSLConfigHelper.Automation;

public interface IDistroInstaller
{
    Task<WslExecutionResult> InstallDistroAsync(
        string baseDistro,
        string targetName,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default);
}
