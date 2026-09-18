# AGENTS.md

## Project

This repository contains **WGO DTX Inspector**, a .NET 10 / C# desktop tool for
read-only validation and inventory of DTX Studio Clinic nodes.

The project started as a replacement for existing PowerShell scripts and evolved
into an Avalonia UI application. The current preferred direction is a single app
with mandatory manual node selection. Never infer or automatically select a role.
If none is selected, prompt for Core, Workstation or Client before node checks.

Supported node roles:

- Core
- Workstation
- Client

## Main Requirements

### Mandatory infrastructure checklist and separate node reports

The following user-provided preparation checklist MUST always appear in node
checks AND final reports. Empty JSON lists or inventory-generated settings must
never remove these checks. It is a deployment checklist, not a claim that all
items are universal vendor requirements. Perform checks only: never automatically
configure IP, IPv6, hostname, DNS, services, firewall, hosts or security products.

- Core: static IP; IPv6 disabled on the DTX adapter; unchanged hostname compared
  with its approved installation baseline; Core resolvable by internal local DNS;
  no active VPN/mesh, interfering virtual adapters or hosts overrides; DTX Windows
  services running AND application health verified separately from service state.
- Workstation: static IP; IPv6 disabled on the DTX adapter; unchanged hostname;
  correct internal DNS resolution of Core; no active VPN/mesh, interfering virtual
  adapters or hosts overrides; operational user read/write access to local DTX
  directories (especially C:\ProgramData\DTX Studio...); DTX installation and
  updates performed with local administrator privileges.
- Client: static IP and IPv6 disabled on the DTX adapter, as for every other role;
  correct internal DNS resolution of Core;
  hostname unchanged since association/installation; no active VPN/mesh,
  interfering virtual adapters or hosts overrides.
- Every node report: functional bidirectional DNS between Core/workstations/clients;
  no slow fallback to public DNS; permitted and uninspected inter-node traffic;
  required REST, gRPC and dynamic/service TCP communication; DICOM TCP 104 when
  required; no AV/EDR SSL/TLS inspection or DPI on local DTX traffic; verify DTX
  data exchange does not depend on SMB shares or mapped drives.

Latest user policy: every role requires static IPv4 and disabled IPv6 on the DTX
adapter. DHCP is FAIL even on clients with stable DNS. The legacy
clientRequiresIpv6Disabled setting must not exempt clients from this requirement.

Generate a separate report per tested node role and machine; never combine roles
into a single successful assessment. Show role, hostname, time, evidence and the
scope of every result. Missing configuration, insufficient privileges, unperformed
network probes and manual-only requirements must remain WARNING / not verified,
never PASS. Use explicit not-applicable status for configured conditional items.
No inferred port numbers for REST/gRPC/dynamic services. A listening port or TCP
connection is not proof of protocol health, absence of inspection, or bidirectionality.
An elevated tool token is not evidence of the operational user's permissions or
past installation/update privileges. No automatic system changes.
User explicitly requires DNS/TCP checks on every requested test run, with timeout:
use only configured targets, bound each attempt, and never probe at startup,
during inventory. Missing targets must produce WARNING.

- The app must be read-only by default.
- It must not modify network, DNS, firewall, services, registry, hosts file, or
  system configuration.
- It must not run PowerShell or shell commands from the app.
- DNS/TCP probes run on configured targets when the user starts node checks,
  with a configurable bounded timeout. No application payloads or port scanning.
- It may open official documentation links only after explicit user action.
- It may write report/log files only when requested by the desktop workflow or
  explicit report/log options.
- User-approved exception: `Impostazioni da inventario` may replace the selected
  node's app configuration after an explicit warning/confirmation. It backs up
  the previous JSON, preserves common/other-node settings and never changes OS settings.
- Windows checks are Windows-only and must show a clear message outside Windows.
- Desktop builds must target Windows x64 (`win-x64`) and be self-contained.
  Desktop publishes must also be single-file. Other runtime targets are unsupported.

## Current Architecture

- `Wisegar.DTXInspector.Core`: shared logic for configuration, checks, inventory,
  reports and GUI service facade.
- `Wisegar.DTXInspector.App`: the only desktop app, published as
  `WgoDtxInspector`, with mandatory manual node selection.
- `src/Wisegar.DTXInspector.App/Desktop`: Avalonia window, application and constants.
- All checks/configuration/inventory/report sources live in `src/Wisegar.DTXInspector.Core`.
- The former CLI, per-node apps and linked shared-source folders have been removed.
- `tests/Wisegar.DTXInspector.Desktop.Smoke` is a test runner, not a distributed product.

## Important Files

- `configs/appsettings.json`: the only configuration source; contains common
  settings and all three node sections. Build targets copy it next to the app.
- `Directory.Build.props`: defaults desktop builds to Windows x64, self-contained.
- `Directory.Build.targets`: rejects other desktop targets and framework-dependent
  builds; requires single-file publishing.
- `packaging/inno`: Inno Setup packaging for Windows installers.
  `Build-InnoInstallers.cmd` now builds the unified `Wisegar.DTXInspector.Setup.iss` only.
  The generated installer goes to `installers/Wisegar.DTXInspector.Setup-<version>.exe`
  (git-ignored); the filename uses the Inno `AppVersion` definition.
  No per-node installer sources remain.
- `README.md`: user-facing project documentation.
- `codex.mem`: conversation and project memory.

## Build And Publish

Version numbering starts at `0.0.1` per user request. Keep .NET version metadata
in Directory.Build.props, the app manifest and Inno AppVersion aligned. Earlier
1.0.x version references in codex.mem are historical.

Build:

```bash
dotnet build Wisegar.DTXInspector.sln
```

Preferred Windows publish:

```bash
dotnet publish src/Wisegar.DTXInspector.App/Wisegar.DTXInspector.App.csproj -c Release -r win-x64
```

`-r win-x64` is optional because it is the default. Do not build or publish
desktop apps for macOS, Linux, x86 or ARM64. Do not disable self-contained deploy.

Desktop regression smoke checks (requires a desktop environment):

```bash
dotnet run --project tests/Wisegar.DTXInspector.Desktop.Smoke
```

## Engineering Notes

- Use `rg` for searching.
- Use `apply_patch` for manual file edits.
- Do not revert user changes.
- Keep the app behavior read-only.
- Keep configuration JSON external and modifiable next to the published app.
- Keep network access out of startup/inventory; node test runs include bounded DNS/TCP probes.
- If packaging artifacts are regenerated, keep them outside git unless the user
  explicitly asks to commit them.

## Current Working Direction

The preferred future state is a single branded app:

- `WGO DTX Inspector`
- one unified config file
- request manual node selection at startup
- manual override in UI
- HTML reports opened automatically after test/inventory runs
- self-contained deploys for Windows x64 only
