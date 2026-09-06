using Spectre.Console;
using WSLConfigHelper.Core.Guardrails;
using WSLConfigHelper.Core.Parsing;
using WSLConfigHelper.Core.SystemInfo;

namespace WSLConfigHelper.Cli.UI;

public static class PresetView
{
    public static bool Show(IniDocument doc, HostHardwareMetrics host, PresetEngine presetEngine)
    {
        var presets = presetEngine.GeneratePresets(host);

        AnsiConsole.Write(new Rule("[bold cyan]Hardware-Tuned Configuration Presets[/]") { Justification = Justify.Left });
        AnsiConsole.MarkupLine("[grey]Presets are dynamically calculated based on your host's physical RAM and logical cores.[/]\n");

        var prompt = new SelectionPrompt<string>()
            .Title("Select a preset to preview:")
            .PageSize(10);

        foreach (var p in presets)
        {
            prompt.AddChoice(p.Name);
        }
        prompt.AddChoice("[red]Cancel & Return to Menu[/]");

        var selection = AnsiConsole.Prompt(prompt);
        if (selection.Contains("Cancel"))
        {
            return false;
        }

        var chosenPreset = presets.First(p => p.Name == selection);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold yellow]Selected:[/] [bold]{chosenPreset.Name}[/]");
        AnsiConsole.MarkupLine($"[grey]{chosenPreset.Description}[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn("[bold]Section[/]");
        table.AddColumn("[bold]Key[/]");
        table.AddColumn("[bold]Current Value[/]");
        table.AddColumn("[bold]Preset Proposed Value[/]");

        foreach (var (section, settings) in chosenPreset.Settings)
        {
            foreach (var (key, val) in settings)
            {
                var current = doc.GetValue(section, key) is { } rawCurrent
                    ? Markup.Escape(rawCurrent)
                    : "[grey]Not set[/]";
                table.AddRow(
                    $"[cyan][[{section}]][/]",
                    $"[white]{Markup.Escape(key)}[/]",
                    current,
                    $"[green bold]{Markup.Escape(val)}[/]"
                );
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (AnsiConsole.Confirm("Apply these preset settings to current in-memory configuration?", defaultValue: true))
        {
            presetEngine.ApplyPreset(doc, chosenPreset);
            AnsiConsole.MarkupLine("[green bold]✓ Preset applied to in-memory configuration![/] Remember to select 'Save' from the main menu to write to disk.");
            ConsoleRenderer.PressEnterToContinue();
            return true;
        }

        return false;
    }
}
