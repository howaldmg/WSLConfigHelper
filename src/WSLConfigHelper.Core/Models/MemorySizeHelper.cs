using System.Text.RegularExpressions;

namespace WSLConfigHelper.Core.Models;

public static class MemorySizeHelper
{
    private static readonly Regex SizeRegex = new(@"^(\d+(?:\.\d+)?)\s*([KkMmGgTt][Bb]?)?$", RegexOptions.Compiled);

    public static bool TryParseToBytes(string? input, out ulong bytes)
    {
        bytes = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var trimmed = input.Trim();
        if (trimmed == "0")
        {
            bytes = 0;
            return true;
        }

        var match = SizeRegex.Match(trimmed);
        if (!match.Success) return false;

        if (!double.TryParse(match.Groups[1].Value, out double number) || number < 0)
        {
            return false;
        }

        string unit = match.Groups[2].Value.ToUpperInvariant();
        double multiplier = 1;

        if (unit.StartsWith("K")) multiplier = 1024L;
        else if (unit.StartsWith("M")) multiplier = 1024L * 1024L;
        else if (unit.StartsWith("G") || string.IsNullOrEmpty(unit)) multiplier = 1024L * 1024L * 1024L; // Default to GB if unit omitted? Or bytes? WSL requires unit, defaults to GB if numeric? Actually WSL requires explicit GB or MB. Let's make no unit default to GB for user convenience if number is small, or MB if > 1000.
        else if (unit.StartsWith("T")) multiplier = 1024L * 1024L * 1024L * 1024L;

        bytes = (ulong)(number * multiplier);
        return true;
    }

    public static string FormatBytes(ulong bytes)
    {
        if (bytes == 0) return "0";

        const ulong gb = 1024 * 1024 * 1024;
        const ulong mb = 1024 * 1024;

        if (bytes >= gb && bytes % gb == 0)
        {
            return $"{bytes / gb}GB";
        }
        if (bytes >= gb)
        {
            return $"{((double)bytes / gb):0.#}GB";
        }
        return $"{bytes / mb}MB";
    }
}
