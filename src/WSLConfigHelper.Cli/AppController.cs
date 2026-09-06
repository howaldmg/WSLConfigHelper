using Spectre.Console;
using WSLConfigHelper.Cli.UI;
using WSLConfigHelper.Core.Guardrails;
using WSLConfigHelper.Core.Models;
using WSLConfigHelper.Core.Parsing;
using WSLConfigHelper.Core.Storage;
using WSLConfigHelper.Core.SystemInfo;

namespace WSLConfigHelper.Cli;

public class AppController
{
    private readonly WslConfigFileService _fileService;
    private readonly IHostSystemProber _prober;
    private readonly GuardrailEngine _guardrails;
    private readonly PresetEngine _presets;

    private readonly DesktopAutomationView _desktopAutomationView;
    private IniDocument _currentDoc;
    private HostHardwareMetrics _hostMetrics;
    private bool _hasUnsavedChanges;

    public AppController(WslConfigFileService? fileService = null, IHostSystemProber? prober = null)
    {
        _fileService = fileService ?? new WslConfigFileService();
        _prober = prober ?? new HostSystemProber();
        _guardrails = new GuardrailEngine();
        _presets = new PresetEngine();
        _desktopAutomationView = new DesktopAutomationView();

        _hostMetrics = _prober.Probe();
        _currentDoc = _fileService.Load();
        _hasUnsavedChanges = false;
    }

    public async Task RunAsync()
    {
        while (true)
        {
            ConsoleRenderer.RenderHeader(_hostMetrics, _fileService.ConfigFilePath, _hasUnsavedChanges, _fileService.BackupExists());

            var menu = new SelectionPrompt<string>()
                .Title("[bold]Main Menu:[/] Choose an operation")
                .PageSize(12)
                .AddChoices(
                    "1. 📊 View Current Configuration & Hardware Alignment",
                    "2. 🩺 Run Guardrail Doctor (Diagnostics Audit)",
                    "3. ⚡ Apply Hardware-Tuned Preset",
                    "4. ⚙️  Edit [[wsl2]] Core Settings (Memory, vCPUs, Swap...)",
                    "5. 🧪 Edit [[experimental]] Settings (Mirrored Net, AutoReclaim...)",
                    "6. 🖥️  Desktop Experience & Viewport (Fedora KDE + GPU-PV)",
                    "7. 💾 Save Changes to .wslconfig (Creates single .bak)",
                    "8. 🔄 Restore from .wslconfig.bak",
                    "9. 📄 View Raw INI Document",
                    "10. 🚪 Exit"
                );

            var choice = AnsiConsole.Prompt(menu);

            if (choice.StartsWith("1."))
            {
                DashboardView.Show(_currentDoc, _hostMetrics, _guardrails);
            }
            else if (choice.StartsWith("2."))
            {
                DoctorView.Show(_currentDoc, _hostMetrics, _guardrails);
            }
            else if (choice.StartsWith("3."))
            {
                if (PresetView.Show(_currentDoc, _hostMetrics, _presets))
                {
                    _hasUnsavedChanges = true;
                }
            }
            else if (choice.StartsWith("4."))
            {
                if (SectionEditorView.Show(WslKnownSettings.SectionWsl2, _currentDoc, _hostMetrics, _guardrails))
                {
                    _hasUnsavedChanges = true;
                }
            }
            else if (choice.StartsWith("5."))
            {
                if (SectionEditorView.Show(WslKnownSettings.SectionExperimental, _currentDoc, _hostMetrics, _guardrails))
                {
                    _hasUnsavedChanges = true;
                }
            }
            else if (choice.StartsWith("6."))
            {
                await _desktopAutomationView.ShowAsync();
            }
            else if (choice.StartsWith("7."))
            {
                SaveChanges();
            }
            else if (choice.StartsWith("8."))
            {
                RestoreBackup();
            }
            else if (choice.StartsWith("9."))
            {
                ShowRawIni();
            }
            else if (choice.StartsWith("10."))
            {
                if (ConfirmExit())
                {
                    break;
                }
            }
        }
    }

    private void SaveChanges()
    {
        try
        {
            var alreadyExists = _fileService.Exists();
            _fileService.Save(_currentDoc);
            _hasUnsavedChanges = false;

            AnsiConsole.MarkupLine("[green bold]✓ Successfully saved to disk![/]");
            if (alreadyExists)
            {
                AnsiConsole.MarkupLine($"[cyan]✓ Single backup updated at:[/] [grey]{_fileService.BackupFilePath}[/]");
            }
            AnsiConsole.MarkupLine("[grey]Remember to restart WSL (`wsl --shutdown`) for changes to take effect.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red bold]Failed to save configuration:[/] {ex.Message}");
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private void RestoreBackup()
    {
        if (!_fileService.BackupExists())
        {
            AnsiConsole.MarkupLine("[yellow]No backup file (.wslconfig.bak) was found on disk.[/]");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        if (AnsiConsole.Confirm("Are you sure you want to restore the existing .wslconfig from .wslconfig.bak?", defaultValue: false))
        {
            if (_fileService.RestoreBackup())
            {
                _currentDoc = _fileService.Load();
                _hasUnsavedChanges = false;
                AnsiConsole.MarkupLine("[green bold]✓ Restored .wslconfig from backup file.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to restore backup.[/]");
            }
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private void ShowRawIni()
    {
        AnsiConsole.Clear();
        var rule = new Rule("[bold cyan]Raw In-Memory .wslconfig Content[/]") { Justification = Justify.Left };
        AnsiConsole.Write(rule);

        var raw = _currentDoc.Serialize();
        if (string.IsNullOrWhiteSpace(raw))
        {
            AnsiConsole.MarkupLine("[grey]<Empty Document>[/]");
        }
        else
        {
            AnsiConsole.WriteLine(raw);
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private bool ConfirmExit()
    {
        if (_hasUnsavedChanges)
        {
            if (AnsiConsole.Confirm("[yellow bold]You have unsaved changes in memory![/] Would you like to save before exiting?", defaultValue: true))
            {
                _fileService.Save(_currentDoc);
                _hasUnsavedChanges = false;
                AnsiConsole.MarkupLine("[green]Saved changes.[/]");
                Thread.Sleep(500);
                return true;
            }

            return AnsiConsole.Confirm("Exit anyway without saving?", defaultValue: false);
        }

        return true;
    }
}
