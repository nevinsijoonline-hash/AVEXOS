# AVEX OS

A Windows visual transformation platform. C# · .NET 10 · WinUI 3 · Windows App SDK 2.6.5 · SQLite · JSON.

> **Status: first vertical slice implemented and verified on host.** Wallpaper is the one end-to-end
> change flow that currently runs through backup → approval → apply → read-back verification → restore.
> Everything else is either scaffolding or unsupported. See "What actually works" below.

## Read this first

The master specification asks AVEX OS to restyle the taskbar, Start menu, Explorer, context menus and
other vendors' applications. Within the same specification's rules — never touch `C:\Windows`, never
patch binaries, never inject, never use undocumented techniques — **that is not possible**, and the
tools that do it operate exactly the way the specification forbids.

AVEX OS therefore ships an honest capability matrix instead of a fake claim: it restyles its own
surfaces completely, customizes the user-scope settings Windows actually exposes, and lists everything
else as **unsupported with a stated reason**. A capability is never reported as applied until the value
has been read back and verified.

See [docs/compatibility/COMPATIBILITY.md](docs/compatibility/COMPATIBILITY.md) for the full matrix.

## What actually works (verified on this host)

| Flow | Verified how |
|---|---|
| Control Center launches as an unpackaged WinUI 3 app and renders | Window captured via `PrintWindow`; real `WinUIDesktopWin32WindowClass`, title `AVEX OS Control Center` |
| Application discovery | 78 installed apps read live from the Uninstall registry tree |
| Wallpaper read | `SystemParametersInfo(SPI_GETDESKWALLPAPER)` + `WallpaperStyle`/`TileWallpaper` |
| Wallpaper backup | JSON snapshot written to `%LOCALAPPDATA%\AVEXOS\backups` before the change |
| Wallpaper apply | Approval dialog gates the write; value read back and matched |
| Wallpaper restore | Returns to the backed-up original; registry value confirmed changed back |
| Theme selection inside the app | Picking a different row switches the app between its dark and light surfaces; content-area luminance 14.3 vs 102.5 |
| Protected-path guard | 15 unit tests |
| Theme schema validation | 8 unit tests |
| Theme resource building | 10 unit tests |
| Backup integrity (SHA256 hash) | 3 unit tests |
| Registry policy (prefix-shadowing) | 5 unit tests |
| Pipeline rollback verification | 3 unit tests |
| Risk level gating | 3 unit tests |
| WallpaperService.ImportWallpaper | 5 unit tests |
| AppDiscoveryService.DiscoverAsync | 4 unit tests |
| ShortcutParser | 44 unit tests |
| ThemeSchedulerService | 8 unit tests |
| UI structure (XAML + navigation) | 42 unit tests |

## What does NOT work yet

Stated plainly so nothing here is mistaken for a shipped feature:

- **Taskbar, Start menu, Explorer, context menus, system dialogs** — unsupported by design (see the matrix).
- **Folder / file-type / drive icons, shortcuts, cursors, sounds** — designed, **not implemented**.
- **Theme apply** — sets `RequestedTheme` (light or dark) on the Control Center window only. It does
  not restyle Windows. Theme tokens are rendered into `ResourceDictionary` brushes via
  `ThemeResourceBuilder`, so different themes produce distinct colors on screen.
- **AI Design Studio** — only a deterministic local keyword→theme generator exists. No model provider, no network.
- **Plugins, installer, code signing, packaging** — not implemented.
- **Test coverage** — 305 tests across 6 projects cover path policy, registry policy, backup integrity,
  theme engine, pipeline logic, wallpaper service, app discovery, shortcuts, theme scheduler, and UI
  structure. Full UI automation (running app interaction) is not implemented.

## Documents

| Document | Contents |
|---|---|
| [Architecture](docs/architecture/ARCHITECTURE.md) | Layers, projects, change pipeline, guard, data model, theme schema |
| [Design system](docs/architecture/DESIGN_SYSTEM.md) | Tokens, typography, components, motion, accessibility |
| [Capability matrix](docs/compatibility/COMPATIBILITY.md) | Level A/B/C/D per target, with mechanism and host constraints |
| [Security](docs/security/SECURITY.md) | Protected-path policy, registry allowlist, privilege-escalation cases |
| [Threat model](docs/security/THREAT_MODEL.md) | STRIDE entries, assets, trust boundaries, mitigations |
| [Installation](docs/setup/INSTALLATION.md) | Toolchain, build, run, uninstall |
| [User guide](docs/setup/USER_GUIDE.md) | Day-to-day use of the Control Center |
| [Developer guide](docs/setup/DEVELOPER_GUIDE.md) | Adding a capability, conventions, verification harness |
| [Backup & recovery](docs/operations/BACKUP_AND_RECOVERY.md) | What is backed up, how to restore, what recovery cannot do |
| [Troubleshooting](docs/operations/TROUBLESHOOTING.md) | Known failures and how to diagnose them |
| [Contributing](docs/contributing/CONTRIBUTING.md) | Branch, review and safety rules |
| [Changelog](docs/contributing/CHANGELOG.md) | Dated changes |

## Toolchain status (verified on this host)

| Item | State |
|---|---|
| Windows | 11 build 26300.9711 (26H2 Insider), Core (Home), x64 |
| .NET SDK | 10.0.403, user-scoped (`%LOCALAPPDATA%\Microsoft\dotnet`) — **not on PATH** |
| .NET runtime | 10.0.14 (`Microsoft.NETCore.App`) |
| Windows App Runtime | 2.6.5 installed machine-wide |
| Solution build | Succeeds — 0 errors, 0 warnings |
| Tests | 305 pass (44 Core, 28 ThemeEngine, 15 Backup, 95 WindowsIntegration, 29 Security, 94 App), 1 skipped |
| Unpackaged launch | Works — **requires `DOTNET_ROOT`**; without it the apphost shows a missing-runtime dialog |
| Packaged launch | Blocked — Developer Mode disabled |
| Windows SDK | 10.0.26100 · VS Build Tools 17.14.41 |

### Build warnings

None. The six warnings that previously existed were resolved:
- `NU1903` (SQLitePCLRaw advisory GHSA-2m69-gcr7-jv3q) — fixed by upgrading `Microsoft.Data.Sqlite` to 10.0.14
- `CS0168` (unused exception variables) — fixed by removing unused `ex` declarations

It prints **6**. The `sed` strips the bracketed scope that makes the duplicate lines look distinct —
without it, `sort -u` leaves 10.

## Layout

```
src/
  AVEXOS.Core                models, contracts, change pipeline
  AVEXOS.Security            protected-path policy, filesystem guard, SQLite store
  AVEXOS.ThemeEngine         validator, built-in catalog, local proposal generator
  AVEXOS.Backup              JSON backup store
  AVEXOS.WindowsIntegration  wallpaper service, app discovery, registry policy
  AVEXOS.App                 WinUI 3 Control Center
assets/    themes, wallpapers
tests/     AVEXOS.App.Tests, AVEXOS.Backup.Tests, AVEXOS.Core.Tests, AVEXOS.Security.Tests,
           AVEXOS.ThemeEngine.Tests, AVEXOS.WindowsIntegration.Tests
docs/      architecture, security, compatibility, setup, operations, contributing
scripts/   launch-app.cmd, capture-window.ps1, uia-dump.ps1, gui-cycle.ps1, theme-probe.ps1
```

## Quick start

```bat
set "DOTNET_ROOT=C:\Users\<you>\AppData\Local\Microsoft\dotnet"
"%DOTNET_ROOT%\dotnet" build AVEXOS.slnx -c Debug
scripts\launch-app.cmd
```

`scripts/launch-app.cmd` sets `DOTNET_ROOT` and starts the app from its output directory. Adjust the
path inside it if your SDK lives elsewhere.

To re-run the verified wallpaper cycle (apply → approve → verify → restore) against the live desktop:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -STA -File scripts/gui-cycle.ps1
```

This **does** change your desktop wallpaper and restores it at the end.

## Non-negotiables

1. `C:\Windows` is never written, renamed, moved or replaced — including via links, junctions or alternate paths.
2. No binary patching, no injection, no TrustedInstaller bypass, no disabling Defender/Firewall/UAC.
3. Every change is backed up, verified by read-back, and restorable.
4. Unsupported targets are labelled unsupported. Never claimed to work.
5. AVEX OS data lives in `%LOCALAPPDATA%\AVEXOS`, `%APPDATA%\AVEXOS`.

## Roadmap

| Phase | Deliverable | State |
|---|---|---|
| 1 | Audit, architecture, compatibility matrix, design system, security model | Done |
| 2 | Control Center shell, tokens, light/dark | Done |
| 3 | Theme engine (schema, validation, local proposals) | Partial — apply is app-internal only |
| 4 | Application discovery | Done (read-only inventory) |
| 5 | First safe Windows integration | **Done for wallpaper**; shortcuts/icons/cursors/sounds not started |
| 6 | Icon and folder system | Not started |
| 7 | Application adapters (Level A/C only) | Not started |
| 8 | AI Design Studio | Not started (local generator only) |
| 9 | Monitoring, diagnostics, recovery | Partial (audit log + operations store) |
| 10 | Installer, signing, release | Not started |
