using Spectre.Console;
using WSLConfigHelper.Core.SystemInfo;

namespace WSLConfigHelper.Cli.UI;

public static class ConsoleRenderer
{
    public static void RenderHeader(HostHardwareMetrics host, string configPath, bool hasUnsavedChanges, bool backupExists)
    {
        AnsiConsole.Clear();

        var rule = new Rule("[bold cyan]WSLConfigHelper[/] [grey]v1.0 (.NET 10 | GPLv3)[/]")
        {
            Justification = Justify.Left,
            Style = Style.Parse("cyan dim")
        };
        AnsiConsole.Write(rule);

        var grid = new Grid();
        grid.AddColumn(new GridColumn().PadRight(2));
        grid.AddColumn(new GridColumn().PadRight(2));
        grid.AddColumn(new GridColumn());

        var hostRam = $"{host.TotalPhysicalMemoryGigabytes:F1} GB Total ({host.AvailablePhysicalMemoryGigabytes:F1} GB Available)";
        var hostCpu = $"{host.LogicalProcessors} Logical Cores";
        var unsavedBadge = hasUnsavedChanges ? "[yellow bold]* UNSAVED IN-MEMORY EDITS *[/]" : "[green]Saved / Synchronized[/]";
        var backupBadge = backupExists ? "[cyan]Available[/]" : "[grey]None[/]";

        grid.AddRow(
            $"[grey]Host Hardware:[/] [white]{hostCpu}[/], [white]{hostRam}[/]",
            $"[grey]Config Status:[/] {unsavedBadge}",
            $"[grey]Backup (.bak):[/] {backupBadge}"
        );
        grid.AddRow(
            $"[grey]Target File:[/] [dim]{configPath}[/]",
            "",
            ""
        );

        AnsiConsole.Write(new Panel(grid)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey")
        });
        AnsiConsole.WriteLine();
    }

    public static void PressEnterToContinue()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[grey]Press [[Enter]] to return to the menu...[/] ");
        Console.ReadLine();
    }
}
