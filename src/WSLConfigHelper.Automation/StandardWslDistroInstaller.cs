namespace WSLConfigHelper.Automation;

public class StandardWslDistroInstaller : IDistroInstaller
{
    private readonly IWslProcessRunner _runner;

    public StandardWslDistroInstaller(IWslProcessRunner? runner = null)
    {
        _runner = runner ?? new WslProcessRunner();
    }

    public Task<WslExecutionResult> InstallDistroAsync(
        string baseDistro,
        string targetName,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var args = $"--install {baseDistro} --name {targetName} --no-launch --web-download";
        return _runner.ExecuteAsync(args, onOutputLine: onOutputLine, cancellationToken: cancellationToken);
    }
}
