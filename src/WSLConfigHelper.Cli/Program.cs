using Spectre.Console;
using WSLConfigHelper.Automation;
using WSLConfigHelper.Automation.DesktopEnvironments;
using WSLConfigHelper.Automation.Workstations;
using WSLConfigHelper.Core.Storage;

namespace WSLConfigHelper.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length > 0)
        {
            var firstArg = args[0].ToLowerInvariant();
            switch (firstArg)
            {
                case "--help":
                case "-h":
                case "help":
                    PrintHelp();
                    return 0;

                case "--license":
                    PrintLicense();
                    return 0;

                case "clean-launchers":
                    return HandleCleanLaunchers();

                case "status":
                    return await HandleStatusAsync();

                case "launch":
                    return await HandleLaunchAsync(args.Skip(1).ToArray());

                case "teardown":
                    return await HandleTeardownAsync(args.Skip(1).ToArray());

                case "provision":
                    return await HandleProvisionAsync(args.Skip(1).ToArray());
            }
        }

        string? customConfigPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--config" || args[i] == "-c") && i + 1 < args.Length)
            {
                customConfigPath = args[++i];
            }
        }

        try
        {
            var fileService = new WslConfigFileService(customConfigPath);
            var app = new AppController(fileService);
            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static int HandleCleanLaunchers()
    {
        var count = 0;
        var dir = Directory.GetCurrentDirectory();
        var cmdFiles = Directory.GetFiles(dir, "launch-*-wslg.cmd")
            .Concat(Directory.GetFiles(dir, "connect-*.cmd"));
        var rdpFiles = Directory.GetFiles(dir, "*.rdp");

        foreach (var file in cmdFiles.Concat(rdpFiles))
        {
            try
            {
                File.Delete(file);
                count++;
                AnsiConsole.MarkupLine($"[grey]Deleted:[/] [cyan]{Path.GetFileName(file)}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error deleting {Path.GetFileName(file)}:[/] {ex.Message}");
            }
        }

        if (count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No generated launcher (.cmd / .rdp) files found to clean.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green bold]✓ Cleaned {count} launcher file(s).[/]");
        }

        return 0;
    }

    private static async Task<int> HandleStatusAsync()
    {
        var runner = new WslProcessRunner();
        var distroManager = new DistroManager(runner);
        var gpuConfigurator = new GpuConfigurator(runner);
        var registry = new WorkstationRegistry();

        var installedDistros = await distroManager.ListDistrosAsync();

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.AddColumn("[bold]Profile[/]");
        table.AddColumn("[bold]WSL Instance[/]");
        table.AddColumn("[bold]Desktop Shell[/]");
        table.AddColumn("[bold]Status[/]");
        table.AddColumn("[bold]RDP Port[/]");

        foreach (var profile in registry.GetAllProfiles())
        {
            var match = installedDistros.FirstOrDefault(d => string.Equals(d.Name, profile.DistroName, StringComparison.OrdinalIgnoreCase));
            var statusBadge = match != null
                ? (match.State.Equals("Running", StringComparison.OrdinalIgnoreCase) ? "[green bold]RUNNING[/]" : "[yellow]STOPPED[/]")
                : "[grey]NOT INSTALLED[/]";

            table.AddRow(
                profile.DisplayName,
                profile.DistroName,
                profile.DesktopEnvironment.DisplayName,
                statusBadge,
                profile.RdpPort.ToString()
            );
        }

        AnsiConsole.Write(new Rule("[bold cyan]WSLConfigHelper Workstations & Hardware Status[/]") { Justification = Justify.Left });
        AnsiConsole.Write(table);

        var firstRunning = installedDistros.FirstOrDefault(d => d.State.Equals("Running", StringComparison.OrdinalIgnoreCase));
        if (firstRunning != null)
        {
            var gpuReport = await gpuConfigurator.RunGpuDiagnosticsAsync(firstRunning.Name);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Active GPU Report ({firstRunning.Name}):[/]");
            AnsiConsole.MarkupLine($"  • Direct Rendering: {(gpuReport.DirectRenderingEnabled ? "[green]ENABLED[/]" : "[yellow]DISABLED[/]")}");
            AnsiConsole.MarkupLine($"  • Adapter:          [cyan]{Markup.Escape(gpuReport.OpenGLRenderer ?? "N/A")}[/]");
            AnsiConsole.MarkupLine($"  • OpenGL:           [cyan]{Markup.Escape(gpuReport.OpenGLVersion ?? "N/A")}[/]");
        }

        return 0;
    }

    private static async Task<int> HandleLaunchAsync(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: Missing workstation profile or distro name.[/]");
            AnsiConsole.MarkupLine("Usage: [white]wslconfig-helper launch <profile-or-distro> [--rdp][/]");
            return 1;
        }

        var target = args[0];
        bool useRdp = args.Any(a => a.Equals("--rdp", StringComparison.OrdinalIgnoreCase));

        var registry = new WorkstationRegistry();
        var profile = registry.GetAllProfiles().FirstOrDefault(p =>
            string.Equals(p.Id, target, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.DistroName, target, StringComparison.OrdinalIgnoreCase));

        if (profile == null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Unknown profile or distro '{target}'.[/]");
            AnsiConsole.MarkupLine("Available profiles: " + string.Join(", ", registry.GetAllProfiles().Select(p => p.DistroName)));
            return 1;
        }

        var runner = new WslProcessRunner();
        var distroManager = new DistroManager(runner);
        if (!await distroManager.DistroExistsAsync(profile.DistroName))
        {
            AnsiConsole.MarkupLine($"[red]Error: WSL instance '{profile.DistroName}' is not installed.[/]");
            AnsiConsole.MarkupLine($"Run: [white]wslconfig-helper provision {profile.DistroName} --with-desktop[/]");
            return 1;
        }

        var viewportManager = new ViewportManager(runner);
        if (useRdp)
        {
            AnsiConsole.MarkupLine($"[cyan]Connecting to {profile.DistroName} via RDP (port {profile.RdpPort})...[/]");
            viewportManager.LaunchWindowsMstsc($"127.0.0.1:{profile.RdpPort}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[cyan]Launching {profile.DistroName} ({profile.DesktopEnvironment.DisplayName}) at native monitor refresh rate via WSLg...[/]");
            viewportManager.LaunchWslgViewport(profile.DistroName, "developer", profile.DesktopEnvironment.NativeWslgScriptPath);
        }

        AnsiConsole.MarkupLine("[green bold]✓ Launch initiated![/]");
        return 0;
    }

    private static async Task<int> HandleTeardownAsync(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: Missing workstation profile or distro name.[/]");
            AnsiConsole.MarkupLine("Usage: [white]wslconfig-helper teardown <profile-or-distro> [--force][/]");
            return 1;
        }

        var target = args[0];
        bool force = args.Any(a => a.Equals("--force", StringComparison.OrdinalIgnoreCase) || a.Equals("-f", StringComparison.OrdinalIgnoreCase));

        var registry = new WorkstationRegistry();
        var profile = registry.GetAllProfiles().FirstOrDefault(p =>
            string.Equals(p.Id, target, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.DistroName, target, StringComparison.OrdinalIgnoreCase));

        string distroName = profile?.DistroName ?? target;

        var runner = new WslProcessRunner();
        var distroManager = new DistroManager(runner);

        if (!await distroManager.DistroExistsAsync(distroName))
        {
            AnsiConsole.MarkupLine($"[yellow]WSL instance '{distroName}' does not exist or is already uninstalled.[/]");
            return 0;
        }

        if (!force)
        {
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
                AnsiConsole.MarkupLine("[grey]Teardown aborted. No changes were made.[/]");
                return 0;
            }
        }

        AnsiConsole.MarkupLine($"[cyan]Terminating and unregistering {distroName}...[/]");
        await distroManager.UnregisterDistroAsync(distroName);

        // Clean launchers
        var filesToDelete = new[]
        {
            $"launch-{distroName.ToLowerInvariant()}-wslg.cmd",
            $"connect-{distroName.ToLowerInvariant()}.cmd",
            $"{distroName}.rdp",
            $"{distroName.ToUpperInvariant()}.rdp"
        };
        foreach (var file in filesToDelete)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }

        AnsiConsole.MarkupLine($"[green bold]✓ {distroName} has been completely uninstalled and cleaned up.[/]");
        return 0;
    }

    private static async Task<int> HandleProvisionAsync(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: Missing workstation profile or distro name.[/]");
            AnsiConsole.MarkupLine("Usage: [white]wslconfig-helper provision <profile-or-distro> [--with-desktop][/]");
            return 1;
        }

        var target = args[0];
        bool withDesktop = args.Any(a => a.Equals("--with-desktop", StringComparison.OrdinalIgnoreCase) || a.Equals("--full", StringComparison.OrdinalIgnoreCase));

        var registry = new WorkstationRegistry();
        var profile = registry.GetAllProfiles().FirstOrDefault(p =>
            string.Equals(p.Id, target, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.DistroName, target, StringComparison.OrdinalIgnoreCase));

        if (profile == null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Unknown profile '{target}'.[/]");
            AnsiConsole.MarkupLine("Available profiles: " + string.Join(", ", registry.GetAllProfiles().Select(p => p.DistroName)));
            return 1;
        }

        var runner = new WslProcessRunner();
        var distroManager = new DistroManager(runner);

        AnsiConsole.MarkupLine($"[cyan bold]Phase 1: Provisioning {profile.DistroName} ({profile.DistroBase.DisplayName})...[/]");
        var installResult = await distroManager.InstallDistroAsync(profile.DistroBase.DefaultWslImage, profile.DistroName, line =>
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
        });

        if (!installResult.Success)
        {
            AnsiConsole.MarkupLine($"[red bold]Installation failed:[/] {Markup.Escape(installResult.StandardError)}");
            return 1;
        }

        AnsiConsole.MarkupLine("[cyan]Configuring /etc/wsl.conf and systemd...[/]");
        await profile.DistroBase.ConfigureWslConfAsync(profile.DistroName, defaultUser: "developer", runner);

        AnsiConsole.MarkupLine("[cyan]Configuring GPU acceleration & Mesa D3D12 drivers...[/]");
        await profile.DistroBase.ConfigureGpuAccelerationAsync(profile.DistroName, runner);

        AnsiConsole.MarkupLine("[cyan]Setting up 'developer' user...[/]");
        await profile.DistroBase.EnsureUserAsync(profile.DistroName, "developer", runner);

        // Configure WSLg native script
        var viewportManager = new ViewportManager(runner);
        var options = new ViewportOptions(1920, 1080, profile.RdpPort);
        await profile.DesktopEnvironment.ConfigureNativeWslgViewportAsync(profile.DistroName, runner, options);

        if (withDesktop)
        {
            AnsiConsole.MarkupLine($"\n[cyan bold]Phase 2: Installing {profile.DesktopEnvironment.DisplayName} & audio stack...[/]");
            var packages = profile.DesktopEnvironment.GetPackageList(profile.DistroBase.PackageManager);
            await profile.DistroBase.InstallPackagesAsync(profile.DistroName, packages, runner, line =>
            {
                if (line.Contains("Installing:") || line.Contains("Complete!") || line.Contains("Setting up") ||
                    line.Contains("download 0 B") || line.Contains("Already downloaded") || line.Contains("[Cache"))
                {
                    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(line)}[/]");
                }
            });
            AnsiConsole.MarkupLine($"[green bold]✓ {profile.DesktopEnvironment.DisplayName} packages installed successfully![/]");
        }

        AnsiConsole.MarkupLine($"\n[green bold]✓ Workstation '{profile.DistroName}' successfully provisioned![/]");
        if (!withDesktop)
        {
            AnsiConsole.MarkupLine($"[grey]To install the full desktop environment later, run: [white]wslconfig-helper provision {profile.DistroName} --with-desktop[/][/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[grey]To launch: [white]wslconfig-helper launch {profile.DistroName}[/][/]");
        }

        return 0;
    }

    private static void PrintHelp()
    {
        AnsiConsole.MarkupLine("[bold cyan]WSLConfigHelper[/] - Interactive Spectre.Console manager for WSL configuration and workstations");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Usage:[/] [white]wslconfig-helper [[command]] [[options]][/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Commands:[/] (Launches interactive TUI if omitted)");
        AnsiConsole.MarkupLine("  launch <distro> [--rdp]       Launch workstation desktop (WSLg native viewport by default, or --rdp)");
        AnsiConsole.MarkupLine("  provision <distro> [--with-desktop] Non-interactively provision base distro and optional full desktop");
        AnsiConsole.MarkupLine("  teardown <distro> [--force]   Guarded unregister & deletion of a workstation instance");
        AnsiConsole.MarkupLine("  status                        Show status table of all workstations and GPU-PV state");
        AnsiConsole.MarkupLine("  clean-launchers               Remove orphaned .cmd and .rdp launcher files in working directory");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Global Options:[/]");
        AnsiConsole.MarkupLine("  -c, --config <path>           Specify a custom path to .wslconfig (default: %USERPROFILE%\\.wslconfig)");
        AnsiConsole.MarkupLine("  --license                     Display license information (GNU GPLv3)");
        AnsiConsole.MarkupLine("  -h, --help                    Show help and usage information");
    }

    private static void PrintLicense()
    {
        AnsiConsole.MarkupLine("[bold]WSLConfigHelper[/] is licensed under the [cyan bold]GNU General Public License v3.0 (GPL-3.0)[/].");
        AnsiConsole.MarkupLine("This program comes with ABSOLUTELY NO WARRANTY. You are free to distribute and modify it under the terms of the GPLv3.");
    }
}
