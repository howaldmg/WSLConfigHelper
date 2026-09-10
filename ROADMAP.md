# WSLConfigHelper Roadmap

This roadmap outlines architectural enhancements, feature expansions, and technical improvements planned for **WSLConfigHelper**. Priorities are organized into thematic phases designed to increase hardware compatibility, automation flexibility, and scriptability.

---

## 🗺️ High-Level Milestones

| Milestone | Focus Area | Status | Target Version |
| :--- | :--- | :---: | :---: |
| **Phase 1** | Hardware-Agnostic GPU & Multi-Vendor Acceleration | 📋 Planned | v1.1.0 |
| **Phase 2** | Package Manager Decoupling & Distro Extensibility | 📋 Planned | v1.1.0 |
| **Phase 3** | Headless / Non-Interactive Scripting CLI | 📋 Planned | v1.2.0 |
| **Phase 4** | Workstation Security & Identity Customization | 📋 Planned | v1.3.0 |
| **Phase 5** | CI/CD, Documentation & Project Maintenance | 🔄 In Progress | v1.0.1 |

---

## Phase 1: Hardware-Agnostic GPU & Multi-Vendor Acceleration

Currently, Mesa D3D12 GPU pass-through hardcodes `MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA`. Systems with AMD Radeon, Intel Arc, or hybrid graphics setups should experience seamless hardware-accelerated OpenGL and Vulkan rendering without manual configuration.

- [ ] **Host GPU Detection via `IHostSystemProber`**
  - Query Windows host GPU devices using WMI (`Win32_VideoController`) or DXGI device enumerators.
  - Detect primary rendering adapter vendor (`NVIDIA`, `AMD`, `Intel`, or software fallback).
  - Add GPU info to `HostHardwareMetrics` (device name, vendor ID, dedicated VRAM).
- [ ] **Dynamic Mesa Environment Injection**
  - Omit `MESA_D3D12_DEFAULT_ADAPTER_NAME` by default when only a single primary GPU exists (allowing Mesa to pick the default D3D12 adapter automatically).
  - If multiple discrete/integrated GPUs are present, dynamically populate `/etc/profile.d/wsl-gpu.sh` with the detected high-performance adapter or provide an interactive selector.
- [ ] **Expanded Vulkan & DirectML Verification**
  - Extend `GpuConfigurator` diagnostics to probe `/usr/lib/wsl/drivers` for DirectX DirectML acceleration and `vulkaninfo --summary` compatibility.

---

## Phase 2: Package Manager Decoupling & Distro Extensibility

Refactor the automation layer to completely decouple distro-specific packaging logic from high-level configurators.

- [ ] **Consolidate GPU Package Installation into `IDistroBase`**
  - Remove direct `dnf install` invocation from `GpuConfigurator.InstallMesaDriversAsync`.
  - Delegate all package installation exclusively through `IDistroBase.ConfigureGpuAccelerationAsync(distro, runner)`.
- [ ] **Standardize Desktop Package Profiles**
  - Unify package definition schemas for `IDesktopEnvironment` so dependencies (`xrdp`, `xorgxrdp`, `pipewire`, `wireplumber`) map cleanly across different package managers.
- [ ] **Extend Distro Base Support**
  - Add `ArchDistroBase` (`pacman`) support for ArchWSL.
  - Add `OpenSuseDistroBase` (`zypper`) support for openSUSE Tumbleweed / Leap.

---

## Phase 3: Headless / Non-Interactive Scripting CLI

Enable WSLConfigHelper to run non-interactively in automated setups, dotfiles repositories, and continuous integration environments.

- [ ] **Subcommand Architecture**
  - Introduce non-interactive CLI commands alongside the Spectre.Console TUI:
    ```bash
    wslconfig-helper doctor [--json] [--exit-code]
    wslconfig-helper preset apply <balanced|conservative|workstation> [--force]
    wslconfig-helper get <section.key>
    wslconfig-helper set <section.key> <value>
    wslconfig-helper restore
    ```
- [ ] **Machine-Readable Diagnostics Output**
  - Add `--json` flag to `doctor` command to output structured guardrail issues for automated audits and scripts.
  - Return exit code `1` if critical guardrail errors (e.g. invalid memory syntax, extreme RAM exhaustion) are detected.

---

## Phase 4: Workstation Security & Identity Customization

Enhance provisioning security and flexibility for workstations deployed in shared or enterprise environments.

- [ ] **Configurable Workstation User**
  - Allow users to specify their preferred Linux username instead of hardcoding `developer`.
  - Provide an interactive prompt during initial desktop provisioning with a sane default.
- [ ] **Flexible Credential Handling**
  - Support setting a secure custom password during user provisioning instead of identical username/password pairs.
  - Make passwordless `sudo` optional via a toggle (`--passwordless-sudo=false`).
- [ ] **SSH Key Pre-provisioning**
  - Optionally import host Windows SSH public keys (`%USERPROFILE%\.ssh\id_*.pub`) into `/home/<user>/.ssh/authorized_keys` for headless remote workflows.

---

## Phase 5: CI/CD, Documentation & Maintenance

Keep repository metrics, continuous integration, and local developer ergonomics clean and accurate.

- [ ] **Automated GitHub Actions Workflow**
  - Add `.github/workflows/ci.yml` running `dotnet build` and `dotnet test` on Windows runners.
- [ ] **README Metric Synchronizations**
  - Update test count badge in `README.md` to reflect current passing test suite count (40 tests).
- [ ] **Workspace Hygiene**
  - Remove empty whitespace directory artifact (`" "`) at repository root to clear Git path-normalization warnings.
