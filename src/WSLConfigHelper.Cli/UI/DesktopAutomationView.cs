using System.Diagnostics;
using Spectre.Console;
using WSLConfigHelper.Automation;
using WSLConfigHelper.Automation.DesktopEnvironments;
using WSLConfigHelper.Automation.DistroBases;
using WSLConfigHelper.Automation.Workstations;
using WSLConfigHelper.Core.Storage;

namespace WSLConfigHelper.Cli.UI;

public class DesktopAutomationView
{
    private readonly DistroManager _distroManager;
    private readonly GpuConfigurator _gpuConfigurator;
    private readonly ViewportManager _viewportManager;
    private readonly WslConfigFileService _configFileService;
    private readonly IWslProcessRunner _runner;
    private readonly WorkstationRegistry _registry;
    private WorkstationProfile _activeProfile;

    public DesktopAutomationView()
    {
        _runner = new WslProcessRunner();
        _distroManager = new DistroManager(_runner);
        _gpuConfigurator = new GpuConfigurator(_runner);
        _viewportManager = new ViewportManager(_runner);
        _configFileService = new WslConfigFileService();
        _registry = new WorkstationRegistry();
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
            grid.AddColumn(new GridColumn().PadRight(2));
            grid.AddColumn(new GridColumn());
            grid.AddRow("[grey]Workstation Profile:[/] [bold white]" + _activeProfile.DisplayName + "[/]", $"[grey]Distro Status:[/] {statusBadge}");
            grid.AddRow("[grey]WSL Instance Name:[/] [white]" + _activeProfile.DistroName + "[/]", $"[grey]RDP Port:[/] [bold cyan]{_activeProfile.DesktopEnvironment.DefaultRdpPort}[/]");
            grid.AddRow("[grey]Base Distribution:[/] [white]" + _activeProfile.DistroBase.DisplayName + $" ({_activeProfile.DistroBase.PackageManager})[/]", "[grey]GPU Backend:[/] [white]Mesa D3D12 (/dev/dxg)[/]");
            grid.AddRow("[grey]Desktop Shell:[/] [white]" + _activeProfile.DesktopEnvironment.DisplayName + $" ({_activeProfile.DesktopEnvironment.Protocol})[/]", "[grey]Default User:[/] [white]developer[/]");

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
                    "1. 🔍 Run Diagnostics & Hardware Audit (Checkpoints 1 & 2)",
                    $"2. 📦 Phase 1: Provision {_activeProfile.DistroName} & GPU-PV (Mesa D3D12)",
                    $"3. 🎨 Phase 2: Install {_activeProfile.DesktopEnvironment.DisplayName} & Audio",
                    $"4. 🖥️ Phase 3: Launch Full Desktop (RDP Viewport : {_activeProfile.DesktopEnvironment.DefaultRdpPort})",
                    $"5. 🪟 Phase 4: Launch Native {_activeProfile.DesktopEnvironment.DisplayName} Apps in WSLg",
                    "6. 🔄 Switch / Create Workstation Profile",
                    "7. ☀️ Phase 5: Sunshine Streaming Setup (Architecture Note)",
                    "8. 🚪 ← Back to Main Menu"
                );

            var choice = AnsiConsole.Prompt(menu);

            if (choice.StartsWith("1."))
            {
                await RunDiagnosticsAsync(distroExists);
            }
            else if (choice.StartsWith("2."))
            {
                await RunPhase1Async(distroExists);
            }
            else if (choice.StartsWith("3."))
            {
                await RunPhase2Async(distroExists);
            }
            else if (choice.StartsWith("4."))
            {
                await RunPhase3RdpAsync(distroExists);
            }
            else if (choice.StartsWith("5."))
            {
                await RunPhase4WslgAppsAsync(distroExists);
            }
            else if (choice.StartsWith("6."))
            {
                await SwitchOrCreateProfileAsync();
            }
            else if (choice.StartsWith("7."))
            {
                await RunPhase5SunshineAsync(distroExists);
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
            if (!AnsiConsole.Confirm($"Download and provision fresh WSL instance '[bold]{_activeProfile.DistroName}[/]' from {_activeProfile.DistroBase.DefaultWslImage}?", defaultValue: true))
            {
                return;
            }

            AnsiConsole.MarkupLine("[cyan]Starting download and installation via WSL... (this may take a few minutes)[/]");
            var installResult = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Downloading and registering {_activeProfile.DistroName}...", async ctx =>
                {
                    return await _distroManager.InstallDistroAsync(_activeProfile.DistroBase.DefaultWslImage, _activeProfile.DistroName, line =>
                    {
                        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
                    });
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
                    if (line.Contains("Installing:") || line.Contains("Complete!") || line.Contains("Setting up"))
                    {
                        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(line)}[/]");
                    }
                });
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

    private async Task RunPhase3RdpAsync(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold cyan]Phase 3: Launch Full Desktop ({_activeProfile.DesktopEnvironment.DisplayName} RDP Viewport)[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{_activeProfile.DistroName}' is not yet provisioned.[/] Complete Phase 1 and Phase 2 first.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        int defaultPort = _activeProfile.DesktopEnvironment.DefaultRdpPort;
        AnsiConsole.MarkupLine($"[grey]Deploys a virtual 60fps {_activeProfile.DesktopEnvironment.DisplayName} desktop exposed via {_activeProfile.DesktopEnvironment.Protocol} on port {defaultPort}.[/]\n");

        int width = AnsiConsole.Prompt(new TextPrompt<int>("Viewport Width (pixels):").DefaultValue(1920));
        int height = AnsiConsole.Prompt(new TextPrompt<int>("Viewport Height (pixels):").DefaultValue(1080));
        int port = AnsiConsole.Prompt(new TextPrompt<int>("RDP Port:").DefaultValue(defaultPort));

        AnsiConsole.MarkupLine("[cyan]Configuring viewport service and security keys...[/]");
        var options = new ViewportOptions(Width: width, Height: height, Port: port, User: "developer");
        await _activeProfile.DesktopEnvironment.ConfigureViewportServiceAsync(_activeProfile.DistroName, _runner, options);
        AnsiConsole.MarkupLine($"[green]✓ Viewport service configured for {_activeProfile.DesktopEnvironment.DisplayName} on port {port}![/]");

        // Generate Windows .rdp file and .cmd launcher
        await GenerateWindowsLaunchersAsync(_activeProfile.DistroName, port, width, height);

        var infoPanel = new Panel(new Markup(
            "[bold white]Remote Desktop Connection Info:[/]\n\n" +
            $"• Address:  [bold green]127.0.0.1:{port}[/]\n" +
            "• Username: [bold cyan]developer[/]\n" +
            "• Password: [bold cyan]developer[/]\n\n" +
            $"[grey]Generated launchers in current directory:[/] [cyan]{_activeProfile.DistroName}.rdp[/], [cyan]connect-{_activeProfile.DistroName.ToLowerInvariant()}.cmd[/]\n" +
            "[grey]Hardware acceleration is active via Mesa D3D12 (RTX 4080).[/]"
        ))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader($" {_activeProfile.DesktopEnvironment.DisplayName} RDP Viewport ")
        };
        AnsiConsole.Write(infoPanel);
        AnsiConsole.WriteLine();

        if (AnsiConsole.Confirm($"Open Windows Remote Desktop (mstsc) to 127.0.0.1:{port} now?", defaultValue: true))
        {
            AnsiConsole.MarkupLine("[cyan]Opening Windows Remote Desktop Connection (mstsc.exe)...[/]");
            _viewportManager.LaunchWindowsMstsc($"{_activeProfile.DistroName}.rdp");
            AnsiConsole.MarkupLine("[green]✓ Client launched! Enter 'developer' / 'developer' when prompted.[/]");
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task GenerateWindowsLaunchersAsync(string distroName, int port, int width, int height)
    {
        try
        {
            var rdpContent = $"""
                full address:s:127.0.0.1:{port}
                username:s:developer
                prompt for credentials:i:1
                desktopwidth:i:{width}
                desktopheight:i:{height}
                session bpp:i:32
                audiomode:i:0
                smart sizing:i:1
                screen mode id:i:2
                use multimon:i:0
                connection type:i:7
                networkautodetect:i:1
                bandwidthautodetect:i:1
                """;

            await File.WriteAllTextAsync($"{distroName}.rdp", rdpContent);

            var cmdContent = $"""
                @echo off
                echo Waking up {distroName}...
                wsl -d {distroName} -u developer -- true
                echo Connecting to Remote Desktop on port {port}...
                start mstsc {distroName}.rdp
                """;

            await File.WriteAllTextAsync($"connect-{distroName.ToLowerInvariant()}.cmd", cmdContent);
        }
        catch
        {
            // Best effort convenience generation
        }
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
            menuChoices.Add($"{p.DisplayName} [{p.DistroName} : {p.DesktopEnvironment.DefaultRdpPort}]{activeMarker}");
        }
        menuChoices.Add("🛠️ Create Custom Profile (Mix & Match Distro Base + Desktop)");
        menuChoices.Add("← Cancel");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Choose an existing profile to activate, or create a custom profile:[/]")
                .PageSize(10)
                .AddChoices(menuChoices)
        );

        if (choice == "← Cancel")
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
                    .AddChoices(des.Select(d => $"{d.DisplayName} (Port {d.DefaultRdpPort})"))
            );
            var selectedDe = des.First(d => deChoice.StartsWith(d.DisplayName));

            AnsiConsole.MarkupLine("\n[bold cyan]3. Enter WSL Instance Name:[/]");
            string defaultName = $"{selectedBase.Id.ToUpperInvariant()}-{selectedDe.Id.ToUpperInvariant()}";
            string customName = AnsiConsole.Prompt(
                new TextPrompt<string>("WSL Distribution Instance Name:")
                    .DefaultValue(defaultName)
            );

            var newProfile = _registry.CreateCustomProfile(selectedBase, selectedDe, customName);
            _activeProfile = newProfile;
            AnsiConsole.MarkupLine($"\n[green bold]✓ Custom profile created and activated: {newProfile.DisplayName}![/]");
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
