# Developer Guide

Everything here was checked against the code and the running app on this host on 2026-11-08. Where a
capability is only designed, it says so.

---

## 1. What is actually in the tree

Six projects build, two test projects run, and one change flow works end to end.

| Project | Ships | What it does today |
|---|---|---|
| `AVEXOS.Core` | yes | Models, contracts, `ChangePipeline`, `AvexPaths`, `AvexJson` |
| `AVEXOS.Security` | yes | `ProtectedPathPolicy`, `FileSystemGuard`, `RegistryPolicy`, `SqliteStore` |
| `AVEXOS.WindowsIntegration` | yes | `WallpaperService`, `AppDiscoveryService`, `CapabilityRegistry`, `NativeMethods` |
| `AVEXOS.ThemeEngine` | yes | `ThemeDocument`, `ThemeValidator`, `BuiltInThemeCatalog`, `LocalProposalService` |
| `AVEXOS.Backup` | yes | `JsonBackupStore` |
| `AVEXOS.App` | yes | WinUI 3 Control Center: Home, Appearance, Themes, Applications, Activity |
| `AVEXOS.Security.Tests` | yes | 24 cases: 15 over `ProtectedPathPolicy`, 9 over `RegistryPolicy` |
| `AVEXOS.ThemeEngine.Tests` | yes | 8 tests over `ThemeValidator` |
| `AVEXOS.Backup.Tests` | yes | 3 cases over `JsonBackupStore` integrity |

There is **no** `Discovery`, `Compatibility`, `IconManager`, `Monitoring`, `Diagnostics`, `AI` or
`Plugins` project. Discovery lives in `WindowsIntegration`, proposals in `ThemeEngine`. See
[ARCHITECTURE.md](../architecture/ARCHITECTURE.md) §4 for the full built-vs-planned split.

The single working mutation path is wallpaper: read → back up → approve → apply → read back → restore.
Everything else in the UI is read-only inventory or app-internal.

---

## 2. Build, run, test

```bat
set "DOTNET_ROOT=C:\Users\<you>\AppData\Local\Microsoft\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
dotnet build AVEXOS.slnx
scripts\launch-app.cmd
dotnet test tests\AVEXOS.Security.Tests\AVEXOS.Security.Tests.csproj --no-build
dotnet test tests\AVEXOS.ThemeEngine.Tests\AVEXOS.ThemeEngine.Tests.csproj --no-build
```

Verified on this host: **0 errors, 6 distinct warnings; 178 tests pass (22 + 28 + 15 + 32 + 29 + 52)**. MSBuild's headline
warning total is an occurrence count and reads 10 incremental / 12 with `-t:Rebuild` — see the README.

### Build the solution, never a single csproj

This is the trap that wastes the most time here, and it is silent.

`AVEXOS.App` sets `<Platforms>x64</Platforms>`, and `AVEXOS.slnx` pins `<Platform Project="x64" />`.
Going through the solution, output lands in:

```
src\AVEXOS.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\
```

Building the csproj directly (`dotnet build src/AVEXOS.App/AVEXOS.App.csproj`) still reports
`Build succeeded` and `0 Error(s)`, but writes to a **different** directory:

```
src\AVEXOS.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64\
```

`scripts/launch-app.cmd` launches from the `bin\x64\Debug\...` path. So a csproj build "succeeds", the
app starts, and you are running a binary from before your edit — with no error anywhere to tell you.
This was reproduced: an edit plus a successful csproj build left `AVEXOS.App.dll` stamped with an
older time than its own source file, and the running app behaved exactly as the pre-edit code.

Three habits avoid it:

1. Always `dotnet build AVEXOS.slnx`.
2. Stop the app first — a running instance locks the output and the copy step fails.
3. When results look impossible, check the timestamp before believing anything:

```bash
ls -la --time-style=+%H:%M:%S src/AVEXOS.App/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/AVEXOS.App.dll
ls -la --time-style=+%H:%M:%S src/AVEXOS.App/Views/ThemesPage.xaml.cs
```

If the DLL is older than the source you changed, you are not testing your change.

### `DOTNET_ROOT` is not optional

The SDK is user-scoped and not on `PATH`. Without `DOTNET_ROOT` the apphost cannot find
`Microsoft.NETCore.App` and pops a **"you must install .NET"** dialog. That dialog's process *is*
`AVEXOS.App.exe`, has a window, and a live PID — so `Get-Process` and `MainWindowHandle` both look
like success. See [TROUBLESHOOTING.md](../operations/TROUBLESHOOTING.md) §1 for how to tell them apart.

---

## 3. Rules that are not negotiable in review

These come from the master specification and are already encoded in the guard. A change that breaks
one is rejected regardless of how well it works.

1. Nothing is written under `C:\Windows`, ever — including through a junction, symlink, `\\?\` prefix,
   8.3 name, or alternate data stream that resolves there.
2. No binary patching, no DLL injection, no disabling Defender / Firewall / UAC, no TrustedInstaller
   bypass.
3. Every host mutation goes through `ChangePipeline`. If a feature writes to the host some other way,
   it does not get merged.
4. Nothing is reported as applied until it has been **read back and compared**. A successful API call
   is not evidence.
5. Anything not supported is labelled unsupported with a reason. Silence counts as a claim.
6. AVEX data stays in `%LOCALAPPDATA%\AVEXOS` and `%APPDATA%\AVEXOS`.
7. File extensions and file-type associations are not branding levers (spec rule 22). This is enforced,
   not just stated: `HKCU\Software\Classes` was removed from the registry write allowlist on 2026-11-08
   and `RegistryPolicyTests` keeps it refused — see [THREAT_MODEL.md](../security/THREAT_MODEL.md) §4.

---

## 4. Adding a capability

Every capability is one service + one page action + one pipeline call. Wallpaper is the reference
implementation; copy its shape.

### Step 1 — service in `AVEXOS.WindowsIntegration`

Expose three operations and nothing else: read current state, apply a state, and (if files are
involved) import assets into the AVEX data root.

```csharp
public sealed class WallpaperService : IWallpaperService
{
    public AppearanceState Read();                    // SPI_GETDESKWALLPAPER + registry style
    public void Apply(AppearanceState state);         // registry policy check, then SPI_SETDESKWALLPAPER
    public string ImportWallpaper(string sourcePath); // guard-checked copy into %LOCALAPPDATA%\AVEXOS\wallpapers
}
```

Rules for this layer:

- Check before you write. Registry: `RegistryPolicy.IsWritable(fullKeyPath, out reason)` → throw
  `UnauthorizedAccessException` on false. Files: `_guard.EnsureWritable(path)` → use
  `guard.CanonicalPath` for the actual write, never the string you were handed.
- `Read()` must return the value Windows actually holds, so verification has something independent to
  compare against. If you read from your own cache, verification is theatre.
- Never write a user file in place. Import a copy into an AVEX directory and point the setting at it.

### Step 2 — register it

Interface in [`Contracts.cs`](../../src/AVEXOS.Core/Contracts.cs), implementation in
[`App.xaml.cs`](../../src/AVEXOS.App/App.xaml.cs#L42) `ConfigureServices()` as a singleton.

### Step 3 — call the pipeline from the page

```csharp
var result = await pipeline.ExecuteAsync(
    request:      new ChangeRequest("windows.desktop.wallpaper", "Wallpaper",
                    "Apply AVEX wallpaper", RiskLevel.Medium, PathToWrite: null),
    capture:      () => wallpaper.Read(),
    apply:        _   => wallpaper.Apply(newState),
    verify:       ()  => Matches(wallpaper, importedPath),
    restore:      original => wallpaper.Apply(original),
    approve:      req => ConfirmAsync("Apply Wallpaper", "...", "Apply"));
```

`PathToWrite` is only for changes that write a file. A registry-only change leaves it `null` — the
guard would reject a registry path as an invalid filesystem path, and registry writes are guarded
inside the service by `RegistryPolicy` instead.

The `restore` delegate is the pipeline's **automatic rollback** (used when apply or verify fails), not
the user's Restore button. See §5 for how the button works.

### Step 4 — declare the capability honestly

Add the app or surface to [`CapabilityRegistry.cs`](../../src/AVEXOS.WindowsIntegration/CapabilityRegistry.cs)
with a level and a reason. Default is **Level D**: "No documented theming API." Only claim A/B/C when
the target publishes a supported way to theme it — and if AVEX does not actually write it, say so in
the reason string. `windows.system.accent`, cursors and sounds are designed and **not implemented**;
do not mark them as working in UI text.

### Step 5 — test it

- `ThemeValidator`-style pure logic → xunit tests in `tests/`.
- Anything that touches the host → verify on the running desktop with the harnesses in §7, and record
  the evidence in [COMPATIBILITY.md](../compatibility/COMPATIBILITY.md) §6.

---

## 5. Restore is not "undo the last click"

The original mistake in this codebase: `AppearancePage` cached the state it read at page load in a
field, and re-read it after every apply. So "Restore" re-applied the wallpaper it had just set, while
the status line cheerfully said "Restored original wallpaper." The registry never changed back.
It looked correct because the code that set it was the same code that "restored" it.

The rule now: **the UI caches no state. Restore resolves the newest backup for that target id and
feeds it through the pipeline.**

```csharp
var snapshot = backups.List(50).FirstOrDefault(b => b.TargetId == WallpaperTarget);
var saved    = backups.Load<AppearanceState>(snapshot.Id);
// ... pipeline with apply: _ => wallpaper.Apply(saved)
```

Two consequences worth knowing before you add a second target:

- `IBackupStore.List()` orders by **filename descending**, not by `CreatedUtc`. That is correct only
  because ids embed a sortable `yyyyMMdd-HHmmss`. If you change the id format, ordering breaks
  silently and restore picks the wrong snapshot.
- `List(50)` is a truncation, so a very old original can fall out of the window. See
  [BACKUP_AND_RECOVERY.md](../operations/BACKUP_AND_RECOVERY.md) §5.

---

## 6. WinUI 3 gotchas that cost real debugging time

Learned the hard way on this host; all four produce a broken app with a misleading message.

1. **`Icon="Color"` style shorthand does not exist in WinUI 3.** The `Symbol` enum is a reduced subset
   versus UWP, and the XAML parser fails while constructing the page:
   `XamlParseException: Failed to create a 'Microsoft.UI.Xaml.Controls.IconElement' from the text
   'Color'`. It surfaces as a native exit code `0xC000027B` with no managed exception. Use explicit
   `<FontIcon Glyph="&#xE790;" />`.
2. **Never block the UI thread waiting on a dialog.** `dialog.ShowAsync().GetAwaiter().GetResult()`
   deadlocks XAML's dispatcher and throws "An async operation was not properly started. Only a single
   ContentDialog can be open at any time." The pipeline's `approve` hook is
   `Func<ChangeRequest, Task<bool>>` for exactly this reason — await it.
3. **Do not use `ConfigureAwait(false)` around apply/verify/restore.** `SystemParametersInfo` and
   registry writes are apartment-sensitive; the continuation must stay on the UI thread.
4. **Status text is direction-neutral.** A successful restore prints "Verified. Backup …" — the same
   wording as an apply. The pipeline's success message does not claim a direction, so tests and docs
   should not infer one from the string.

---

## 7. Verification harnesses

`scripts/` holds five tools. They are how this project proves things, so use them rather than
eyeballing a screenshot.

| Script | What it proves | Touches the host? |
|---|---|---|
| `launch-app.cmd` | Starts the built app with `DOTNET_ROOT` set | no |
| `capture-window.ps1` | The app really renders (window class + pixel histogram) | no |
| `uia-dump.ps1` | What the accessibility tree exposes | no |
| `gui-cycle.ps1` | Restore → Apply(+approval) → Restore, cross-checked against the real registry | **yes — changes your wallpaper, then restores it** |
| `theme-probe.ps1` | The selected row drives apply — dark 14.3 vs light 102.5 content luminance. It does **not** prove a theme's palette renders; that is debt item 9 in §9. | no |

### Driving the UI from a script

Synthetic pixel clicks work but are fragile here. What the harnesses rely on:

- `PrintWindow(hwnd, hdc, 2)` (`PW_RENDERFULLCONTENT`) captures WinUI content even when the window is
  occluded; `BitBlt` from the screen does not.
- **`GetWindowRect` is DPI-virtualised (this desktop runs at 125 %) while UIA
  `BoundingRectangle` reports physical pixels.** Mixing them puts clicks in the wrong place. Use UIA
  rects for both coordinates and centres.
- `NavigationViewItem` supports **SelectionItemPattern, not InvokePattern** — invoking it does nothing
  and reports success. The nav pane and content lists both expose `ListItem`; tell them apart by
  `Name` (`ThemeDocument` vs the nav label).
- Plain `Button` supports **InvokePattern**, which is far more reliable than a pixel click.
- A luminance measurement over the content region is a decent oracle for a theme change; the absolute
  threshold is arbitrary, so compare rows against each other (avex.dark ≈ 14.3, avex.light ≈ 102.5
  on this display) rather than trusting one cut-off.
- Windows PowerShell 5.1 reads `.ps1` files as **ANSI unless the file has a UTF-8 BOM**. A non-ASCII
  character (an em dash) inside a string breaks parsing mid-script. Keep these scripts ASCII-only.
- PowerShell turns every `Write-Output` inside a function into its return value, so diagnostics
  printed by a function called from an `if` vanish. Use `Write-Host` for harness chatter.

### Reading the ground truth

The app's own status line is *not* the oracle. Check what Windows holds:

```powershell
(Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper).Wallpaper
```

Compare it against a file inside `%LOCALAPPDATA%\AVEXOS\wallpapers\` to distinguish "AVEX applied"
from "AVEX restored the original".

---

## 8. Conventions

- **One mutating entry point.** UI → `ChangePipeline` → service → OS. No shortcuts.
- **Explicit interface implementation** where a class serves several contracts. `SqliteStore`
  implements `IAuditLog`, `IOperationStore` and `ISettingsStore`, and the two `Recent(int)` methods
  collide — cast to the wrong interface and you silently query the other table.
- `ChangeResult` and `GuardResult` carry a `Reason`/`Detail` string. Surface it in the UI; never
  collapse a refusal into `false`.
- Records for state (`AppearanceState`, `BackupSnapshot`, `AuditEntry`), classes for documents that
  need JSON property setters (`ThemeDocument`).
- `AvexJson.Options` (camelCase, indented) for all backup and theme serialisation. Backup property
  names are part of the on-disk format — changing them makes existing snapshots unreadable.
- Nullable enabled, implicit usings on. Names state the target: `windows.desktop.wallpaper`,
  `avex.controlcenter`.
- **No comments that restate code.** Where a constraint is invisible — the STA reason in
  `ChangePipeline`, the filename-ordering reason in §5, the `\\?\` handling in the policy — say why.
- Theme assets are `assets/themes/*.json`, embedded into `AVEXOS.ThemeEngine` as
  `AVEXOS.ThemeEngine.Themes.<name>.json` and **silently dropped if validation fails**. A theme that
  doesn't appear in the UI is a validator error, not a missing file — run it through
  `ThemeValidator` before assuming it loaded.

---

## 9. Known debt

Honest list, roughly in the order it hurts:

1. **No tests for anything that writes.** `WallpaperService`, `ChangePipeline`, `AppDiscoveryService`,
   `JsonBackupStore` and all UI are untested. The 178 passing tests cover path policy, registry policy,
   backup integrity and theme validation.
2. `NU1903` — `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (transitive via `Microsoft.Data.Sqlite` 10.0.0) has
   a high-severity advisory, [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q).
   Unresolved; 4 of the 6 distinct warnings (one per project that pulls in SQLite).
3. `CS0168` × 2 — unused `catch (Exception ex)` variables in `ActivityPage` and `HomePage`, where the
   exception is swallowed instead of surfaced.
4. Pipeline rollback is not verified (ARCHITECTURE §5) and `RiskLevel` is recorded but never acts as a
   gate.
5. Backups are unsigned and unhashed; `Load<T>` accepts whatever JSON is in the file.
6. Pages are code-behind, not the MVVM the Phase 1 draft promised.
7. No packaging, signing, installer, or arm64 build.
8. Discovery caps output at 80 apps and caches nothing; each Applications visit re-reads the registry.
9. **A theme's tokens are inert.** `ThemeService.Apply` writes `theme.id`/`theme.mode` to `Settings`
   and flips `RequestedTheme` — that is the entire mechanism, so colours, radii, spacing and font sizes
   in `assets/themes/*.json` never reach the visual tree, and the real palette lives in
   `Styles/AvexTokens.xaml`. Until something builds a `ResourceDictionary` from a `ThemeDocument`,
   "Generate Proposal" and the theme picker cannot show a palette difference, and the JSON and the XAML
   will drift apart silently — there is no test comparing them.
10. Two security tests are weaker than their names. `Denies_Alternate_Data_Stream` asserts the path is
    **allowed** after suffix stripping. Both junction tests open with
    `if (!TryCreateJunction(...)) return;`, so a missing `cmd.exe`, a `mklink` error or the 5-second
    `WaitForExit` timeout makes them **pass having asserted nothing** — a machine without those tools
    reads as green. Junctions need no privilege, so on this host they do run. Renaming or strengthening
    these is better than leaving the next reader to find out the hard way.
11. `RegistryPolicy` matches allowlist roots with `StartsWith`, so `…\Control Panel\DesktopEvil` and
    `…\AppEvents\SchemesX` are approved (measured 2026-11-08; THREAT_MODEL risk #9). Harmless while no
    sensitive HKCU location is a prefix-extension of a root; the fix is a segment-boundary comparison,
    not a shorter list. `HKCU\Software\Classes` itself left the list on 2026-11-08, pinned by a red-first
    test. Separate item in the same file: `App.Trace` writes `bootstrap.log` without going through the
    guard.
12. **No handle-based re-check between the guard and the write** (T-T6 shape 2). Canonicalisation now
    follows every component, so a link planted *before* the check is caught — that gap closed
    2026-11-08 with `Denies_Path_Below_Junction_In_Allowed_Root`, which was red first. What remains is
    the window *after* the check: `ImportWallpaper` copies with `overwrite: true` onto a predictable
    filename, so swapping that file for a link between the decision and the copy still redirects the
    write. Fixing it means `FileOptions.OpenReparsePoint` semantics or writing through a handle whose
    final path is re-read. Do not "fix" the canonicalisation half by adding another protected-root
    string; that bug was in resolution, not in the list.
