using System.Text.RegularExpressions;

namespace WSLConfigHelper.Automation;

public record WslDistroInfo(string Name, string State, int Version, bool IsDefault);

public class DistroManager
{
    private readonly IWslProcessRunner _runner;

    public DistroManager(IWslProcessRunner? runner = null)
    {
        _runner = runner ?? new WslProcessRunner();
    }

    public async Task<IReadOnlyList<WslDistroInfo>> ListDistrosAsync(CancellationToken cancellationToken = default)
    {
        // Use wsl.exe -l -v
        var result = await _runner.ExecuteAsync("-l -v", cancellationToken: cancellationToken);
        var distros = new List<WslDistroInfo>();

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return distros;
        }

        // Clean null characters or UTF-16 padding if any
        var cleanOutput = result.StandardOutput.Replace("\0", "");
        var lines = cleanOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("NAME", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("---") ||
                string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            bool isDefault = trimmed.StartsWith("*");
            var content = isDefault ? trimmed.Substring(1).Trim() : trimmed;

            // Regex for: <NAME> <STATE> <VERSION>
            var parts = Regex.Split(content, @"\s{2,}");
            if (parts.Length >= 3)
            {
                var name = parts[0].Trim();
                var state = parts[1].Trim();
                var verStr = parts[2].Trim();
                int.TryParse(verStr, out int version);

                distros.Add(new WslDistroInfo(name, state, version, isDefault));
            }
        }

        return distros;
    }

    public async Task<bool> DistroExistsAsync(string distroName, CancellationToken cancellationToken = default)
    {
        var distros = await ListDistrosAsync(cancellationToken);
        return distros.Any(d => string.Equals(d.Name, distroName, StringComparison.OrdinalIgnoreCase));
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

    public Task<WslExecutionResult> TerminateDistroAsync(string distroName, CancellationToken cancellationToken = default)
    {
        return _runner.ExecuteAsync($"-t {distroName}", cancellationToken: cancellationToken);
    }

    public Task<WslExecutionResult> ShutdownAsync(CancellationToken cancellationToken = default)
    {
        return _runner.ExecuteAsync("--shutdown", cancellationToken: cancellationToken);
    }
}
