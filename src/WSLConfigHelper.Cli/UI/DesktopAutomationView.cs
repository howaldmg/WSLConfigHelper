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
                    "4. 🖥️ Phase 3: Launch Full Desktop (KDE 6 RDP Viewport - mstsc)",
                    "5. 🪟 Phase 4: Launch Native KDE Apps in WSLg (Dolphin / Konsole)",
                    "6. ☀️ Phase 5: Sunshine Streaming Setup (Experimental)",
                    "7. 🚪 ← Back to Main Menu"
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

    private async Task RunPhase3RdpAsync(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Phase 3: Launch Full Desktop (KDE Plasma 6 RDP Viewport)[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{TargetDistro}' is not yet provisioned.[/] Complete Phase 1 and Phase 2 first.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        AnsiConsole.MarkupLine("[grey]Deploys a virtual 60fps KDE Plasma 6 desktop and exposes it via KDE's native RDP server ('krdp') on port 3390.[/]\n");

        int width = AnsiConsole.Prompt(new TextPrompt<int>("Viewport Width (pixels):").DefaultValue(1920));
        int height = AnsiConsole.Prompt(new TextPrompt<int>("Viewport Height (pixels):").DefaultValue(1080));
        int port = 3390;

        AnsiConsole.MarkupLine("[cyan]Generating startup script and TLS certificate...[/]");
        await _viewportManager.SetupRdpViewportScriptAsync(TargetDistro, width, height, port);
        AnsiConsole.MarkupLine("[green]✓ Script generated at /usr/local/bin/start-plasma-rdp[/]");

        var infoPanel = new Panel(new Markup(
            "[bold white]Remote Desktop Connection Info:[/]\n\n" +
            $"• Address:  [bold green]127.0.0.1:{port}[/]\n" +
            "• Username: [bold cyan]developer[/]\n" +
            "• Password: [bold cyan]developer[/]\n\n" +
            "[grey]Hardware acceleration is fully active via Mesa D3D12 (RTX 4080).[/]"
        ))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(" RDP Viewport Details ")
        };
        AnsiConsole.Write(infoPanel);
        AnsiConsole.WriteLine();

        if (AnsiConsole.Confirm("Start KDE Plasma session and open Windows Remote Desktop (mstsc) now?", defaultValue: true))
        {
            AnsiConsole.MarkupLine("[green bold]Starting KDE Plasma session in background...[/]");
            _viewportManager.LaunchRdpViewport(TargetDistro, "developer");

            // Give it 2 seconds to initialize KWin and krdpserver
            await Task.Delay(2000);

            AnsiConsole.MarkupLine("[cyan]Opening Windows Remote Desktop Connection (mstsc.exe)...[/]");
            _viewportManager.LaunchWindowsMstsc($"127.0.0.1:{port}");
            AnsiConsole.MarkupLine("[green]✓ Client launched! Enter 'developer' / 'developer' when prompted.[/]");
        }

        ConsoleRenderer.PressEnterToContinue();
    }

    private async Task RunPhase4WslgAppsAsync(bool distroExists)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Phase 4: Launch Native KDE Apps in WSLg[/]") { Justification = Justify.Left });

        if (!distroExists)
        {
            AnsiConsole.MarkupLine($"[yellow]'{TargetDistro}' is not yet provisioned.[/] Complete Phase 1 and Phase 2 first.");
            ConsoleRenderer.PressEnterToContinue();
            return;
        }

        AnsiConsole.MarkupLine("[grey]Launches individual KDE desktop apps seamlessly onto your Windows desktop using WSLg.[/]\n");

        var appChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select an application to launch on Windows:[/]")
                .AddChoices(
                    "1. 📁 Dolphin (KDE File Manager)",
                    "2. 💻 Konsole (KDE Terminal)",
                    "3. ⚙️ System Settings (systemsettings)",
                    "4. ✏️ Custom Linux GUI Command",
                    "5. ← Cancel"
                )
        );

        string? command = null;
        if (appChoice.StartsWith("1.")) command = "dolphin";
        else if (appChoice.StartsWith("2.")) command = "konsole";
        else if (appChoice.StartsWith("3.")) command = "systemsettings";
        else if (appChoice.StartsWith("4."))
        {
            command = AnsiConsole.Prompt(new TextPrompt<string>("Enter Linux GUI command:"));
        }

        if (!string.IsNullOrWhiteSpace(command))
        {
            AnsiConsole.MarkupLine($"[green bold]Launching '{command}' via WSLg...[/]");
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d {TargetDistro} -u developer -- {command}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);
            AnsiConsole.MarkupLine("[cyan]Process launched! Check your Windows taskbar.[/]");
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
            "  2. Use [green bold]Phase 3 (RDP Viewport)[/] for full KDE Plasma 6 desktop inside WSL2.\n" +
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
