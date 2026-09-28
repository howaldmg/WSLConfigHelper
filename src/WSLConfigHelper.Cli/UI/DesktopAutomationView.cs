using System.Diagnostics;
using Spectre.Console;
using WSLConfigHelper.Automation;
using WSLConfigHelper.Automation.DesktopEnvironments;
using WSLConfigHelper.Automation.DistroBases;
using WSLConfigHelper.Automation.Workstations;
using WSLConfigHelper.Core.Storage;
using WSLConfigHelper.Core.SystemInfo;

namespace WSLConfigHelper.Cli.UI;

/// <summary>
/// Terminal UI view presenting the interactive provisioning, configuration, and launch workflow
/// for WSL2 desktop workstations and multi-monitor viewports.
/// </summary>
public class DesktopAutomationView
{
    private readonly DistroManager _distroManager;
    private readonly GpuConfigurator _gpuConfigurator;
    private readonly ViewportManager _viewportManager;
    private readonly WslConfigFileService _configFileService;
    private readonly IWslProcessRunner _runner;
    private readonly WorkstationRegistry _registry;
    private readonly IDisplayProber _displayProber;
    private WorkstationProfile _activeProfile;

    public DesktopAutomationView(IDisplayProber? displayProber = null)
    {
        _runner = new WslProcessRunner();
        _distroManager = new DistroManager(_runner);
        _gpuConfigurator = new GpuConfigurator(_runner);
        _viewportManager = new ViewportManager(_runner);
        _configFileService = new WslConfigFileService();
        _registry = new WorkstationRegistry();
        _displayProber = displayProber ?? new DisplayProber();
        _activeProfile = _registry.GetProfile("fedora-kde")!;
    }

    public async Task ShowAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            var rule = new Rule($"[bold cyan]WSL Desktop Workstation Manager[/] [grey]({_activeProfile.DisplayName})[/]")
            {
                Justification = Justify.Left
            };
            AnsiConsole.Write(rule);

            bool distroExists = await _distroManager.DistroExistsAsync(_activeProfile.DistroName);
            var statusBadge = distroExists ? "[green bold]Installed / Available[/]" : "[yellow]Not Yet Provisioned[/]";

            var grid = new Grid();
            grid.AddColumn(new GridColumn().PadRight(4));
            grid.AddColumn(new GridColumn());
            grid.AddRow("[grey]Workstation Profile:[/] [bold white]" + _activeProfile.DisplayName + "[/]", $"[grey]Distro Status:[/] {statusBadge}");
            grid.AddRow("[grey]WSL Instance Name:[/]   [white]" + _activeProfile.DistroName + "[/]", $"[grey]WSLg Viewport:[/] [bold green]Active (Monitor Native Hz)[/]");
            grid.AddRow("[grey]Base Distribution:[/]   [white]" + _activeProfile.DistroBase.DisplayName + $" ({_activeProfile.DistroBase.PackageManager})[/]", $"[grey]RDP Viewport:[/]  [dim]localhost:{_activeProfile.RdpPort} (Optional Fallback)[/]");
            grid.AddRow("[grey]Desktop Shell:[/]       [white]" + _activeProfile.DesktopEnvironment.DisplayName + "[/]", "[grey]GPU Backend:[/]   [white]Mesa D3D12 (/dev/dxg GPU-PV)[/]");

            AnsiConsole.Write(new Panel(grid)
            {
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("grey")
            });
            AnsiConsole.WriteLine();

            var menu = new SelectionPrompt<string>()
                .Title("[bold]Automation Operations & Phased Checkpoints:[/]")
                .PageSize(10)
                .AddChoices(
                    " 1. 🔍 Run Diagnostics & Hardware Audit (Checkpoints 1 & 2)",
                    $" 2. 📦 Phase 1: Provision {_activeProfile.DistroName} & GPU-PV (Mesa D3D12)",
                    $" 3. 🎨 Phase 2: Install {_activeProfile.DesktopEnvironment.DisplayName} & Audio",
                    $" 4. 🚀 Phase 3: Launch Desktop Workstation (RDP - Port {_activeProfile.RdpPort})",
                    " 5. 🔄 Switch / Create Workstation Profile",
                    $" 6. 🛑 Stop / Terminate {_activeProfile.DistroName}",
                    $" 7. 🗑️  Tear Down / Unregister {_activeProfile.DistroName} (Reset)",
                    " 8. 🚪 ← Back to Main Menu"
                );

            var choice = AnsiConsole.Prompt(menu);

            if (choice.Contains(" 1."))
            {
                await RunDiagnosticsAsync(distroExists);
            }
            else if (choice.Contains(" 2."))
            {
                await RunPhase1Async(distroExists);
            }
            else if (choice.Contains(" 3."))
            {
                await RunPhase2Async(distroExists);
            }
            else if (choice.Contains(" 4."))
            {
                await RunPhase3Async(distroExists);
            }
            else if (choice.Contains(" 5."))
            {
                await SwitchOrCreateProfileAsync();
            }
            else if (choice.Contains(" 6."))
            {
                await StopWorkstationAsync();
            }
            else if (choice.Contains(" 7."))
            {
                await TeardownWorkstationAsync();
            }
            else
            {
                break;
            }
        }
    }

    private async Task RunDiagnosticsAsync(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold cyan]System Diagnostics & Hardware Audit: {_activeProfile.DisplayName}[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{_activeProfile.DistroName}' is not yet provisioned.[/] Run Phase 1 from the menu to create it.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Auditing GPU-PV and Desktop subsystems...", async ctx =>
            {
                ctx.Status("Checking /dev/dxg device & graphics drivers...");
                var gpuReport = await _gpuConfigurator.RunGpuDiagnosticsAsync(_activeProfile.DistroName);

                ctx.Status($"Checking {_activeProfile.DesktopEnvironment.DisplayName} components...");
                var desktopProbe = await _activeProfile.DesktopEnvironment.ProbeDesktopAsync(_activeProfile.DistroName, _runner);

                var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
                table.AddColumn("[bold]Component[/]");
                table.AddColumn("[bold]Status[/]");
                table.AddColumn("[bold]Details[/]");

                table.AddRow(
                    "DirectX GPU-PV (/dev/dxg)",
                    gpuReport.DxgDeviceFound ? "[green]FOUND[/]" : "[red]MISSING[/]",
                    gpuReport.DxgDeviceFound ? "Kernel dxgkrnl driver mounted" : "Check Windows WSL2 GPU drivers"
                );

                table.AddRow(
                    "WSL Host Libs (/usr/lib/wsl/lib)",
                    gpuReport.WslLibMounted ? "[green]MOUNTED[/]" : "[red]MISSING[/]",
                    gpuReport.WslLibMounted ? "Windows DXCore & CUDA libraries attached" : "Auto-mount disabled"
                );

                table.AddRow(
                    "OpenGL Hardware Acceleration",
                    gpuReport.DirectRenderingEnabled ? "[green]ACTIVE[/]" : "[yellow]SOFTWARE / OFF[/]",
                    gpuReport.OpenGLRenderer ?? "[grey]Unknown (Run Phase 1 to install drivers)[/]"
                );

                table.AddRow(
                    $"{_activeProfile.DesktopEnvironment.DisplayName} Shell ({desktopProbe.CompositorOrWm})",
                    desktopProbe.IsInstalled ? "[green]INSTALLED[/]" : "[yellow]NOT INSTALLED[/]",
                    desktopProbe.Version ?? $"Run Phase 2 to install {_activeProfile.DesktopEnvironment.DisplayName}"
                );

                table.AddRow(
                    "Audio Subsystem",
                    desktopProbe.AudioReady ? "[green]READY[/]" : "[yellow]NOT READY[/]",
                    desktopProbe.AudioReady ? "PipeWire sound server active" : "Run Phase 2 to configure"
                );

                AnsiConsole.Write(table);
            });

        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task RunPhase1Async(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold cyan]Phase 1: Provision {_activeProfile.DistroName} & Configure GPU-PV[/]") { Justification = Justify.Left });
        AnsiConsole.MarkupLine($"[grey]This phase will install {_activeProfile.DistroBase.DefaultWslImage} as '{_activeProfile.DistroName}', enable systemd, and configure Mesa D3D12.[/]\n");

        if (!distroExists)
        {
            if (!AnsiConsole.Confirm($"Download and provision WSL instance '[bold]{_activeProfile.DistroName}[/]' from {_activeProfile.DistroBase.DefaultWslImage}?", defaultValue: true))
            {
                return;
            }

            bool useCache = AnsiConsole.Confirm("Use local cached base image & packages (faster)?", defaultValue: true);
            bool noCache = !useCache;

            AnsiConsole.MarkupLine("[cyan]Starting download and installation via WSL... (this may take a few minutes)[/]");
            var installResult = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Downloading and registering {_activeProfile.DistroName}...", async ctx =>
                {
                    return await _distroManager.InstallDistroAsync(_activeProfile.DistroBase.DefaultWslImage, _activeProfile.DistroName, line =>
                    {
                        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
                    }, noCache: noCache);
                });

            if (!installResult.Success)
            {
                AnsiConsole.MarkupLine($"[red bold]Installation failed:[/] {Markup.Escape(installResult.StandardError)}");
                ConsoleRenderer.PressEnterToContinue();
                return;
            }

            AnsiConsole.MarkupLine($"[green bold]✓ Distribution '{_activeProfile.DistroName}' successfully registered![/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]'{_activeProfile.DistroName}' already exists.[/] Proceeding to GPU and system configuration...");
        }

        AnsiConsole.MarkupLine("[cyan]Configuring /etc/wsl.conf and systemd...[/]");
        await _activeProfile.DistroBase.ConfigureWslConfAsync(_activeProfile.DistroName, defaultUser: "developer", _runner);

        AnsiConsole.MarkupLine("[cyan]Configuring GPU acceleration & Mesa D3D12 drivers...[/]");
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Configuring dynamic linker & graphics packages...", async ctx =>
            {
                await _activeProfile.DistroBase.ConfigureGpuAccelerationAsync(_activeProfile.DistroName, _runner);
            });
        AnsiConsole.MarkupLine("[green bold]✓ Graphics driver packages and linker paths configured![/]");

        // Restart instance to ensure systemd and drivers load cleanly
        AnsiConsole.MarkupLine($"[cyan]Restarting {_activeProfile.DistroName} to apply configuration...[/]");
        await _distroManager.TerminateDistroAsync(_activeProfile.DistroName);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]Checkpoint 1 Verification:[/]");
        var gpuReport = await _gpuConfigurator.RunGpuDiagnosticsAsync(_activeProfile.DistroName);

        if (gpuReport.DirectRenderingEnabled)
        {
            AnsiConsole.MarkupLine($"[green bold]✓ Direct Rendering Enabled![/] Hardware Adapter: [bold cyan]{Markup.Escape(gpuReport.OpenGLRenderer ?? "NVIDIA")}[/]");
            AnsiConsole.MarkupLine($"[grey]OpenGL Version: {Markup.Escape(gpuReport.OpenGLVersion ?? "N/A")}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Warning: Direct rendering could not be confirmed automatically yet. Diagnostic details:[/]");
            AnsiConsole.WriteLine(gpuReport.RawGlxInfoOutput);
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task RunPhase2Async(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold cyan]Phase 2: Install {_activeProfile.DesktopEnvironment.DisplayName} & Audio[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{_activeProfile.DistroName}' is not yet provisioned.[/] Run Phase 1 first.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        AnsiConsole.MarkupLine($"[grey]This will set up the 'developer' user (sudo), and install {_activeProfile.DesktopEnvironment.DisplayName} packages.[/]\n");

        if (!AnsiConsole.Confirm("Begin Phase 2 installation?", defaultValue: true))
        {
            return;
        }

        bool usePackageCache = AnsiConsole.Confirm("Use local package cache repo (faster)?", defaultValue: true);
        bool noCache = !usePackageCache;

        AnsiConsole.MarkupLine("[cyan]Configuring unprivileged 'developer' user with passwordless sudo & lingering...[/]");
        await _activeProfile.DistroBase.EnsureUserAsync(_activeProfile.DistroName, "developer", _runner);
        AnsiConsole.MarkupLine("[green]✓ User 'developer' configured.[/]");

        var packages = _activeProfile.DesktopEnvironment.GetPackageList(_activeProfile.DistroBase.PackageManager);
        AnsiConsole.MarkupLine($"[cyan]Starting {_activeProfile.DistroBase.PackageManager} installation of {_activeProfile.DesktopEnvironment.DisplayName} packages...[/]");
        AnsiConsole.MarkupLine($"[grey]Packages: {string.Join(", ", packages)}[/]");

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Installing {_activeProfile.DesktopEnvironment.DisplayName} desktop environment...", async ctx =>
            {
                await _activeProfile.DistroBase.InstallPackagesAsync(_activeProfile.DistroName, packages, _runner, line =>
                {
                    if (line.Contains("Installing:") || line.Contains("Complete!") || line.Contains("Setting up") ||
                        line.Contains("download 0 B") || line.Contains("Already downloaded") || line.Contains("[Cache"))
                    {
                        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(line)}[/]");
                    }
                }, noCache: noCache);
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]Checkpoint 2 Verification:[/]");
        var probe = await _activeProfile.DesktopEnvironment.ProbeDesktopAsync(_activeProfile.DistroName, _runner);

        if (probe.IsInstalled)
        {
            AnsiConsole.MarkupLine($"[green bold]✓ {_activeProfile.DesktopEnvironment.DisplayName} Installed:[/] [bold cyan]{Markup.Escape(probe.Version ?? probe.CompositorOrWm)}[/]");
            AnsiConsole.MarkupLine($"[green]✓ Audio Subsystem:[/] {(probe.AudioReady ? "Ready" : "Missing / Inactive")}");
            AnsiConsole.MarkupLine("[green bold]✓ Phase 2 complete! Ready for Viewport setup.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]{_activeProfile.DesktopEnvironment.DisplayName} was not detected. Please inspect package installation logs.[/]");
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task RunPhase3Async(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold cyan]Phase 3: Launch Hardware-Accelerated Desktop ({_activeProfile.DesktopEnvironment.DisplayName})[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{_activeProfile.DistroName}' is not yet provisioned.[/] Complete Phase 1 and Phase 2 first.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        int port = _activeProfile.RdpPort;
        AnsiConsole.MarkupLine("[grey]Deploys a low-latency, hardware-accelerated remote desktop session via RDP (mstsc.exe).[/]");
        AnsiConsole.MarkupLine("[grey]Hardware cursor prediction provides zero mouse lag with full DirectX 12 GPU-PV passthrough.[/]\n");

        // 1. Detect connected Windows displays
        var displays = _displayProber.GetConnectedDisplays();
        var primaryDisp = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault() ?? new DisplayInfo(1, "DISPLAY1", 1920, 1080, 0, 0, 1920, 1040, 0, 0, true);

        var choices = new List<string>
        {
            $"📺 Fullscreen (Primary Display {primaryDisp.DisplayNumber}: {primaryDisp.Width}x{primaryDisp.Height})",
            "🖥️  Multi-Monitor Fullscreen (Span across all displays)",
            "🪟 Dynamic Windowed (Auto-resizes with window)",
            "⚙️  Custom Resolution"
        };

        var modeChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select Remote Desktop Display Mode:[/]")
                .PageSize(8)
                .AddChoices(choices)
        );

        int width = 0;
        int height = 0;
        bool fullscreen = true;
        bool multimon = false;

        if (modeChoice.StartsWith("📺"))
        {
            width = primaryDisp.Width;
            height = primaryDisp.Height;
            fullscreen = true;
            multimon = false;
        }
        else if (modeChoice.StartsWith("🖥️"))
        {
            fullscreen = true;
            multimon = true;
        }
        else if (modeChoice.StartsWith("🪟"))
        {
            fullscreen = false;
            multimon = false;
        }
        else
        {
            width = AnsiConsole.Prompt(new TextPrompt<int>("Viewport Width (pixels):").DefaultValue(1920));
            height = AnsiConsole.Prompt(new TextPrompt<int>("Viewport Height (pixels):").DefaultValue(1080));
            fullscreen = false;
        }

        AnsiConsole.MarkupLine("[cyan]Configuring XRDP performance tuning and user session inside distro...[/]");
        var options = new ViewportOptions(Width: width > 0 ? width : 1920, Height: height > 0 ? height : 1080, Port: port, User: "developer");
        var result = await _activeProfile.DesktopEnvironment.ConfigureViewportServiceAsync(_activeProfile.DistroName, _runner, options);

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red bold]Failed to configure remote desktop:[/] {Markup.Escape(result.StandardError)}");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        AnsiConsole.MarkupLine($"[green]✓ Remote desktop service active on port {port}![/]");

        // Generate optimized Windows .rdp profile and .cmd launcher
        string rdpFile = $"{_activeProfile.DistroName}.rdp";
        await _viewportManager.GenerateOptimizedRdpFileAsync(
            rdpFile,
            hostname: "127.0.0.1",
            port: port,
            username: "developer",
            width: width,
            height: height,
            fullscreen: fullscreen,
            multimon: multimon);

        var cmdContent = $"""
            @echo off
            echo Waking up {_activeProfile.DistroName}...
            wsl -d {_activeProfile.DistroName} -u developer -- true
            echo Connecting to {_activeProfile.DesktopEnvironment.DisplayName} on port {port}...
            start mstsc {rdpFile}
            """;
        await File.WriteAllTextAsync($"connect-{_activeProfile.DistroName.ToLowerInvariant()}.cmd", cmdContent);

        var infoPanel = new Panel(new Markup(
            "[bold white]Remote Desktop Connection Profile:[/]\n\n" +
            $"• Address:         [bold green]127.0.0.1:{port}[/]\n" +
            "• Mode:            [bold cyan]" + (multimon ? "Multi-Monitor Fullscreen" : (fullscreen ? $"Fullscreen ({width}x{height})" : "Dynamic Windowed")) + "[/]\n" +
            "• Acceleration:    [bold green]DirectX 12 GPU-PV (/dev/dxg, Mesa D3D12)[/]\n" +
            "• Input Latency:   [bold green]Zero Client-Side Mouse Cursor Prediction[/]\n" +
            "• Audio:           [bold cyan]PipeWire XRDP Virtual Channel Redirection[/]\n" +
            "• Credentials:     [bold cyan]developer / developer[/]\n\n" +
            $"[grey]Generated launchers:[/] [cyan]{rdpFile}[/], [cyan]connect-{_activeProfile.DistroName.ToLowerInvariant()}.cmd[/]"
        ))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader($" {_activeProfile.DesktopEnvironment.DisplayName} Workstation ")
        };
        AnsiConsole.Write(infoPanel);
        AnsiConsole.WriteLine();

        if (AnsiConsole.Confirm($"Open Windows Remote Desktop (mstsc) to {_activeProfile.DesktopEnvironment.DisplayName} now?", defaultValue: true))
        {
            AnsiConsole.MarkupLine("[cyan]Opening Windows Remote Desktop Connection (mstsc.exe)...[/]");
            _viewportManager.LaunchWindowsMstsc(rdpFile);
            AnsiConsole.MarkupLine("[green]✓ Remote Desktop launched! Enter 'developer' / 'developer' when prompted.[/]");
        }

        ConsoleRenderer.PressEnterToContinue();
    }



    private async Task RunPhase4WslgAppsAsync(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold cyan]Phase 4: Launch Native {_activeProfile.DesktopEnvironment.DisplayName} Apps in WSLg[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{_activeProfile.DistroName}' is not yet provisioned.[/] Complete Phase 1 and Phase 2 first.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        AnsiConsole.MarkupLine($"[grey]Launches individual {_activeProfile.DesktopEnvironment.DisplayName} apps seamlessly onto your Windows desktop using WSLg.[/]\n");

        var choices = _activeProfile.DesktopEnvironment.RecommendedApps
            .Select(app => $"{app.Name} ({app.Command}) - {app.Category}")
            .ToList();
        choices.Add("✏️ Custom Linux GUI Command");
        choices.Add("← Cancel");

        var appChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select an application to launch on Windows:[/]")
                .AddChoices(choices)
        );

        if (appChoice == "← Cancel")
        {
            return;
        }

        string? command = null;
        if (appChoice == "✏️ Custom Linux GUI Command")
        {
            command = AnsiConsole.Prompt(new TextPrompt<string>("Enter Linux GUI command:"));
        }
        else
        {
            var matchedApp = _activeProfile.DesktopEnvironment.RecommendedApps
                .FirstOrDefault(app => appChoice.StartsWith(app.Name));
            command = matchedApp?.Command;
        }

        if (!string.IsNullOrWhiteSpace(command))
        {
            AnsiConsole.MarkupLine($"[green bold]Launching '{command}' via WSLg...[/]");
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d {_activeProfile.DistroName} -u developer -- {command}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);
            AnsiConsole.MarkupLine("[cyan]Process launched! Check your Windows taskbar.[/]");
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task SwitchOrCreateProfileAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Workstation Profile Selection & Setup[/]") { Justification = Justify.Left });

        var profiles = _registry.GetAllProfiles().ToList();
        var menuChoices = new List<string>();

        foreach (var p in profiles)
        {
            var activeMarker = p.Id == _activeProfile.Id ? " [bold green](Active)[/]" : "";
            menuChoices.Add($"{p.DisplayName} ({p.DistroName}){activeMarker}");
        }
        menuChoices.Add("🛠️  Create Custom Profile (Mix & Match Distro Base + Desktop)");
        menuChoices.Add("🚪 ← Cancel");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Choose an existing profile to activate, or create a custom profile:[/]")
                .PageSize(10)
                .AddChoices(menuChoices)
        );

        if (choice.Contains("Cancel"))
        {
            return;
        }

        if (choice.StartsWith("🛠️"))
        {
            AnsiConsole.MarkupLine("\n[bold cyan]1. Select Base Distribution:[/]");
            var bases = _registry.GetAvailableDistroBases().ToList();
            var baseChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose Distro Base:")
                    .AddChoices(bases.Select(b => $"{b.DisplayName} ({b.PackageManager})"))
            );
            var selectedBase = bases.First(b => baseChoice.StartsWith(b.DisplayName));

            AnsiConsole.MarkupLine("\n[bold cyan]2. Select Desktop Environment:[/]");
            var des = _registry.GetAvailableDesktopEnvironments().ToList();
            var deChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose Desktop Environment:")
                    .AddChoices(des.Select(d => d.DisplayName))
            );
            var selectedDe = des.First(d => deChoice.StartsWith(d.DisplayName));

            AnsiConsole.MarkupLine("\n[bold cyan]3. Enter WSL Instance Name:[/]");
            string defaultName = $"{selectedBase.Id.ToUpperInvariant()}-{selectedDe.Id.ToUpperInvariant()}";
            string customName = AnsiConsole.Prompt(
                new TextPrompt<string>("WSL Distribution Instance Name:")
                    .DefaultValue(defaultName)
            );

            int nextPort = _registry.GetNextAvailablePort();
            AnsiConsole.MarkupLine("\n[bold cyan]4. Enter RDP Viewport Port:[/]");
            int customPort = AnsiConsole.Prompt(
                new TextPrompt<int>("RDP Port:")
                    .DefaultValue(nextPort)
            );

            var newProfile = _registry.CreateCustomProfile(selectedBase, selectedDe, customName, customPort);
            _activeProfile = newProfile;
            AnsiConsole.MarkupLine($"\n[green bold]✓ Custom profile created and activated: {newProfile.DisplayName} (Port {newProfile.RdpPort})![/]");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        var matched = profiles.FirstOrDefault(p => choice.StartsWith(p.DisplayName));
        if (matched != null)
        {
            _activeProfile = matched;
            AnsiConsole.MarkupLine($"\n[green bold]✓ Switched active profile to '{matched.DisplayName}'![/]");
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task StopWorkstationAsync()
    {
        AnsiConsole.MarkupLine($"[cyan]Shutting down {_activeProfile.DistroName}...[/]");
        await _distroManager.TerminateDistroAsync(_activeProfile.DistroName);
        AnsiConsole.MarkupLine($"[green bold]✓ {_activeProfile.DistroName} successfully stopped.[/]");
        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task TeardownWorkstationAsync()
    {
        AnsiConsole.Clear();
        var distroName = _activeProfile.DistroName;

        var dangerPanel = new Panel(new Markup(
            "[bold red]⚠️  DANGER ZONE: Permanent Workstation Deletion[/]\n\n" +
            $"You are about to permanently unregister and delete: [bold white]{distroName}[/]\n\n" +
            "This action [bold red]CANNOT[/] be undone:\n" +
            "  • Destroys the virtual disk ([cyan]ext4.vhdx[/]) and all data inside the instance\n" +
            "  • Removes WSL registration for this distro\n" +
            "  • Deletes local launcher shortcuts and RDP profiles\n\n" +
            "[grey]Note: Base cached images (.tar) are preserved for instant rebuilds.[/]"
        ))
        {
            Border = BoxBorder.Heavy,
            BorderStyle = Style.Parse("red")
        };
        AnsiConsole.Write(dangerPanel);
        AnsiConsole.WriteLine();

        var confirmation = AnsiConsole.Prompt(
            new TextPrompt<string>($"To confirm deletion, please type [bold red]{distroName}[/] or press Enter to abort:")
                .AllowEmpty()
        );

        if (!string.Equals(confirmation?.Trim(), distroName, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("\n[grey]Teardown aborted. No changes were made.[/]");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        AnsiConsole.WriteLine();
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Terminating and unregistering {distroName}...", async _ =>
            {
                await _distroManager.UnregisterDistroAsync(distroName);
                CleanupDistroLaunchers(distroName);
            });

        AnsiConsole.MarkupLine($"[green bold]✓ {distroName} has been completely uninstalled and cleaned up.[/]");
        ConsoleRenderer.PressEnterToContinue();
    }

    private static void CleanupDistroLaunchers(string distroName)
    {
        var filesToDelete = new[]
        {
            $"launch-{distroName.ToLowerInvariant()}-wslg.cmd",
            $"connect-{distroName.ToLowerInvariant()}.cmd",
            $"{distroName}.rdp",
            $"{distroName.ToUpperInvariant()}.rdp"
        };

        foreach (var file in filesToDelete)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private async Task RunPhase5SunshineAsync(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Phase 5: Sunshine Streaming (Technical Architecture)[/]") { Justification = Justify.Left });

        AnsiConsole.MarkupLine("[grey]Sunshine self-hosted streaming server for Moonlight clients.[/]\n");

        var archPanel = new Panel(new Markup(
            "[bold yellow]WSL2 Graphics Virtualization Architecture Note:[/]\n\n" +
            "Sunshine inside Linux captures displays via Linux DMA-BUF ([cyan]EGL_EXT_image_dma_buf_import[/]) or DRM/KMS ([cyan]/dev/dri/card0[/]).\n\n" +
            "Under WSL2, the GPU is virtualized through Microsoft's DirectX kernel ([cyan]/dev/dxg[/]), meaning guest DMA-BUF export is not supported. " +
            "For ultra-low latency Sunshine streaming:\n" +
            "  1. [green bold]Recommended:[/] Install Sunshine natively on Windows (uses Windows Desktop Duplication API + NVENC).\n" +
            "  2. Use [green bold]Phase 3 (RDP Viewport)[/] for full desktop inside WSL2.\n" +
            "  3. Use [green bold]Phase 4 (WSLg)[/] for seamless native window integration on Windows."
        ))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(" Sunshine on WSL2 ")
        };

        AnsiConsole.Write(archPanel);
        ConsoleRenderer.PressEnterToContinue();
    }
}
