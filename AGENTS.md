# AGENTS.md

## Project

This repository contains **WGO DTX Node Check**, a .NET 10 / C# desktop tool for
read-only validation and inventory of DTX Studio Clinic nodes.

The project started as a replacement for existing PowerShell scripts and evolved
into an Avalonia UI application. The current preferred direction is a single app
that can infer the node type locally. If inference is not confident, the user
selects the node manually.

Supported node roles:

- Core
- Workstation
- Client

## Main Requirements

- The app must be read-only by default.
- It must not modify network, DNS, firewall, services, registry, hosts file, or
  system configuration.
- It must not run PowerShell or shell commands from the app.
- It must not send data over the network automatically.
- It may open official documentation links only after explicit user action.
- It may write report/log files only when requested by the desktop workflow or
  explicit report/log options.
- Windows checks are Windows-only and must show a clear message outside Windows.
- Desktop deploys must always be self-contained and single-file when published
  for a Runtime Identifier.

## Current Architecture

- `DtxNodeCheck.Core`: shared logic for configuration, checks, inventory,
  reports, node inference, and GUI service facade.
- `DtxNodeCheck.App`: preferred unified Avalonia app, published as
  `WgoDtxNodeCheck`, with node inference and manual node selection.
- `DtxNodeCheck.CoreApp`: legacy Avalonia app pinned to Core.
- `DtxNodeCheck.WorkstationApp`: legacy Avalonia app pinned to Workstation.
- `DtxNodeCheck.ClientApp`: legacy Avalonia app pinned to Client.
- `DtxNodeCheck.Desktop.Shared`: shared Avalonia UI code linked into the
  desktop apps.

## Important Files

- `configs/dtx-node-check.json`: unified configuration used by the single app.
- `configs/dtx-node-check-core.json`: legacy Core app config.
- `configs/dtx-node-check-workstation.json`: legacy Workstation app config.
- `configs/dtx-node-check-client.json`: legacy Client app config.
- `Directory.Build.targets`: blocks desktop publish without Runtime Identifier
  and without self-contained deploy.
- `packaging/inno`: Inno Setup packaging for Windows installers.
- `README.md`: user-facing project documentation.
- `codex.mem`: conversation and project memory.

## Build And Publish

Build:

```bash
dotnet build DtxNodeCheck.sln
```

Preferred Windows publish:

```bash
dotnet publish src/DtxNodeCheck.App/DtxNodeCheck.App.csproj -c Release -r win-x64
```

Preferred macOS ARM64 publish:

```bash
dotnet publish src/DtxNodeCheck.App/DtxNodeCheck.App.csproj -c Release -r osx-arm64
```

Do not publish desktop apps without `-r <RID>`. The repository intentionally
fails such publishes.

## Engineering Notes

- Use `rg` for searching.
- Use `apply_patch` for manual file edits.
- Do not revert user changes.
- Keep the app behavior read-only.
- Keep configuration JSON external and modifiable next to the published app.
- Keep automatic network access out of checks and startup logic.
- If packaging artifacts are regenerated, keep them outside git unless the user
  explicitly asks to commit them.

## Current Working Direction

The preferred future state is a single branded app:

- `WGO DTX Node Check`
- one unified config file
- node inference at startup
- manual override in UI
- HTML reports opened automatically after test/inventory runs
- self-contained deploys for Windows and macOS

