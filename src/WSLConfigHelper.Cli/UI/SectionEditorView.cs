using Spectre.Console;
using WSLConfigHelper.Core.Guardrails;
using WSLConfigHelper.Core.Models;
using WSLConfigHelper.Core.Parsing;
using WSLConfigHelper.Core.SystemInfo;

namespace WSLConfigHelper.Cli.UI;

public static class SectionEditorView
{
    public static bool Show(string sectionName, IniDocument doc, HostHardwareMetrics host, GuardrailEngine guardrails)
    {
        bool changed = false;

        while (true)
        {
            var knownSettings = WslKnownSettings.All
                .Where(s => string.Equals(s.Section, sectionName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var currentEntries = doc.GetSectionEntries(sectionName);

            AnsiConsole.Clear();
            var rule = new Rule($"[bold cyan]Edit [[{sectionName}]] Settings[/]") { Justification = Justify.Left };
            AnsiConsole.Write(rule);
            AnsiConsole.MarkupLine("[grey]Select a setting to edit, delete, or inspect guardrail bounds.[/]\n");

            var prompt = new SelectionPrompt<string>()
                .Title("Choose setting:")
                .PageSize(15);

            foreach (var s in knownSettings)
            {
                var isSet = currentEntries.TryGetValue(s.Key, out var val);
                var status = isSet ? $"[white bold]{Markup.Escape(val!)}[/]" : "[grey]<default>[/]";
                prompt.AddChoice($"{s.Key,-24} -> {status}");
            }

            prompt.AddChoice("[green]+ Add Custom / Arbitrary Key[/]");
            prompt.AddChoice("[red]← Back to Main Menu[/]");

            var selection = AnsiConsole.Prompt(prompt);

            if (selection.Contains("Back to Main Menu"))
            {
                break;
            }

            if (selection.Contains("Add Custom"))
            {
                if (EditCustomKey(sectionName, doc))
                {
                    changed = true;
                }
                continue;
            }

            // Find selected setting
            var keyName = selection.Split("->")[0].Trim();
            var settingDef = knownSettings.FirstOrDefault(k => string.Equals(k.Key, keyName, StringComparison.OrdinalIgnoreCase));
            if (settingDef != null)
            {
                if (EditSingleSetting(settingDef, doc, host, guardrails))
                {
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool EditSingleSetting(WslSettingDefinition def, IniDocument doc, HostHardwareMetrics host, GuardrailEngine guardrails)
    {
        AnsiConsole.WriteLine();
        var panel = new Panel(new Markup(
            $"[bold white]{def.DisplayName}[/] ([cyan]{def.Key}[/])\n" +
            $"[grey]Type:[/] {def.Type}  |  [grey]Category:[/] {def.Category ?? "General"}\n\n" +
            $"[white]{Markup.Escape(def.Description)}[/]\n\n" +
            $"[grey]Default:[/] {def.DefaultValue ?? "System defined"}  |  [grey]Recommended:[/] [green]{def.RecommendedValue ?? "Varies by workload"}[/]"
        ))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader($" Setting Details ")
        };
        AnsiConsole.Write(panel);

        var currentVal = doc.GetValue(def.Section, def.Key);
        AnsiConsole.MarkupLine($"Current value: {(currentVal != null ? $"[white bold]{currentVal}[/]" : "[grey]Not set (default)[/]")}\n");

        var actionPrompt = new SelectionPrompt<string>()
            .Title("Action:")
            .AddChoices(
                "Modify Value",
                "Remove Setting (Revert to Default)",
                "Cancel"
            );

        var action = AnsiConsole.Prompt(actionPrompt);
        if (action == "Cancel")
        {
            return false;
        }

        if (action == "Remove Setting (Revert to Default)")
        {
            if (doc.RemoveKey(def.Section, def.Key))
            {
                AnsiConsole.MarkupLine($"[green]✓ Removed '{def.Key}'. WSL2 will use the default behavior.[/]");
                Thread.Sleep(800);
                return true;
            }
            return false;
        }

        // Modify value based on type
        string? newValue = null;

        switch (def.Type)
        {
            case SettingType.Boolean:
                var boolPrompt = new SelectionPrompt<string>()
                    .Title($"Select value for {def.Key}:")
                    .AddChoices("true", "false");
                newValue = AnsiConsole.Prompt(boolPrompt);
                break;

            case SettingType.Enum:
                var enumPrompt = new SelectionPrompt<string>()
                    .Title($"Select option for {def.Key}:");
                if (def.AllowedValues != null)
                {
                    foreach (var opt in def.AllowedValues)
                    {
                        enumPrompt.AddChoice(opt);
                    }
                }
                newValue = AnsiConsole.Prompt(enumPrompt);
                break;

            case SettingType.MemorySize:
                var maxRam = host.TotalPhysicalMemoryGigabytes;
                var safeCap = (ulong)(host.TotalPhysicalMemoryBytes * 0.75);
                var safeStr = MemorySizeHelper.FormatBytes(safeCap);

                AnsiConsole.MarkupLine($"[grey]Host total RAM:[/] {maxRam:F1} GB. [grey]Recommended safe limit (75%):[/] [green]{safeStr}[/]");
                var textPrompt = new TextPrompt<string>($"Enter memory size (e.g. 8GB, 4096MB):")
                    .DefaultValue(currentVal ?? safeStr)
                    .Validate(input =>
                    {
                        if (!MemorySizeHelper.TryParseToBytes(input, out var bytes))
                        {
                            return ValidationResult.Error("[red]Invalid format. Use units like '8GB', '4096MB', or '0'.[/]");
                        }
                        if (bytes > 0 && bytes < 2UL * 1024 * 1024 * 1024)
                        {
                            return ValidationResult.Error("[yellow]Allocating less than 2GB is not recommended for WSL2.[/]");
                        }
                        if (bytes > host.TotalPhysicalMemoryBytes)
                        {
                            return ValidationResult.Error($"[red]Value exceeds total host physical RAM ({maxRam:F1} GB).[/]");
                        }
                        return ValidationResult.Success();
                    });
                newValue = AnsiConsole.Prompt(textPrompt);
                break;

            case SettingType.Integer:
                var intPrompt = new TextPrompt<int>($"Enter number for {def.Key}:")
                    .DefaultValue(int.TryParse(currentVal, out var cv) ? cv : Math.Max(2, host.LogicalProcessors - 2))
                    .Validate(val =>
                    {
                        if (val <= 0) return ValidationResult.Error("[red]Must be greater than 0.[/]");
                        if (def.Key == "processors" && val > host.LogicalProcessors)
                        {
                            return ValidationResult.Error($"[red]Cannot exceed host logical cores ({host.LogicalProcessors}).[/]");
                        }
                        return ValidationResult.Success();
                    });
                newValue = AnsiConsole.Prompt(intPrompt).ToString();
                break;

            default:
                var stringPrompt = new TextPrompt<string>($"Enter value for {def.Key}:");
                if (currentVal != null) stringPrompt.DefaultValue(currentVal);
                newValue = AnsiConsole.Prompt(stringPrompt);
                break;
        }

        if (!string.IsNullOrWhiteSpace(newValue))
        {
            doc.SetValue(def.Section, def.Key, newValue);
            AnsiConsole.MarkupLine($"[green]✓ Updated '{def.Key}' to '{newValue}'[/]");
            Thread.Sleep(800);
            return true;
        }

        return false;
    }

    private static bool EditCustomKey(string defaultSection, IniDocument doc)
    {
        var section = AnsiConsole.Prompt(new TextPrompt<string>("Section name:")
            .DefaultValue(defaultSection));

        var key = AnsiConsole.Prompt(new TextPrompt<string>("Key name:"));
        var val = AnsiConsole.Prompt(new TextPrompt<string>("Value:"));

        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(val))
        {
            doc.SetValue(section, key, val);
            AnsiConsole.MarkupLine($"[green]✓ Set [[{section}]] {Markup.Escape(key)}={Markup.Escape(val)}[/]");
            Thread.Sleep(800);
            return true;
        }

        return false;
    }
}
