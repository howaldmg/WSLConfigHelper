using Spectre.Console;
using WSLConfigHelper.Core.Storage;

namespace WSLConfigHelper.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        string? customConfigPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--help" || args[i] == "-h")
            {
                PrintHelp();
                return 0;
            }
            if (args[i] == "--license")
            {
                PrintLicense();
                return 0;
            }
            if ((args[i] == "--config" || args[i] == "-c") && i + 1 < args.Length)
            {
                customConfigPath = args[++i];
            }
        }

        try
        {
            var fileService = new WslConfigFileService(customConfigPath);
            var app = new AppController(fileService);
            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void PrintHelp()
    {
        AnsiConsole.MarkupLine("[bold cyan]WSLConfigHelper[/] - Interactive Spectre.Console manager for WSL configuration");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Usage:[/] [white]wslconfig-helper [[options]][/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Options:[/]");
        AnsiConsole.MarkupLine("  -c, --config <path>   Specify a custom path to .wslconfig (default: %USERPROFILE%\\.wslconfig)");
        AnsiConsole.MarkupLine("  --license             Display license information (GNU GPLv3)");
        AnsiConsole.MarkupLine("  -h, --help            Show help and usage information");
    }

    private static void PrintLicense()
    {
        AnsiConsole.MarkupLine("[bold]WSLConfigHelper[/] is licensed under the [cyan bold]GNU General Public License v3.0 (GPL-3.0)[/].");
        AnsiConsole.MarkupLine("This program comes with ABSOLUTELY NO WARRANTY. You are free to distribute and modify it under the terms of the GPLv3.");
    }
}
