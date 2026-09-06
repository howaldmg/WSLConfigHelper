using System.Diagnostics;
using Spectre.Console;
using WSLConfigHelper.Automation;
using WSLConfigHelper.Core.Storage;

namespace WSLConfigHelper.Cli.UI;

public class DesktopAutomationView
{
    private const string TargetDistro = "Fedora-Desktop";
    private readonly DistroManager _distroManager;
    private readonly GpuConfigurator _gpuConfigurator;
    private readonly PlasmaProvisioner _plasmaProvisioner;
    private readonly ViewportManager _viewportManager;
    private readonly WslConfigFileService _configFileService;

    public DesktopAutomationView()
    {
        var runner = new WslProcessRunner();
        _distroManager = new DistroManager(runner);
        _gpuConfigurator = new GpuConfigurator(runner);
        _plasmaProvisioner = new PlasmaProvisioner(runner);
        _viewportManager = new ViewportManager(runner);
        _configFileService = new WslConfigFileService();
    }

    public async Task ShowAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            var rule = new Rule("[bold cyan]WSL Desktop Automation & Viewport[/] [grey](KDE Plasma 6 + GPU-PV)[/]")
            {
                Justification = Justify.Left
            };
            AnsiConsole.Write(rule);

            bool distroExists = await _distroManager.DistroExistsAsync(TargetDistro);
            var statusBadge = distroExists ? "[green bold]Installed / Available[/]" : "[yellow]Not Yet Provisioned[/]";

            var grid = new Grid();
            grid.AddColumn(new GridColumn().PadRight(2));
            grid.AddColumn(new GridColumn());
            grid.AddRow("[grey]Target Distribution:[/] [bold white]" + TargetDistro + "[/]", $"[grey]Status:[/] {statusBadge}");
            grid.AddRow("[grey]Target Desktop:[/] [white]KDE Plasma 6 (Full)[/]", "[grey]GPU Backend:[/] [white]Mesa D3D12 (DirectX 12 via /dev/dxg)[/]");

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
                    "2. 📦 Phase 1: Provision Fedora-Desktop & GPU-PV (Mesa D3D12)",
                    "3. 🎨 Phase 2: Install Full KDE Plasma Desktop & Sound",
                    "4. 🪟 Phase 3: Launch Viewport (WSLg Direct Nested Window)",
                    "5. ☀️ Phase 4: Configure Sunshine + Headless Virtual Display",
                    "6. 🚪 ← Back to Main Menu"
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
                await RunPhase3Async(distroExists);
            }
            else if (choice.StartsWith("5."))
            {
                await RunPhase4Async(distroExists);
            }
            else if (choice.StartsWith("6."))
            {
                break;
            }
        }
    }

    private async Task RunDiagnosticsAsync(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]System Diagnostics & Hardware Audit[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{TargetDistro}' is not yet provisioned.[/] Run Phase 1 from the menu to create it.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Auditing GPU-PV and Plasma subsystems...", async ctx =>
            {
                ctx.Status("Checking /dev/dxg device...");
                var gpuReport = await _gpuConfigurator.RunGpuDiagnosticsAsync(TargetDistro);

                ctx.Status("Checking Plasma & KWin components...");
                var plasmaReport = await _plasmaProvisioner.RunPlasmaDiagnosticsAsync(TargetDistro);

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
                    "KDE KWin Wayland Compositor",
                    plasmaReport.KWinInstalled ? "[green]INSTALLED[/]" : "[yellow]NOT INSTALLED[/]",
                    plasmaReport.KWinVersion ?? "Run Phase 2 to install KDE Plasma"
                );

                table.AddRow(
                    "PipeWire Audio Server",
                    plasmaReport.PipeWireInstalled ? "[green]AVAILABLE[/]" : "[yellow]NOT INSTALLED[/]",
                    plasmaReport.PipeWireInstalled ? "Ready for desktop & Sunshine streaming" : "Run Phase 2 to install"
                );

                table.AddRow(
                    "Developer User (sudo)",
                    plasmaReport.SudoConfigured ? "[green]CONFIGURED[/]" : "[yellow]NOT CONFIGURED[/]",
                    plasmaReport.SudoConfigured ? "Passwordless wheel sudo enabled" : "Run Phase 2 to configure"
                );

                AnsiConsole.Write(table);
            });

        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task RunPhase1Async(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Phase 1: Provision Fedora-Desktop & Configure GPU-PV[/]") { Justification = Justify.Left });
        AnsiConsole.MarkupLine("[grey]This phase will install FedoraLinux-43 as 'Fedora-Desktop', enable systemd, and configure Mesa D3D12.[/]\n");

        if (!distroExists)
        {
            if (!AnsiConsole.Confirm($"Download and provision fresh WSL instance '[bold]{TargetDistro}[/]' from FedoraLinux-43?", defaultValue: true))
            {
                return;
            }

            AnsiConsole.MarkupLine("[cyan]Starting download and installation via WSL... (this may take a few minutes)[/]");
            var installResult = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Downloading and registering Fedora-Desktop...", async ctx =>
                {
                    return await _distroManager.InstallDistroAsync("FedoraLinux-43", TargetDistro, line =>
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

            AnsiConsole.MarkupLine("[green bold]✓ Distribution 'Fedora-Desktop' successfully registered![/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]'{TargetDistro}' already exists.[/] Proceeding to GPU and system configuration...");
        }

        AnsiConsole.MarkupLine("[cyan]Configuring /etc/wsl.conf and systemd...[/]");
        await _gpuConfigurator.ConfigureWslConfAsync(TargetDistro, defaultUser: "developer");

        AnsiConsole.MarkupLine("[cyan]Configuring dynamic linker & Mesa D3D12 environment...[/]");
        await _gpuConfigurator.ConfigureGpuEnvironmentAsync(TargetDistro);

        if (AnsiConsole.Confirm("Install Mesa D3D12 drivers and glx-utils via dnf now?", defaultValue: true))
        {
            AnsiConsole.MarkupLine("[cyan]Running dnf install (mesa-dri-drivers, mesa-vulkan-drivers, glx-utils)...[/]");
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Installing graphics drivers...", async ctx =>
                {
                    var res = await _gpuConfigurator.InstallMesaDriversAsync(TargetDistro, line =>
                    {
                        if (line.StartsWith("[stderr]"))
                            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(line)}[/]");
                    });
                });

            AnsiConsole.MarkupLine("[green bold]✓ Graphics driver packages installed![/]");
        }

        // Restart instance to ensure systemd and drivers load cleanly
        AnsiConsole.MarkupLine("[cyan]Restarting Fedora-Desktop to apply configuration...[/]");
        await _distroManager.TerminateDistroAsync(TargetDistro);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]Checkpoint 1 Verification:[/]");
        var gpuReport = await _gpuConfigurator.RunGpuDiagnosticsAsync(TargetDistro);

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
        AnsiConsole.Write(new Rule("[bold cyan]Phase 2: Install Full KDE Plasma Desktop Environment[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{TargetDistro}' is not yet provisioned.[/] Run Phase 1 first.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        AnsiConsole.MarkupLine("[grey]This will set up the 'developer' user (wheel sudo), and install the full @kde-desktop-environment package group.[/]\n");

        if (!AnsiConsole.Confirm("Begin Phase 2 installation?", defaultValue: true))
        {
            return;
        }

        AnsiConsole.MarkupLine("[cyan]Configuring unprivileged 'developer' user with passwordless sudo...[/]");
        await _plasmaProvisioner.EnsureUserAsync(TargetDistro, "developer");
        AnsiConsole.MarkupLine("[green]✓ User 'developer' configured.[/]");

        AnsiConsole.MarkupLine("[cyan]Starting dnf installation of KDE Plasma, PipeWire, and fonts...[/]");
        AnsiConsole.MarkupLine("[grey]Note: The full KDE desktop group contains extensive tools and will take a few minutes to download and assemble.[/]");

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Installing KDE Plasma desktop environment...", async ctx =>
            {
                var result = await _plasmaProvisioner.InstallPlasmaDesktopAsync(TargetDistro, fullEnvironment: true, line =>
                {
                    if (line.Contains("Installing:") || line.Contains("Complete!"))
                    {
                        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(line)}[/]");
                    }
                });
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]Checkpoint 2 Verification:[/]");
        var plasmaReport = await _plasmaProvisioner.RunPlasmaDiagnosticsAsync(TargetDistro);

        if (plasmaReport.KWinInstalled)
        {
            AnsiConsole.MarkupLine($"[green bold]✓ KDE Plasma & KWin Installed:[/] [bold cyan]{Markup.Escape(plasmaReport.KWinVersion ?? "KWin Wayland")}[/]");
            AnsiConsole.MarkupLine($"[green]✓ PipeWire Audio:[/] {(plasmaReport.PipeWireInstalled ? "Ready" : "Missing")}");
            AnsiConsole.MarkupLine("[green bold]✓ Phase 2 complete! Ready for Viewport setup.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]KWin Wayland was not detected. Please inspect package installation logs.[/]");
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task RunPhase3Async(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Phase 3: Viewport Step 1 - WSLg Direct Nested Window[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{TargetDistro}' is not yet provisioned.[/] Complete Phase 1 and Phase 2 first.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        AnsiConsole.MarkupLine("[grey]Generates '/usr/local/bin/start-plasma-wslg' and launches KDE Plasma directly inside a native Windows desktop window using WSLg.[/]\n");

        var config = _configFileService.Load();
        var guiValue = config.GetValue("wsl2", "guiApplications");
        bool guiDisabled = string.Equals(guiValue, "false", StringComparison.OrdinalIgnoreCase);

        if (guiDisabled)
        {
            AnsiConsole.MarkupLine("[yellow bold]Notice:[/] Your [cyan]%USERPROFILE%\\.wslconfig[/] currently has [bold red]guiApplications=false[/].");
            AnsiConsole.MarkupLine("[grey]WSLg requires 'guiApplications=true' to initialize host window surfaces.[/]\n");

            if (AnsiConsole.Confirm("Would you like WSLConfigHelper to update guiApplications=true now?", defaultValue: true))
            {
                config.SetValue("wsl2", "guiApplications", "true");
                _configFileService.Save(config);
                AnsiConsole.MarkupLine("[green bold]✓ Updated .wslconfig! (Backup created at .wslconfig.bak)[/]");
                AnsiConsole.MarkupLine("[yellow bold]Important:[/] A full WSL restart ([cyan]wsl --shutdown[/]) is required before WSLg will become active.\n");
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]Note: You can alternatively use Phase 4 (Sunshine), which does not require WSLg.[/]\n");
            }
        }

        int width = AnsiConsole.Prompt(new TextPrompt<int>("Window Width (pixels):").DefaultValue(1920));
        int height = AnsiConsole.Prompt(new TextPrompt<int>("Window Height (pixels):").DefaultValue(1080));

        AnsiConsole.MarkupLine("[cyan]Generating startup script...[/]");
        await _viewportManager.SetupWslgViewportScriptAsync(TargetDistro, width, height);
        AnsiConsole.MarkupLine("[green]✓ Script generated at /usr/local/bin/start-plasma-wslg[/]");

        if (AnsiConsole.Confirm("Launch KDE Plasma desktop window now?", defaultValue: true))
        {
            AnsiConsole.MarkupLine("[green bold]Launching KDE Plasma inside WSLg nested Wayland window...[/]");
            _viewportManager.LaunchWslgViewport(TargetDistro, "developer");
            AnsiConsole.MarkupLine("[cyan]Desktop process launched! Look for the KDE Plasma window on your Windows desktop.[/]");
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task RunPhase4Async(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Phase 4: Viewport Step 2 - Sunshine + Virtual Display[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{TargetDistro}' is not yet provisioned.[/] Complete Phase 1 and Phase 2 first.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        AnsiConsole.MarkupLine("[grey]Configures KWin's headless virtual display and installs Sunshine for ultra-low-latency 60-120fps NVENC streaming to Moonlight.[/]\n");

        int width = AnsiConsole.Prompt(new TextPrompt<int>("Virtual Display Width:").DefaultValue(2560));
        int height = AnsiConsole.Prompt(new TextPrompt<int>("Virtual Display Height:").DefaultValue(1440));

        AnsiConsole.MarkupLine("[cyan]Configuring headless KWin script and Sunshine packages...[/]");
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Setting up Sunshine headless streaming...", async ctx =>
            {
                await _viewportManager.SetupSunshineHeadlessScriptAsync(TargetDistro, width, height);
            });

        AnsiConsole.MarkupLine("[green bold]✓ Sunshine headless script configured at /usr/local/bin/start-plasma-sunshine[/]");
        AnsiConsole.WriteLine();

        var infoPanel = new Panel(new Markup(
            "[bold white]Connecting via Moonlight:[/]\n\n" +
            "1. Start the headless Plasma session in Fedora-Desktop:\n" +
            "   [cyan]wsl -d Fedora-Desktop -u developer -- /usr/local/bin/start-plasma-sunshine[/]\n\n" +
            "2. Open the Sunshine web configuration portal on Windows at:\n" +
            "   [bold green]https://localhost:47990[/]\n\n" +
            "3. Open [bold]Moonlight[/] on Windows, add [cyan]127.0.0.1[/], enter the PIN, and enjoy 60-120 FPS GPU-accelerated KDE Plasma!"
        ))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(" Sunshine Stream Instructions ")
        };

        AnsiConsole.Write(infoPanel);

        if (AnsiConsole.Confirm("Launch Sunshine headless desktop session in background now?", defaultValue: false))
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d {TargetDistro} -u developer -- /usr/local/bin/start-plasma-sunshine",
                UseShellExecute = false,
                CreateNoWindow = false
            };
            Process.Start(psi);
            AnsiConsole.MarkupLine("[green bold]✓ Sunshine session started![/] Navigate to [bold cyan]https://localhost:47990[/] to configure pairing.");
        }

        ConsoleRenderer.PressEnterToContinue();
    }
}
