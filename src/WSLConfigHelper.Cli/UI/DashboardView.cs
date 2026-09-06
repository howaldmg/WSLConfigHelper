using Spectre.Console;
using WSLConfigHelper.Core.Guardrails;
using WSLConfigHelper.Core.Models;
using WSLConfigHelper.Core.Parsing;
using WSLConfigHelper.Core.SystemInfo;

namespace WSLConfigHelper.Cli.UI;

public static class DashboardView
{
    public static void Show(IniDocument doc, HostHardwareMetrics host, GuardrailEngine guardrails)
    {
        var issues = guardrails.Evaluate(doc, host);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn("[bold]Section[/]");
        table.AddColumn("[bold]Key[/]");
        table.AddColumn("[bold]Configured Value[/]");
        table.AddColumn("[bold]Default / Notes[/]");
        table.AddColumn("[bold]Guardrail Status[/]");

        // Group by sections known + any extra
        var sections = doc.GetSections().ToList();
        if (!sections.Contains(WslKnownSettings.SectionWsl2, StringComparer.OrdinalIgnoreCase))
            sections.Add(WslKnownSettings.SectionWsl2);
        if (!sections.Contains(WslKnownSettings.SectionExperimental, StringComparer.OrdinalIgnoreCase))
            sections.Add(WslKnownSettings.SectionExperimental);

        foreach (var section in sections)
        {
            var entries = doc.GetSectionEntries(section);
            var knownForSection = WslKnownSettings.All.Where(k => string.Equals(k.Section, section, StringComparison.OrdinalIgnoreCase)).ToList();

            // Display known settings for this section
            foreach (var setting in knownForSection)
            {
                var isConfigured = entries.TryGetValue(setting.Key, out var currentVal);
                var displayVal = isConfigured ? $"[white bold]{Markup.Escape(currentVal!)}[/]" : "[grey]Not set (default)[/]";
                var defaultNote = setting.DefaultValue != null ? $"[grey]{setting.DefaultValue}[/]" : "[grey]-[/]";

                var issue = issues.FirstOrDefault(i =>
                    string.Equals(i.Section, section, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(i.Key, setting.Key, StringComparison.OrdinalIgnoreCase));

                string status;
                if (issue != null)
                {
                    status = issue.Severity switch
                    {
                        GuardrailSeverity.Error => "[red bold]FAIL[/]",
                        GuardrailSeverity.Warning => "[yellow bold]WARN[/]",
                        GuardrailSeverity.Recommendation => "[blue]TIP[/]",
                        _ => "[dim]INFO[/]"
                    };
                }
                else if (isConfigured)
                {
                    status = "[green]OK[/]";
                }
                else
                {
                    status = "[grey]-[/]";
                }

                table.AddRow(
                    $"[cyan][[{section}]][/]",
                    $"[white]{setting.Key}[/]",
                    displayVal,
                    defaultNote,
                    status
                );
            }

            // Display any custom/unknown keys in this section
            foreach (var kv in entries)
            {
                if (!knownForSection.Any(k => string.Equals(k.Key, kv.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    table.AddRow(
                        $"[cyan][[{section}]][/]",
                        $"[magenta]{Markup.Escape(kv.Key)} (custom)[/]",
                        $"[white]{Markup.Escape(kv.Value)}[/]",
                        "[grey]User custom setting[/]",
                        "[green]OK[/]"
                    );
                }
            }
        }

        AnsiConsole.Write(new Rule("[bold]Current Configuration & Guardrails[/]") { Justification = Justify.Left });
        AnsiConsole.Write(table);

        var warnCount = issues.Count(i => i.Severity == GuardrailSeverity.Warning);
        var errCount = issues.Count(i => i.Severity == GuardrailSeverity.Error);
        var tipCount = issues.Count(i => i.Severity == GuardrailSeverity.Recommendation);

        if (errCount > 0 || warnCount > 0 || tipCount > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Diagnostics summary:[/] [red]{errCount} Errors[/], [yellow]{warnCount} Warnings[/], [blue]{tipCount} Recommendations[/]. Run [bold cyan]Guardrail Doctor[/] from the main menu for detailed insights.");
        }

        ConsoleRenderer.PressEnterToContinue();
    }
}
