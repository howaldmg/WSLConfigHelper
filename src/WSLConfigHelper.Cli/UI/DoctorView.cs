using Spectre.Console;
using WSLConfigHelper.Core.Guardrails;
using WSLConfigHelper.Core.Parsing;
using WSLConfigHelper.Core.SystemInfo;

namespace WSLConfigHelper.Cli.UI;

public static class DoctorView
{
    public static void Show(IniDocument doc, HostHardwareMetrics host, GuardrailEngine guardrails)
    {
        AnsiConsole.Write(new Rule("[bold cyan]Guardrail Doctor Diagnostic Audit[/]") { Justification = Justify.Left });
        AnsiConsole.WriteLine();

        var issues = guardrails.Evaluate(doc, host);

        if (issues.Count == 0)
        {
            AnsiConsole.MarkupLine("[green bold]All guardrails passed![/] Your WSL configuration aligns cleanly with your host hardware resources.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn("[bold]Severity[/]");
        table.AddColumn("[bold]Target[/]");
        table.AddColumn("[bold]Finding & Impact[/]");
        table.AddColumn("[bold]Suggested Value[/]");

        foreach (var issue in issues.OrderByDescending(i => i.Severity))
        {
            var badge = issue.Severity switch
            {
                GuardrailSeverity.Error => "[bold white on red] ERROR [/]",
                GuardrailSeverity.Warning => "[bold black on yellow] WARNING [/]",
                GuardrailSeverity.Recommendation => "[bold white on blue] RECOMMEND [/]",
                _ => "[dim] INFO [/]"
            };

            var suggested = issue.SuggestedValue != null
                ? $"[bold green]{Markup.Escape(issue.SuggestedValue)}[/]"
                : "[grey]None[/]";

            table.AddRow(
                badge,
                $"[cyan][[{issue.Section}]][/] [white]{issue.Key}[/]",
                Markup.Escape(issue.Message),
                suggested
            );
        }

        AnsiConsole.Write(table);
        ConsoleRenderer.PressEnterToContinue();
    }
}
