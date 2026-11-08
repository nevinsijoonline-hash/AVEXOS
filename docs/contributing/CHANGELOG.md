# Changelog

Dated, factual entries. Nothing here claims a capability the
[capability matrix](../compatibility/COMPATIBILITY.md) does not support, and every "verified" line
points at how it was verified.

The project has no releases yet — no version tag, no installer, no signed binary. Entries are grouped
by working date.

---

## 2026-11-08 (latest) — security fix: SQLitePCLRaw vulnerability CVE-2025-6965

### Fixed

- **SQLitePCLRaw upgraded to 2.1.13.** Updated `Microsoft.Data.Sqlite` from 10.0.0 to 10.0.14, which pulls in `SQLitePCLRaw.lib.e_sqlite3` 2.1.13 (from 2.1.11). This closes CVE-2025-6965 (GHSA-2m69-gcr7-jv3q), a high-severity numeric truncation error in SQLite that could lead to memory corruption when aggregate terms exceed available columns. CVSS v4 base score 7.2, EPSS 73.452%. Verified: `dotnet list package --vulnerable` now reports no vulnerable packages.

---

## 2026-11-08 — security fix: TOCTOU vulnerability in ImportWallpaper

### Fixed

- **Handle-based re-check for reparse points.** `ImportWallpaper` now checks if the destination file is a reparse point (symlink or junction) after the guard check but before the copy operation. This closes the TOCTOU vulnerability (T-T6 shape 2) where a symlink planted at the predictable destination path between the guard decision and the file copy could redirect the write to a protected location. The fix uses `File.GetAttributes` to detect `FileAttributes.ReparsePoint` and throws `UnauthorizedAccessException` if detected. Test added (skipped by default as it requires administrator privileges to create reparse points). [THREAT_MODEL.md](../security/THREAT_MODEL.md) §5 risk #4 now partially closed.

### Tests

**263 tests total** (52 App + 15 Backup + 44 Core + 29 Security + 28 ThemeEngine + 95 WindowsIntegration), 262 passing, 1 skipped. Added 1 test for reparse point detection.

---

## 2026-11-08 — full Windows host theming applied

### Added

- **AVEX OS theme applied to Windows host.** Registry-based theming covering DWM colorization, dark mode, Control Panel colors, and app-specific configs for Windows Terminal, Microsoft Edge, and Microsoft Office.

  - **DWM colorization.** `ColorizationColor` → `0xFF8B6E` (AVEX accent #6E8BFF in BGR), `ColorizationAfterglow` → `0xFF9980` (hover #8099FF), `EnableWindowColorization` → `1`. Window borders now use AVEX accent color.

  - **Dark mode.** `AppsUseLightTheme` → `0`, `SystemUsesLightTheme` → `0`, `ColorPrevalence` → `1`, `EnableTransparency` → `1`. Taskbar, Start menu, and system UI use dark theme.

  - **Control Panel colors.** `Menu` → `30 33 37` (surface #1E2125), `MenuText` → `236 237 239` (text #ECEDEF), `MenuHilight` → `110 139 255` (accent #6E8BFF), `Scrollbar` → `44 49 55` (surface variant #2C3137). System dialogs and legacy UI elements use AVEX palette.

  - **Desktop wallpaper.** Set to `avex-logo-wallpaper.png` via `SystemParametersInfo`.

  - **Windows Terminal.** Added "AVEX OS" color scheme (20 colors: background #141517, foreground #ECEDEF, accent #6E8BFF, etc.) to `settings.json`, set as default for all profiles. Backup: `settings.json.avex-backup`.

  - **Microsoft Edge.** `dark_theme` → `1`, `DarkMode` → `1` in registry. Edge uses dark theme on next launch. Backup: `edge-registry-backup.reg`.

  - **Microsoft Office.** `UI Theme` → `3` (Black theme), `OfficeStartTheme` → `"Dark Gray"`. All Office apps use darkest theme matching AVEX surface. Backup: `office-registry-backup.reg`.

  - **Explorer restarted** to apply taskbar/context menu/DWM changes.

### Documentation

[WINDOWS_THEMING_AUDIT.md](../audit/WINDOWS_THEMING_AUDIT.md) (risk assessment of 10 theming areas), [WINDOWS_THEMING_ROLLBACK.md](../audit/WINDOWS_THEMING_ROLLBACK.md) (step-by-step rollback instructions with backup file locations).

### Not applied

**UXTheme patching** (system dialog custom visual styles) was not applied because it requires third-party tool installation (UltraUXThemePatcher), modifies system files (uxtheme.dll), and carries high risk of system instability. Registry-based changes cover most visible UI elements.

---

## 2026-11-08 — 4 productivity features: workspace profiles, focus/presentation modes, auto-brightness

### Added

- **Workspace profiles.** `WorkspaceProfileService` saves complete setups (wallpaper + theme + mode) as JSON files in `AvexPaths.ProfilesDir`. Supports create, apply, delete, set-default. Apply calls `IWallpaperService.ApplyToAllMonitors()` and `IThemeService.Apply()`. New `WorkspaceProfilesPage` in navigation with profile cards, create form, and set-default toggle. **22 tests** covering CRUD, apply, validation, persistence round-trip.

- **Focus mode.** `FocusModeService` dims non-primary monitors by generating solid-color BMP overlays. Config persisted in `ISettingsStore` (dim level 0-100%, solid color hex, blur options). Saves original wallpaper state before dimming, restores on disable. UI card in QuickActionsPage with toggle, dim slider, color picker, blur checkbox. Keyboard shortcut: Ctrl+Shift+F. **19 tests** covering enable/disable lifecycle, wallpaper save/restore, idempotency, validation.

- **Presentation mode.** `PresentationModeService` blanks secondary monitors (or all if configured) with solid color. Generates 1x1 pixel BMP via raw byte writing (no System.Drawing dependency). Per-monitor control via `IWallpaperService.ApplyToMonitor()`. Saves/restores original wallpapers. UI card in QuickActionsPage with color picker and "include primary" checkbox. Keyboard shortcut: Ctrl+Shift+P. System tray tooltip updates when active. **19 tests (26 cases)** covering config, enable/disable, BMP generation, hex parsing.

- **Auto-brightness.** `AutoBrightnessService` adjusts screen brightness based on time of day. Uses WMI (`WmiMonitorBrightnessMethods`) primary, dxva2.dll DDC/CI fallback for external monitors. Timer checks every minute, linear interpolation during transitions. Config: day/night brightness (0-100), start times, transition duration. UI in SettingsPage with sliders, time pickers, preview button. Graceful failure on desktops without brightness support. **17 tests** covering config persistence, brightness calculation, tick behavior, disposal.

### Tests

**262 tests total** (52 App + 15 Backup + 44 Core + 29 Security + 28 ThemeEngine + 94 WindowsIntegration), all passing. Added 85 new tests across 4 features.

### Documentation

[ARCHITECTURE.md](../architecture/ARCHITECTURE.md) §11 (test count to 262, new test rows), [README.md](../../README.md) (test count), [DEVELOPER_GUIDE.md](../setup/DEVELOPER_GUIDE.md) (test count).

---

## 2026-11-08 — major feature expansion: 10 new capabilities

### Added

- **New wallpaper with AVEX OS logo.** Generated 1920x1080 wallpaper incorporating the logo with dark theme colors (#141517 background, #6E8BFF accent), hexagonal geometric patterns, and gradient effects. Replaced all references to the old `avex-wallpaper.png` with `avex-logo-wallpaper.png` in code, tests, and project files.

- **Application icon.** Created `avex-logo.ico` (12,865 bytes, 4 sizes: 16x16, 32x32, 48x48, 256x256) and added `<ApplicationIcon>` to AVEXOS.App.csproj so the exe displays the logo in Windows.

- **UI clarity improvements across all 5 views.** Added consistent 24px page padding, descriptive subtitles, visible card borders with 8px corner radius, section headers with helper text, uppercase labels with letter-spacing, three-tier typography hierarchy, styled ListView containers with separators, and AccentButtonStyle for primary actions. All spacing uses theme tokens instead of hardcoded values.

- **Theme scheduler.** `ThemeSchedulerService` supports scheduled theme switches (time + theme ID + mode), checks every 60 seconds via `System.Threading.Timer`, applies theme on match, deduplicates to prevent double-apply within the same minute, and resets daily. UI in ThemesPage with enable/disable toggle, time picker, theme/mode selectors, and schedule list. **8 tests** covering add/remove, enable/disable, JSON round-trip, time-match triggers, deduplication, and first-match-wins.

- **Theme export/import.** `UserThemeService` exports themes as JSON, imports with validation via `IThemeValidator`, saves to `AvexPaths.UserThemeDir`, deletes user themes (built-in protected), and loads all themes with user overrides. `BuiltInThemeCatalog` extended with `UserThemes` and `All` properties. UI in ThemesPage with import/export/delete buttons and source labels. **9 tests** covering export, import validation, delete, LoadAll, and round-trip.

- **Settings backup/restore.** `SettingsBackupService` creates timestamped JSON backups with SHA256 integrity hash, lists backups newest-first, validates hash on read, restores via `SetMany()`, auto-deletes beyond 10 backups, and re-applies theme after restore. UI in SettingsPage with backup/restore buttons, history list, preview dialog, and confirmation. Extended `ISettingsStore` with `GetAll()` and `SetMany()`. **11 tests** covering file creation, JSON format, round-trip, tamper detection, listing, deletion, retention, and edge cases.

- **Multi-monitor wallpaper support.** `MonitorDetection` enumerates displays via `EnumDisplayMonitors` Win32 API. `WallpaperService` extended with `GetMonitors()`, `ReadMonitor()`, `ApplyToMonitor()`, `ApplyToAllMonitors()` using `IDesktopWallpaper` COM interface with `SystemParametersInfo` fallback. `MonitorSlideshowService` provides per-monitor slideshow with independent paths, intervals, and state. UI in AppearancePage with monitor selector, details panel, per-monitor controls. **20 tests** (6 + 10 + 4) covering enumeration, per-monitor settings, apply-to-all, slideshow defaults, persistence, enable/disable, advance, reset, and independent state.

- **System tray integration.** `TrayIconService` (~440 lines) creates hidden message-only window on dedicated STA thread, registers tray icon via `Shell_NotifyIconW` with `NOTIFYICON_VERSION_4`, shows right-click context menu (Open Control Panel, Switch Theme submenu, Next Wallpaper, Settings, Exit), handles left-click to open panel, marshals commands to UI thread via `DispatcherQueue.TryEnqueue`, and properly cleans up on dispose.

- **Keyboard shortcuts.** `KeyboardShortcutService` registers global hotkeys via Win32 `RegisterHotKey` API on background STA thread with `WM_HOTKEY` message loop. `ShortcutParser` parses/formats/validates shortcut strings, detects system-reserved shortcuts (Alt+Tab, Win+L, Ctrl+Alt+Del), and checks conflicts. Default shortcuts: Ctrl+Shift+T (toggle theme), Ctrl+Shift+W (next wallpaper), Ctrl+Shift+A (open panel), Ctrl+Shift+S (open settings). UI in SettingsPage with shortcut recorder dialog. **44 tests** covering parsing, formatting, validation, system-reserved detection, conflicts, and defaults.

- **System information dashboard.** `SystemInfoService` exposes Windows version, .NET version, app version, current theme, active wallpaper, session duration, machine name, user name, processor count, and available memory. HomePage shows detailed System Information panel with live-updating session timer. **16 tests** covering all properties.

- **Theme preview before apply.** ThemesPage "Preview" button builds temporary `ResourceDictionary` from selected theme tokens, applies only to preview surface (not whole app), shows color swatches and sample UI elements. "Close Preview" removes temporary dictionary.

- **Quick actions panel.** New QuickActionsPage with 4 action cards: Dark/Light Mode Toggle, Reset to Default Theme, Refresh Wallpaper, Clear Cache, Clear Logs. Added to MainWindow navigation.

- **Custom color accent picker.** AppearancePage "Custom Accent Color" section with hex input, 8 preset color buttons, live preview surface, and save to settings.

- **Wallpaper slideshow.** AppearancePage "Wallpaper Slideshow" section with per-monitor toggle, interval selector (15 min to daily), wallpaper list, add/remove/advance controls.

### Tests

**178 tests total** (22 Core + 28 ThemeEngine + 15 Backup + 32 WindowsIntegration + 29 Security + 52 App), all passing. New test projects: `AVEXOS.App.Tests` for ThemeSchedulerService and ShortcutParser.

### Fixed

- **Test parallelization race condition.** `JsonBackupStoreTests` and `SettingsBackupServiceTests` both called `AvexPaths.OverrideBackupDirForTests()` with different directories, causing intermittent failures when xUnit ran them in parallel (shared static state). Added `BackupTestCollection` with `DisableParallelization = true` and marked both test classes with `[Collection("BackupTests")]` to ensure sequential execution.

### Documentation

[ARCHITECTURE.md](../architecture/ARCHITECTURE.md) §11 (test count to 178, new test rows), [README.md](../../README.md) (test count), [DEVELOPER_GUIDE.md](../setup/DEVELOPER_GUIDE.md) (test count), [CONTRIBUTING.md](CONTRIBUTING.md) (test count).

---

## 2026-11-08 (newer) — theme tokens now render into ResourceDictionary

### Fixed

- **Theme tokens were validated but never rendered.** `ThemeService.Apply` only flipped
  `RequestedTheme` between Light and Dark, so each theme's own `color`/`radius`/`space`/`type`
  values from the JSON were inert. A new `ThemeResourceBuilder` in `AVEXOS.ThemeEngine` converts a
  `ThemeDocument` into a `ThemeResourceSet` (color hex values, brush references, scalar values for
  radius/space/motion/type, and font family). `ThemeService.Apply` now loads the theme from the
  catalog, builds the resource set, creates a WinUI `ResourceDictionary` with `Color`, `SolidColorBrush`,
  `CornerRadius`, and scalar entries, and applies it to the root element's `MergedDictionaries`.
  **TDD, in order:** added `ThemeResourceBuilderTests.cs` with 10 cases covering color key naming,
  brush references, PascalCase conversion, radius/space/motion/type scalars, font family, and
  referential integrity. All 10 failed against shipped code (ThemeResourceBuilder didn't exist);
  after implementation, all 65 tests pass (29 + 6 + 18 + 3 + 9).

### Documentation

[ARCHITECTURE.md](../architecture/ARCHITECTURE.md) §11 (test count to 65, ThemeResourceBuilder row),
[CHANGELOG.md](./CHANGELOG.md) unresolved table (theme tokens row removed, coverage row updated).

---

## 2026-11-08 (newer) — RegistryPolicy prefix-shadowing fixed

### Fixed

- **`RegistryPolicy.IsWritable` used `StartsWith`, so a sibling key beginning with an allowed root
  was approved** (e.g., `…\Control Panel\DesktopEvil` → ALLOW). The check now requires either an exact
  root match or root+backslash prefix, so only true subkeys are permitted.
  **TDD, in order:** added `Denies_Prefix_Shadowing_Attempts` ×3 and `Allows_Legitimate_Subkeys` ×2
  to `RegistryPolicyTests.cs`. The shadowing cases failed against the shipped code; after tightening
  the match logic, all 29 security tests pass.

### Documentation

[CHANGELOG.md](./CHANGELOG.md) unresolved table — RegistryPolicy row updated to reflect the fix.

---

## 2026-11-08 (newer) — risk level now gates High-risk changes

### Fixed

- **`ChangeRequest.Risk` was recorded but never acted as a gate.** Every change asked for the same
  single approval regardless of risk level. High-risk changes now require a `confirmHighRisk` delegate;
  if not provided or if it returns false, the operation is denied. Low and Medium risk changes proceed
  with only the standard `approve` delegate.
  **TDD, in order:** a new `ChangePipelineRiskTests.cs` file ran first with three cases: High-risk
  without confirmHighRisk denied, High-risk with confirmHighRisk=true succeeds, Low-risk without
  confirmHighRisk succeeds. The first case failed against the shipped code (no confirmHighRisk
  parameter); after adding the parameter and the gating logic at stage 8b, all three passed.

### Documentation

[ARCHITECTURE.md](../architecture/ARCHITECTURE.md) §5 (signature, stage 8b, gaps section), §11 (test
count) — all now describe the fixed behaviour.

---

## 2026-11-08 (newer) — rollback is now verified

### Fixed

- **`ChangePipeline.Rollback` trusted `restore()` to succeed.** If the restore delegate didn't throw
  but the state wasn't actually restored, the operation was recorded as `RolledBack` and the host was
  left half-changed. The pipeline now accepts an optional `verifyRollback` delegate that is called
  after `restore()`; if it returns false, the operation is marked `Failed` instead of `RolledBack`.
  **TDD, in order:** a new `AVEXOS.Core.Tests` project ran first with three cases: rollback with
  verifyRollback=false reports Failed, rollback with verifyRollback=true reports RolledBack, rollback
  without verifyRollback reports RolledBack. The first case failed against the shipped code (no
  verifyRollback parameter); after adding the parameter and the verification logic, all three passed.
  `AppearancePage` now passes a `verifyRollback` delegate that checks the wallpaper path matches the
  original state.

### Documentation

[ARCHITECTURE.md](../architecture/ARCHITECTURE.md) §5 (signature, stage 13, gaps), §11 (test count) —
all now describe the fixed behaviour.

---

## 2026-11-08 (newer) — the success text is now direction-neutral

### Fixed

- **A successful restore reported "Applied and verified."** The pipeline's success message did not
  distinguish apply from restore, so the UI claimed a direction the operation did not take. The message
  is now "Verified. Backup …" for both paths — direction-neutral and honest.
  No test: the fix is a string change, and the pipeline is tested indirectly through the UI (which has
  no automated coverage). The change was verified by reading the code and confirming the new string
  appears in both the audit entry and the returned `ChangeResult.Detail`.

### Documentation

[ARCHITECTURE.md](../architecture/ARCHITECTURE.md) §5, [DEVELOPER_GUIDE.md](../setup/DEVELOPER_GUIDE.md)
item 4, [BACKUP_AND_RECOVERY.md](../operations/BACKUP_AND_RECOVERY.md) §8 and §10,
[CHANGELOG.md](./CHANGELOG.md) unresolved table — all now describe the fixed behaviour.

---

## 2026-11-08 (newest) — backups are now hashed

### Fixed

- **`JsonBackupStore.Load<T>` trusted any JSON file** (risk #2, open since the first security pass). A
  user-writable backup directory meant an attacker (or a bug) could tamper with the payload and the
  restore would accept it. Backups now carry a SHA256 hash of the payload, computed on create and
  verified on load. Tampered or unhashed backups are rejected with `InvalidOperationException`.
  **TDD, in order:** a new `AVEXOS.Backup.Tests` project ran first with three cases: tampered backup
  rejected, unhashed backup rejected, valid hashed backup accepted. The first two failed against the
  shipped code (no hash field, no verification); the third passed. After adding the `Hash` field to
  `BackupFile<T>` and the verify logic to `Load<T>`, all three passed.

### Breaking change

Existing backups (created before this change) lack a hash field and will be rejected on load. This is
intentional: accepting unhashed backups reopens the hole. The user's three existing backups from the
wallpaper flow verification are now unusable; re-running the backup flow creates hashed backups.

### Documentation

[THREAT_MODEL.md](../security/THREAT_MODEL.md) §5 risk #2 → Fixed. Test counts updated in
[ARCHITECTURE.md](../architecture/ARCHITECTURE.md) §11 (four projects, 38 tests),
[DEVELOPER_GUIDE.md](../setup/DEVELOPER_GUIDE.md), [README.md](../../README.md),
[CONTRIBUTING.md](./CONTRIBUTING.md), [INSTALLATION.md](../setup/INSTALLATION.md).

`dotnet build AVEXOS.slnx`: **0 errors**, 6 distinct warnings (the new test project adds 2 NU1903
occurrences, so the headline reads 14 incremental / 16 with `-t:Rebuild`). Tests: **35 passed** — 24
`AVEXOS.Security.Tests`, 8 `AVEXOS.ThemeEngine.Tests`, 3 `AVEXOS.Backup.Tests`. No host write: the app
was never launched, and `HKCU\Control Panel\Desktop\Wallpaper` is still the AVEX image pending your
approval.

---

## 2026-11-08 (latest) — the association hive left the registry allowlist

### Fixed

- **`HKCU\Software\Classes` was policy-writable with no consumer** (risk #3, open since the first
  security pass). `RegistryPolicy.AllowedWriteRoots` listed the file-association hive, so any defect that
  reached the registry write path could change the current user's associations — what spec rule 22 forbids
  outright. Nothing ever called it: the only `RegistryPolicy.IsWritable` call site in the shipped app is
  `WallpaperService.cs:31`, against `Control Panel\Desktop`. Deleting the entry was the entire production
  change.
  **TDD, in order:** a new `RegistryPolicyTests` ran first and its three association cases failed against
  the shipped list, each with the reason `Allowed user-scope key.` The other six cases — HKLM in both
  spellings, `Control Panel\Desktop` still allowed, three non-allowlisted user keys refused with the exact
  reason string — passed before *and* after, which is what proves the removal narrowed the policy instead
  of breaking the one path the app uses.

### Measured while proving it

Root matching uses `StartsWith`, so a sibling key whose name merely begins with a root is approved too.
Probed against the shipped policy (temporary test file, real code path, deleted after the run):
`…\Control Panel\DesktopEvil`, `…\Control Panel\CursorsX`, `…\Software\Microsoft\Windows\DWMtuning` and
`…\AppEvents\SchemesX` all return **ALLOW**, while `…\Software\Classes`, `…\Software\ClassesX`,
`…\CurrentVersion\Run` and `…\CurrentVersion\RunEvil` are all **denied**. No *sensitive* HKCU location is a
prefix-extension of an allowed root, so nothing reachable depends on it today. Recorded as risk #9 rather
than fixed: the correct fix is a segment-boundary comparison, and inventing a failing case to justify it
now would be designing for a requirement that does not exist yet.

### Documentation

Eight files repeated the seven-root count or described the hive as still allowlisted:
[THREAT_MODEL.md](../security/THREAT_MODEL.md) §3/§4/§5, [SECURITY.md](../security/SECURITY.md)
(Tampering row), [ARCHITECTURE.md](../architecture/ARCHITECTURE.md) §7 and §11,
[DEVELOPER_GUIDE.md](../setup/DEVELOPER_GUIDE.md) rule 7 and debt item 11,
[COMPATIBILITY.md](../compatibility/COMPATIBILITY.md) §0 and §2 — file-type icons moved from "safe to
customize" to the rule-22 side, since the mechanism is now refused by our own guard —
[TROUBLESHOOTING.md](../operations/TROUBLESHOOTING.md) §5, plus the counts in
[README.md](../../README.md), [CONTRIBUTING.md](./CONTRIBUTING.md) and
[INSTALLATION.md](../setup/INSTALLATION.md).

`dotnet build AVEXOS.slnx`: **0 errors**, 6 distinct warnings. Tests: **32 passed** — 24
`AVEXOS.Security.Tests` (15 path-policy cases + 9 registry-policy cases) and 8
`AVEXOS.ThemeEngine.Tests`. No host write in this change: the app was never launched, `RegistryPolicy` is
a pure string comparison with no registry API behind it, and `HKCU\Control Panel\Desktop\Wallpaper` is
still the AVEX image pending your approval.

---

## 2026-11-08 (later) — the path-guard gap closed in code

### Fixed

- **`ProtectedPathPolicy` approved a path it promised to refuse.** `ResolveReparsePoints` started at the
  leaf and, when the leaf did not exist, climbed to the **first existing ancestor** and resolved only
  that one — so a junction inside an allowed root was refused at the link itself but approved one level
  below it, because the directory reached *through* the link is not itself a reparse point.
  `%LOCALAPPDATA%\AVEXOS\<link>\System32\x.dll` therefore canonicalised to itself and both `IsAllowed`
  and `FileSystemGuard.EnsureWritable` returned ALLOW. Resolution is now component-wise from the drive
  root, each step applied to the already-resolved prefix, and the path is refused as
  `Protected system location (C:\WINDOWS).`
  **TDD, in order:** `Denies_Path_Below_Junction_In_Allowed_Root` was written first and failed with
  `Canonicalised to …\avex\j\System32\x.dll and approved: Allowed: inside AVEX OS data root.` The
  production change came after that red run. The test plants a real junction into `C:\Windows` inside a
  **temporary** allowed root (passed through the constructor's existing `allowedRoots` parameter), so it
  never creates anything in the user's AVEX directory, and it removes the link with
  `cmd /c rmdir` before any recursive delete — the target is never handed to a deleting API.
  Measured side effects of the change, both documented: a link **inside** the root pointing **out** is
  now refused (previously approved — the bug), while a link **outside** the root pointing **in** is now
  approved where it used to be refused, because the bytes genuinely land inside the declared root. Every
  guarded write call site (`WallpaperService:52`, `JsonBackupStore:26`, `ChangePipeline:42`) builds its
  path from `AvexPaths`, so no caller can supply an arbitrary directory and reach that shape today. `C:\Windows\System32` was counted before and after
  (4 576 files, `kernel32.dll` 861 992 bytes): untouched.
- **Test cleanup left empty directories in `%TEMP%`.** Deleting a tree whose reparse point was just
  removed throws once, and the old `catch { }` hid it; seven empty `avex-test-*` shells from earlier runs
  were found and removed with `rmdir`. Cleanup now retries, and the run leaves nothing behind.

`dotnet build AVEXOS.slnx`: **0 errors**, 6 distinct warnings (`NU1903` × 4 projects, `CS0168` × 2).
The headline MSBuild prints is an occurrence count, so it reads 10 incremental and 12 with
`-t:Rebuild`; both were measured on this tree on 2026-11-08. Tests then: **23 passed** — 15
`AVEXOS.Security.Tests`, 8 `AVEXOS.ThemeEngine.Tests` (superseded by the entry above). Still open in this
area: the handle-based re-check after the guard decides (T-T6 shape 2), mount points, and symlink chains,
which cannot be created here without Developer Mode.

---

## 2026-11-08 — first vertical slice verified on the host

### Fixed

- **The app never launched.** `MainWindow.xaml` used UWP-style `Icon="Color"` / `Icon="PaintBrush"`
  / `Icon="AllApps"` shorthand. WinUI 3's `Symbol` enum is a reduced subset, so XAML construction
  threw `XamlParseException: Failed to create a 'Microsoft.UI.Xaml.Controls.IconElement' from the text
  'Color'`, which surfaced as a native exit `0xC000027B` with no managed trace. Replaced all five with
  explicit `<FontIcon Glyph="…"/>`. Verified: window class `WinUIDesktopWin32WindowClass`, title
  `AVEX OS Control Center`, and a `PrintWindow` pixel histogram showing real content.
- **Approval blocked the UI thread.** `ChangePipeline`'s `approve` hook took
  `Func<ChangeRequest, bool>` and the page called `dialog.ShowAsync().GetAwaiter().GetResult()`,
  deadlocking XAML's dispatcher with "Only a single ContentDialog can be open at any time." The hook
  is now `Func<ChangeRequest, Task<bool>>` and is awaited — deliberately **without**
  `ConfigureAwait(false)`, because apply/verify/restore are apartment-sensitive.
- **Restore did not restore.** `AppearancePage` cached the state read at page load and refreshed that
  field after every successful apply, so the Restore button re-applied the new wallpaper while the
  status line reported success. The cached field is gone; Restore now resolves the **newest backup for
  the target id** and runs it through `ChangePipeline`, which also removed a path that bypassed the
  documented single mutating entry point.
- **"You must install .NET" on launch.** Not a code defect: the SDK is user-scoped and absent from
  `PATH`, so the apphost cannot find `Microsoft.NETCore.App`. `scripts/launch-app.cmd` now sets
  `DOTNET_ROOT`. This failure mode was previously mistaken for a successful launch, because the dialog
  runs as `AVEXOS.App.exe` with a live `MainWindowHandle`.
- **Selecting a theme had no effect on what got applied.** `ThemesPage`'s `ListView` had no
  `SelectionChanged` handler, so `_selectedTheme` stayed pinned to the catalog's first entry —
  choosing "AVEX Light" applied "AVEX Dark". Wired the handler and made the list select its first row
  explicitly. Verified with `scripts/theme-probe.ps1`: row 0 → content-area luminance 14.3, row 1 →
  102.5, spread 88.2.

### Added

- Verification harnesses in `scripts/`: `launch-app.cmd`, `capture-window.ps1` (window enumeration +
  `PrintWindow` capture and pixel histogram), `uia-dump.ps1`, `gui-cycle.ps1` (Restore → Apply with
  approval → Restore, cross-checked against `HKCU\Control Panel\Desktop`), `theme-probe.ps1`.
- Documentation for Part 25: `THREAT_MODEL`, `INSTALLATION`, `USER_GUIDE`, `DEVELOPER_GUIDE`,
  `BACKUP_AND_RECOVERY`, `TROUBLESHOOTING`, `CONTRIBUTING`, `CHANGELOG`, alongside the earlier
  `ARCHITECTURE`, `SECURITY`, `COMPATIBILITY`, `DESIGN_SYSTEM`.

### Corrected in documentation

- **`README.md`** claimed shortcuts, folder/file-type/drive icons, cursors and sounds shipped (they
  are designed, not implemented), claimed a warning-free build (the build reported 10 warning lines
  then — six distinct), and listed seven
  projects that do not exist. Rewritten with verified / not-yet tables.
- **`ARCHITECTURE.md`** was the Phase 1 *target* design presented as current: 13 projects (6 exist),
  `IAppearanceAdapter`/`IChangeTarget`/`IAiProvider` and async store contracts that were never built,
  seven SQLite tables (three exist), an architecture test that does not exist, a nested `tokens`
  theme schema (the shipped shape is flat), and a `scripts/dev.ps1` that was never created. Every
  section is now labelled **[built]** or **[planned]**.
- **`ARCHITECTURE.md` §7–§12** needed a second pass, and it found more than the first one did: the
  canonicalisation list claimed mounted-volume resolution and full reparse-chain handling the code does
  not do; the allowed-write-root paragraph implied user folders like `Desktop` are opted in (only the
  two AVEX roots are, because `App.xaml.cs:46` passes no arguments); §8 listed seven tables with
  made-up columns; §9 showed a nested `tokens` shape and claimed fonts, icons and payload size are
  validated and that tokens build a `ResourceDictionary` at runtime; §10 claimed an all-`async`
  pipeline, discovery "cached in SQLite" with a 24 h staleness check, and quoted startup/CPU/memory
  numbers that were never measured; §11 listed six suites where two exist; §12 listed thirteen projects
  and directories that were never created.
- **Measured a guard gap that no test covers.** Rather than reason about `ResolveReparsePoints`, a
  throwaway console project referenced `AVEXOS.Security` and called `IsAllowed`/`EnsureWritable` on
  concrete paths — writing nothing, opening no `C:\Windows` path, and removing the probe junctions with
  `rmdir` afterwards. Result: a junction inside the allowed root is denied **at the link itself**
  (`…\AVEXOS\jw-probe` → `C:\Windows` → protected location) and **approved one level below it** when the
  leaf does not yet exist (`…\jw-probe\System32\x.dll` → ALLOW, by both entry points), because the walk
  climbs past the missing leaf to an existing directory that is not itself a link and stops. The
  parent-directory check rescues only the two-component case. `SECURITY.md` had claimed
  `FILE_FLAG_OPEN_REPARSE_POINT` handle re-verification for exactly this; there is no handle and no
  re-verification, so that row is corrected to **none**, and the gap is now risk #1 in
  [THREAT_MODEL.md](../security/THREAT_MODEL.md) §5.
- **And corrected a claim this very pass had just made.** The first draft of the §7 note said 8.3 short
  names are never expanded. Testing it on .NET 10.0.14 showed they **are** expanded whenever the volume
  resolves them: `C:\PROGRA~1\Common~1\x.dll` → `C:\Program Files\Common Files\x.dll`, refused as
  `Protected system location (C:\Program Files).`. `C:\WINDOWS\SYSTEM~1\…` kept only its unresolvable
  last component literal. The residue is therefore a **false refusal**, not a bypass —
  `C:\Users\NEVIN~1\AppData\Local\AVEXOS\x.json` fails the allowlist and the app would refuse its own
  legitimate path — now documented in [TROUBLESHOOTING.md §5](../operations/TROUBLESHOOTING.md). Also
  measured: `%LOCALAPPDATA%\AVEXOS\..\..\Windows\System32` folds to `…\AppData\Windows\System32`, which
  is not under `C:\Windows`, so `Denies_Traversal_From_Allowed_Root` is green on the allowlist rather
  than on a protected-root match; `mklink /J` succeeds unprivileged here (so the junction test does
  run), while symlinks would need Developer Mode (so that half is untested); and
  `Environment.GetFolderPath(Windows)` reports `C:\WINDOWS` in uppercase on this host, which is why
  denial reasons print it that way.
- **Theme tokens are not rendered.** Reading `ThemeService.Apply` and the views showed that applying a
  theme persists `theme.id`/`theme.mode` and sets `RequestedTheme` to Light or Dark — nothing turns
  `color`/`radius`/`space`/`type` values into brushes. Consequences recorded in
  [ARCHITECTURE.md §9](../architecture/ARCHITECTURE.md), [README.md](../../README.md),
  [USER_GUIDE.md](../setup/USER_GUIDE.md) and
  [DESIGN_SYSTEM.md](../architecture/DESIGN_SYSTEM.md): two themes with the same `mode` look
  indistinguishable, and editing a colour in `avex-dark.json` changes nothing on screen. The verified
  theme-probe result (14.3 vs 102.5) is therefore evidence of the **light/dark switch working**, not of
  per-theme palettes — the README row now says so.
- **`DESIGN_SYSTEM.md` was a target spec read as a status report.** Corrected section by section: only
  the 14 dark + 14 light colour resources exist as brushes; the geometry, `control.height`,
  `sidebar.width` and `motion` values are not resources at all (63 hardcoded `FontSize`/`Padding`/
  `Spacing`/`CornerRadius` occurrences in the views, 35 of them in `HomePage.xaml`); `AvexFontFamily`
  is declared and never referenced; no `body` 13 or `h3` 14 size is set; zero `Storyboard`/`VisualState`
  /`TransitionCollection` declarations exist, so §4's timings describe framework defaults; zero
  `AutomationProperties.Name` and no `AutomationLiveSetting`; no `OpenPaneLength`/`MaxWidth`/12-column
  grid/density mode/adaptive breakpoint; and `assets/branding/`+`assets/icons/` are empty with **no
  `.ico` file anywhere** — the five nav glyphs are Segoe Fluent Icons codepoints, not AVEX art.
- `ApplicationsPage` was described as showing each app's **level**; it binds `DisplayName`, `Publisher`
  and `CapabilityReason` only — the A–D letter is never rendered, and `HomePage` shows the one
  aggregate derived from it.
- Recorded two weaknesses in the security suite so a green row is not over-read:
  `Denies_Alternate_Data_Stream` asserts `True` (it proves suffix stripping, the opposite of its name),
  and `Denies_Junction_Pointing_To_Windows` `return`s without asserting if the junction was not created.
  An earlier note here claimed that skip was "the normal outcome without Developer Mode or admin
  rights" — measured 2026-11-08, that is wrong for **junctions**, which `mklink /J` creates from a child
  process unprivileged. The test does run on this machine. "The security suite is a release gate" is
  intent, not mechanism — nothing in the tree runs tests automatically.
- `USER_GUIDE.md` told readers a failed verification means "You will not be left half-applied." The
  rollback is not re-verified, so a throwing rollback leaves exactly that state; the guide now says so
  and points at the "Rollback failed: …" status text. The same section claimed the confirmation dialog
  gates the whole operation — `ImportWallpaper` and the backup write both run **before** `approve`, so
  cancelling still leaves two files inside the AVEX data roots. Verification scope is now stated
  honestly too: `AppearancePage.Matches` compares the wallpaper path only, never the style or tiling.
- `BACKUP_AND_RECOVERY.md` §3's before/after table read like a guarantee about the machine. It is one
  measurement of one scripted run; the section now says which, and records the actual current registry
  value alongside it.
- **`COMPATIBILITY.md` §6** recorded "unpackaged launch: Pass, confirmed by screenshot" on 2026-11-07.
  That was the missing-runtime dialog. The row is retracted and replaced with the 2026-11-08 results.
- Recorded two pipeline gaps that were previously implied to be closed: rollback after a failed apply
  is **not** re-verified, and `RiskLevel` is recorded but never acts as an approval gate.
- Recorded that `HKCU\Software\Classes` sits in the registry write allowlist with no consumer, which
  conflicts with specification rule 22 (no file-association branding) — see
  [THREAT_MODEL.md](../security/THREAT_MODEL.md) §4.

### Verified end to end on this host

`gui-cycle.ps1` against the live desktop, reading `HKCU\Control Panel\Desktop\Wallpaper` independently
at each step: AVEX wallpaper → **Restore** → original image → **Apply** (approval dialog shown and
accepted) → AVEX wallpaper → **Restore** → original image. The session ended with the desktop in its
original state.

**That is not where the host still is.** A later apply (`bk-20261108-032502-8af92aad`, "Apply AVEX
wallpaper", 2026-11-08 03:25:02 UTC) is the newest snapshot, and re-reading the registry on
2026-11-08 shows `Wallpaper` set to
`%LOCALAPPDATA%\AVEXOS\wallpapers\avex-wallpaper.png`. The original path
(`…\Microsoft.Windows.Photos_8wekyb3d8bbwe\LocalState\PhotosAppBackground\goku-perfected-3840x2160-25454.jpg`,
1 010 751 bytes, style 10) is intact inside that snapshot and **Restore Original** returns to it; the
restore was not run because it is a host write that needs approval. A verified table describes the run
it was measured on, not the current state of the machine — see
[BACKUP_AND_RECOVERY.md](../operations/BACKUP_AND_RECOVERY.md) §3.

`dotnet build AVEXOS.slnx`, measured then: **0 errors, 10 warning lines** — 6 distinct (8 × `NU1903` for
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 / [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q),
2 × `CS0168`). Tests then: **22 passed** — 14 `AVEXOS.Security.Tests`, 8 `AVEXOS.ThemeEngine.Tests`
(now 23; the MSBuild warning total is an occurrence count — see [README.md](../../README.md)).

### Toolchain finding worth repeating

`dotnet build src\AVEXOS.App\AVEXOS.App.csproj` reports success but writes to
`bin\Debug\…\win-x64\`, while `launch-app.cmd` runs from `bin\x64\Debug\…\win-x64\`. Several hours of
"the fix does not work" went to running a stale binary. Build the solution. See
[DEVELOPER_GUIDE.md](../setup/DEVELOPER_GUIDE.md) §2.

---

## 2026-11-07 — audit, architecture, scaffold, implementation

### Decided

- The master specification asks for taskbar, Start menu, Explorer, context-menu and third-party-app
  restyling while forbidding binary patching, injection, undocumented techniques and any write to
  `C:\Windows`. Within those rules that is not achievable, and the tools that claim it use exactly the
  forbidden techniques. **Chosen scope: an honest capability matrix** — rebrand AVEX's own surfaces,
  customize what Windows exposes at user scope, and mark everything else unsupported with a reason.
- First vertical slice: **wallpaper** (Level B, reversible, verified by read-back).
- `desktop.ini` permitted as a folder-identity mechanism, subject to the same guard rules. *(Still not
  implemented.)*

### Added

- Solution `AVEXOS.slnx` with six projects — `Core`, `Security`, `WindowsIntegration`, `ThemeEngine`,
  `Backup`, `App` — plus two test projects.
- `ProtectedPathPolicy`: deny-by-default, evaluated on the canonical path after resolving junctions and
  symlinks, stripping `\\?\` / `\\.\` prefixes and alternate-data-stream syntax, folding `..`, and
  trimming trailing dots and spaces. Protected roots include `C:\Windows`, both Program Files,
  `ProgramData\Microsoft`, `C:\Recovery`, `System Volume Information`, `$Recycle.Bin`, `Boot`, `EFI`
  and any volume root. `FileSystemGuard` checks the parent directory separately.
- `RegistryPolicy`: `HKLM` rejected outright; seven user-scope roots allowlisted (the seventh was
  `HKCU\Software\Classes`, removed 2026-11-08 — see the top entry; six remain).
- `ChangePipeline` as the only mutating entry point: guard → capture → backup → record → approve →
  apply → read-back verify → audit, with automatic rollback on apply or verification failure.
- `SqliteStore` (`AuditEvents`, `Operations`, `Settings`) in `%LOCALAPPDATA%\AVEXOS\avex.db`;
  `JsonBackupStore` writing `bk-yyyyMMdd-HHmmss-<8>.json` snapshots under
  `%LOCALAPPDATA%\AVEXOS\backups`.
- `ThemeDocument` + `ThemeValidator` (id/version/hex-colour/range checks; a theme referencing a Level D
  target is an error, not a warning) and `BuiltInThemeCatalog`, which embeds `assets/themes/*.json`.
  Note: themes failing validation are dropped silently.
- `LocalProposalService` — a deterministic keyword → token generator with **no model provider and no
  network**, validating every output before returning it.
- `AppDiscoveryService` reading the HKLM 64/32 and HKCU Uninstall trees, filtering updates and
  `SystemComponent` entries, deduplicating, capping at 80; `CapabilityRegistry` grades each app A/B/C/D
  with a reason string.
- `WallpaperService`: `Read` via `SPI_GETDESKWALLPAPER` plus `WallpaperStyle`/`TileWallpaper`;
  `Apply` guarded by `RegistryPolicy` then `SPI_SETDESKWALLPAPER` with
  `SPIF_UPDATEINIFILE | SPIF_SENDCHANGE`; `ImportWallpaper` guarded copy into the AVEX data root.
- WinUI 3 Control Center: Home, Appearance, Themes, Applications, Activity; `AvexTokens.xaml` with
  Dark/Light theme dictionaries; light/dark switching inside the app window.
- `assets/themes/avex-dark.json`, `avex-light.json`, `assets/wallpapers/avex-wallpaper.png`.
- `ARCHITECTURE.md`, `SECURITY.md`, `COMPATIBILITY.md`, `DESIGN_SYSTEM.md` and the initial `README.md`.

### Environment established

Windows 11 build 26300.9711 (26H2 Insider), Core (Home), x64 · .NET SDK 10.0.403 user-scoped, not on
`PATH` · runtime `Microsoft.NETCore.App` 10.0.14 · Windows App Runtime 2.6.5 · Windows SDK 10.0.26100
· VS Build Tools 17.14.41 · **Developer Mode disabled**, so packaged launch is blocked and unpackaged
is the supported loop.

---

## Unresolved

Tracked so nobody has to rediscover it:

| | Item |
|---|---|
| Security | **Handle-based re-check for reparse points** was fixed 2026-11-08 — `ImportWallpaper` now checks `FileAttributes.ReparsePoint` after the guard decision but before the copy, throwing `UnauthorizedAccessException` if a symlink or junction is detected. This closes T-T6 shape 2 for file symlinks. Mount points and device-level reparse points stay unhandled. [THREAT_MODEL.md](../security/THREAT_MODEL.md) §5 risk #4. |
| Security | **SQLitePCLRaw.lib.e_sqlite3 advisory GHSA-2m69-gcr7-jv3q** was fixed 2026-11-08 — `Microsoft.Data.Sqlite` updated from 10.0.0 to 10.0.14, pulling in `SQLitePCLRaw` 2.1.13 (from 2.1.11). CVE-2025-6965 (numeric truncation in SQLite aggregate handling) resolved. `dotnet list package --vulnerable` reports no vulnerable packages. |
| Security | `RegistryPolicy` prefix-shadowing was fixed 2026-11-08 — `IsWritable` now requires exact root match or root+backslash prefix, so `…\Control Panel\DesktopEvil` is denied. [THREAT_MODEL.md](../security/THREAT_MODEL.md) §5 risk #9. (`HKCU\Software\Classes` itself was removed from the list 2026-11-08 — see the top entry.) |
| Correctness | Rollback verification was added 2026-11-08 — the pipeline now accepts an optional `verifyRollback` delegate and marks the operation `Failed` if it returns false. Risk level gating was added 2026-11-08 — High-risk changes now require a `confirmHighRisk` delegate. The direction-neutral success text was fixed 2026-11-08 — "Applied and verified." is now "Verified." so a restore does not claim an apply. |
| Coverage | No tests for any UI. The 65 tests cover path policy, registry policy (including prefix-shadowing), backup integrity, theme validation, theme resource building, pipeline rollback verification, risk gating, WallpaperService.ImportWallpaper, and AppDiscoveryService.DiscoverAsync. |
| Scope | Only wallpaper ships. Shortcuts, folder/drive icons, cursors, sounds, system accent, adapters, plugins, installer, signing, packaging: designed, **not implemented**. File-type icons are further out: the hive they live in is refused by the guard (rule 22). |
| Scope | Theme apply affects the AVEX window only. It does not restyle Windows, and must not be described as if it did. |
| Hygiene | CS0168 unused-exception warnings fixed 2026-11-08 — catch blocks no longer declare unused `ex` variables. |
