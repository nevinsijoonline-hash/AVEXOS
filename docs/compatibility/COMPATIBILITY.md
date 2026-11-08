# AVEX OS — Capability & Compatibility Matrix

**Status:** Phase 1 audit deliverable
**Audited host:** Windows 11 build 26300.9711 (26H2 Insider), edition **Core (Home)**, x64
**Rule applied:** a target is listed as supported only when a documented or stable user-scope mechanism exists that AVEX OS may use without patching, injecting, or writing to `C:\Windows`.

---

## 0. The ceiling, stated plainly

The engineering rules in the master specification (Parts 2, 13, 15, 27) forbid binary patching, DLL injection, shell hooks, TrustedInstaller abuse and writes under `C:\Windows`. Those same rules are exactly what the well-known third-party "full Windows restyle" tools rely on.

**Consequence:** AVEX OS can fully rebrand *its own* applications, and can safely customize *file-system-visible identity* (wallpaper, shortcuts, folder icons, cursors, sounds). It **cannot** restyle the Windows shell (taskbar, Start menu, Explorer, context menus) or restyle the UI of other vendors' applications.

File-type icons are the one item that used to sit in that safe list and no longer does. The mechanism
is `HKCU\Software\Classes\.<ext>\DefaultIcon`, and `Software\Classes` was taken out of the registry write
allowlist on 2026-11-08 because rule 22 forbids using associations as a branding lever — see §2 and
[THREAT_MODEL.md](../security/THREAT_MODEL.md) §4. "Achievable today" below means *as a technique*; only
wallpaper is implemented and verified.

Any product claim to the contrary would violate Non-Negotiable Rules 10 and 11.

| Area | Achievable today | Not achievable within the rules |
|---|---|---|
| AVEX OS own UI | Full design system, 100% | — |
| Desktop identity | Wallpaper, icon size/spacing/labels, cursor, sounds, shortcut icons | Restyling shell widgets |
| File identity | Folder icons, drive icons, shortcut display names | Changing file contents/associations, including file-type icons via `HKCU\Software\Classes` (rule 22; the hive is off the write allowlist) |
| System appearance | Light/dark mode, accent colour, theme packs | Custom window chrome for other apps |
| Taskbar / Start / Explorer | Toggles only (alignment, auto-hide, search mode) | Colour, shape, layout, custom panels |
| Third-party apps | Documented per-app config (VS Code, Windows Terminal, …) | Anything requiring injection/patching |

Every entry below follows the Level A–D vocabulary from Part 11.1.

---

## 1. Level A — documented, native, fully reversible

| Target | Mechanism | Scope | Reversible |
|---|---|---|---|
| AVEX OS Control Center UI | WinUI 3 + AVEX design tokens | Own app | n/a |
| AVEX OS window chrome | `DwmSetWindowAttribute` (`DWMWA_CAPTION_COLOR`, `DWMWA_BORDER_COLOR`, corner preference) | Own windows | n/a |
| AVEX OS notifications | Toast XML with brand assets | Own app | n/a |
| Windows Terminal appearance | `settings.json` (documented schema) | Per-user | Yes — file backup |
| Visual Studio Code appearance | `settings.json` + extension theming | Per-user | Yes — file backup |
| `.theme` / `.themepack` files | Documented file format | Per-user | Yes — restore previous theme |

---

## 2. Level B — documented Windows APIs / personalization settings

| Target | Mechanism | Notes | Verified on host |
|---|---|---|---|
| Desktop wallpaper | `SystemParametersInfo(SPI_SETDESKWALLPAPER)` | Source image copied into `%LOCALAPPDATA%\AVEXOS\`; original path/value backed up | Not yet executed (Phase 1 forbids host changes) |
| Wallpaper fit mode | `HKCU\Control Panel\Desktop` (`WallpaperStyle`, `TileWallpaper`) | Registry, user scope | Not yet |
| Desktop icon size / spacing / label shadow | `HKCU\Software\Microsoft\Windows\Shell\Bags` + `SHChangeNotify` | Needs Explorer refresh | Not yet |
| Shortcut icon | `IShellLinkW` + `IPersistFile` | Backs up original `.lnk` before rewrite | Not yet |
| Folder icon | `desktop.ini` + `SHChangeNotify(SHCNE_UPDATEITEM)` | **Adds a file inside the user's folder** — see Decision D2 | Not yet |
| File-type icon | `HKCU\Software\Classes\.<ext>\DefaultIcon` | **Blocked by the guard as shipped.** `Software\Classes` was removed from the registry write allowlist on 2026-11-08 (spec rule 22, [THREAT_MODEL.md](../security/THREAT_MODEL.md) §4), so this path needs a deliberate allowlist review before anyone builds it. Extension and association would stay untouched | Not yet |
| Drive icon | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\DriveIcons` | Per-volume-letter | Not yet |
| Cursor scheme | `HKCU\Control Panel\Cursors` + `SPI_SETCURSORS` | Cursor assets stored under AVEX data dir | Not yet |
| App event sounds | `HKCU\AppEvents\Schemes\Apps\.Default` | Sound assets bundled with theme | Not yet |

---

## 3. Level C — supported external configuration (stable, semi-documented)

| Target | Mechanism | Risk | Policy |
|---|---|---|---|
| System light/dark mode | `HKCU\...\Themes\Personalize\AppsUseLightTheme` / `SystemUsesLightTheme` | Undocumented but long-stable; affects all apps | Allowed, backed up, audited |
| System accent colour | `HKCU\...\DWM\AccentColor`, `...\Explorer\Accent` | Undocumented; needs `Explorer` restart for full effect | Allowed with explicit warning + restart prompt |
| Taskbar alignment / auto-hide / search mode / widgets toggle | `HKCU\...\Explorer\Advanced` | Undocumented values, version-sensitive | Allowed as toggles only |
| Explorer folder view defaults | `Shell\Bags`, `FolderTypes` | Version-sensitive | Allowed, per-folder opt-in |

---

## 4. Level D — unsupported, and why

Everything in this table is **listed in the interface as unsupported with a reason**, never silently skipped and never claimed to work.

### 4.1 Windows shell

| Target | Why unsupported |
|---|---|
| Taskbar colour / transparency / shape / custom panels | No documented API. Only achievable via undocumented shell patching or injection — prohibited (Parts 2.4, 13, 15). |
| Start menu appearance / layout | No documented API. Same prohibition. |
| File Explorer window restyle (nav pane, toolbar, tabs) | No theming API. Requires injection/hooking — prohibited. |
| Modern context menus | No supported customization surface. |
| Quick Settings panel, notification-area flyouts | System XAML, not extensible to third parties. |
| Logon / lock / boot / OOBE screens | Lock-screen image is policy-driven (`WindowsPersonalization` CSP), **unavailable on Home edition**; logon and boot visuals require binary/asset replacement — prohibited. |
| Shell icon resources (`imageres.dll`, `shell32.dll`, etc.) | Reside under `C:\Windows` — hard prohibition. Icons are only replaceable *per object* (folder, shortcut, file type, drive). |

### 4.2 Windows applications

No app in this list exposes a documented theming API; all follow the system light/dark setting only.

`Notepad`, `Calculator`, `Paint`, `Snipping Tool`, `Photos`, `Camera`, `Clock`, `Sticky Notes`, `Media Player`, `Sound Recorder`, `Character Map`, `Microsoft Store`, `Task Manager`, `Control Panel`, `Settings`, `Registry Editor`, `Device Manager`, `Disk Management`, `Computer Management`, `Event Viewer`, `Services`, `Task Scheduler`, `Resource Monitor`, `Performance Monitor`, `System Information`, `MSConfig`, `Windows Security`, `Windows Firewall UI`, `Windows Features`, `Recovery`, `Local Security Policy`, `Group Policy Editor`, `Certificate Manager`, `TPM Management`, `MMC`, `Network Connections`, `Network and Sharing Center`, `Wi-Fi settings`, `Bluetooth settings`, `VPN settings`, `Internet Options`, `Remote Desktop`, `Quick Assist`, `Command Prompt`, `PowerShell`, `PowerShell ISE`, `Xbox`, `Xbox Game Bar`, `Solitaire`, `Narrator`, `Magnifier`, `On-Screen Keyboard`, `Voice Access`, `Speech Recognition`, `Accessibility Settings`, `Mobility Center`.

**What AVEX OS does instead for these targets:** registers them in the Application Manager, shows the real compatibility level, applies the *shared* system light/dark and accent settings if the user opts in, and offers an AVEX-branded shortcut icon. The adapter declares `Capability = SystemThemeOnly`.

Additionally excluded by rule:
- Third-party executables — never patched (Part 5.5).
- Games / anti-cheat processes — never touched (Part 5.6).
- Accessibility features — never degraded (Part 5.7).

### 4.3 Level C/D boundary for productivity apps

`Outlook`, `Teams`, `OneDrive`, `Edge`, `Copilot`, `Clipchamp`, `Whiteboard`, `Phone Link`, `Microsoft To Do` expose no third-party theming API. `Edge` and `Teams` are Electron/WebView-based; their internals are not a supported integration point. They are recorded as **Unverified → Unsupported** unless a documented configuration file exists at audit time.

---

## 5. Host-specific constraints found during audit

| Finding | Impact |
|---|---|
| Edition is **Core (Home)** | Policy-based lock-screen customization unavailable; no `gpedit.msc`. |
| **Developer Mode is off** and the shell is not elevated | Packaged MSIX deployment fails. Development runs **unpackaged** (`WindowsPackageType=None`), which was verified working. Enabling it later requires an admin consent step. |
| .NET SDK is **user-scoped** (`%LOCALAPPDATA%\Microsoft\dotnet`, 10.0.403), not machine-wide | Built apphost `.exe` files fail with "You must install .NET" unless `DOTNET_ROOT` is set or the app is published self-contained. **Verified**. |
| Windows SDK 10.0.26100 present; VS Build Tools 17.14.41 present | WinUI 3 build path is healthy. |
| No code-signing certificate identified | Installer/update signing is a Phase 10 blocker; already listed in the roadmap. |

---

## 6. Verification log

| Date | Check | Result |
|---|---|---|
| 2026-11-07 | `dotnet build` WinUI 3 blank app (`net10.0-windows10.0.26100.0`, WASDK 2.6.5) | **Pass** — 0 warnings, 0 errors (blank template; the real app builds with 0 errors and 6 distinct warnings) |
| 2026-11-07 | Packaged launch (`dotnet run`, winapp CLI) | **Fail** — Developer Mode disabled |
| 2026-11-07 | Unpackaged self-contained launch | **Retracted** — first recorded as Pass on the strength of a live PID and `MainWindowHandle`. That window was the apphost's "you must install .NET" dialog, not the app. Corrected 2026-11-08; see the rows below. |
| 2026-11-07 | Runtime customization operations | **Not executed** — Phase 1 forbids host changes pending approval |
| 2026-11-08 | Unpackaged launch, `DOTNET_ROOT` set | **Pass** — `WinUIDesktopWin32WindowClass`, title `AVEX OS Control Center`, `PrintWindow` histogram showed real content |
| 2026-11-08 | Application discovery against this host | **Pass** — 78 installed applications listed, each with a capability level and reason |
| 2026-11-08 | Wallpaper read (`SPI_GETDESKWALLPAPER`) | **Pass** — path plus `WallpaperStyle`/`TileWallpaper` returned |
| 2026-11-08 | Backup written before apply | **Pass** — `bk-*.json` snapshot in `%LOCALAPPDATA%\AVEXOS\backups` |
| 2026-11-08 | Apply behind an approval dialog, then read back | **Pass** — dialog shown, apply ran, registry value matched the imported file |
| 2026-11-08 | Restore to the original | **Pass** — registry returned to the original image path, confirmed outside the app. An earlier **Fail** on this row was the cached-original bug, fixed 2026-11-08. |
| 2026-11-08 | Theme selection visibly changes the rendered window | **Pass** — content-area luminance 14.3 (avex.dark) vs 102.5 (avex.light). An earlier **Fail**: the Themes list had no selection handler, so every apply used the first theme. |
| 2026-11-08 | Taskbar / Start menu / Explorer / context-menu restyle | **Not attempted** — Level D by design; no documented, non-injecting mechanism exists |

---

## 7. Decisions required

- **D1 — Ceiling acceptance.** Confirm the product will ship the honest matrix above (shell/app restyling listed as unsupported) rather than pursuing prohibited techniques.
- **D2 — Folder icons via `desktop.ini`.** This technically adds a hidden file inside the user's folder. It is the only documented mechanism. Accept, or drop folder-icon customization?
- **D3 — Toolchain.** Install .NET 10 SDK machine-wide (admin), or standardise on the user-scoped SDK with `DOTNET_ROOT` set by the build script and self-contained publishes?
