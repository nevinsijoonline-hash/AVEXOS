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

## What does NOT work yet

Stated plainly so nothing here is mistaken for a shipped feature:

- **Taskbar, Start menu, Explorer, context menus, system dialogs** — unsupported by design (see the matrix).
- **Folder / file-type / drive icons, shortcuts, cursors, sounds** — designed, **not implemented**.
- **Theme apply** — sets `RequestedTheme` (light or dark) on the Control Center window only. It does
  not restyle Windows, and it does not render the theme's own numbers: the colours, radii, spacing and
  sizes in the theme JSON are validated but never turned into brushes, so two themes with the same
  `mode` look identical on screen. See [ARCHITECTURE.md §9](docs/architecture/ARCHITECTURE.md).
- **AI Design Studio** — only a deterministic local keyword→theme generator exists. No model provider, no network.
- **Plugins, installer, code signing, packaging** — not implemented.
- **Test coverage** — only Security and ThemeEngine have tests. Wallpaper, pipeline, discovery and UI have none.

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
| Solution build | Succeeds — 0 errors, 6 distinct warnings (below) |
| Tests | 65 pass (29 Security, 6 Core, 18 ThemeEngine, 3 Backup, 9 WindowsIntegration) |
| Unpackaged launch | Works — **requires `DOTNET_ROOT`**; without it the apphost shows a missing-runtime dialog |
| Packaged launch | Blocked — Developer Mode disabled |
| Windows SDK | 10.0.26100 · VS Build Tools 17.14.41 |

### Build warnings you will see

Six distinct warnings, all known debt:

- `NU1903` × 4 — `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 carries a known high-severity advisory
  ([GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)). It is a transitive
  dependency of `Microsoft.Data.Sqlite` 10.0.0 and is **not yet resolved**. One warning per project
  that transitively references it: `AVEXOS.App`, `AVEXOS.Security`, `AVEXOS.WindowsIntegration`,
  `AVEXOS.Security.Tests`.
- `CS0168` × 2 — unused `catch (Exception ex)` variables in `ActivityPage` and `HomePage`, where the
  exception is swallowed instead of surfaced.

Do not anchor on the number MSBuild prints in its summary. That is an **occurrence** count, and each
of these warnings surfaces twice — once resolved at solution scope, once at project scope — so the
headline reads **10** on an up-to-date incremental build and **12** with `-t:Rebuild`, without any
new problem. In Git Bash, count the distinct set with:

```bash
dotnet build AVEXOS.slnx 2>&1 | grep ": warning " | sed 's/ \[C:.*//' | sort -u | wc -l
```

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
tests/     AVEXOS.Security.Tests, AVEXOS.ThemeEngine.Tests
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
