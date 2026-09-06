using System.Diagnostics;
using System.Text;

namespace WSLConfigHelper.Automation;

public record WslExecutionResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}

public interface IWslProcessRunner
{
    Task<WslExecutionResult> ExecuteAsync(string arguments, string? standardInput = null, Action<string>? onOutputLine = null, CancellationToken cancellationToken = default);
    Task<WslExecutionResult> ExecuteInDistroAsync(string distro, string bashCommand, string user = "root", Action<string>? onOutputLine = null, CancellationToken cancellationToken = default);
}

public class WslProcessRunner : IWslProcessRunner
{
    private readonly string _wslExecutablePath;

    public WslProcessRunner(string wslExecutablePath = "wsl.exe")
    {
        _wslExecutablePath = wslExecutablePath;
    }

    public async Task<WslExecutionResult> ExecuteAsync(
        string arguments,
        string? standardInput = null,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _wslExecutablePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput != null,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdoutBuilder.AppendLine(e.Data);
                onOutputLine?.Invoke(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderrBuilder.AppendLine(e.Data);
                onOutputLine?.Invoke($"[stderr] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (standardInput != null)
        {
            await process.StandardInput.WriteAsync(standardInput);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync(cancellationToken);

        return new WslExecutionResult(process.ExitCode, stdoutBuilder.ToString().Trim(), stderrBuilder.ToString().Trim());
    }

    public Task<WslExecutionResult> ExecuteInDistroAsync(
        string distro,
        string bashCommand,
        string user = "root",
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        // Encode command to avoid escaping issues: pass script via stdin or base64
        var encodedCommand = Convert.ToBase64String(Encoding.UTF8.GetBytes(bashCommand));
        var args = $"-d {distro} -u {user} -- bash -c \"echo '{encodedCommand}' | base64 -d | bash\"";
        return ExecuteAsync(args, standardInput: null, onOutputLine, cancellationToken);
    }
}
