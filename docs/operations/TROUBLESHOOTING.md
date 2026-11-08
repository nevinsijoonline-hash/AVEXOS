# Troubleshooting

Every entry here is a failure that actually happened on this host and was diagnosed, not a hypothetical.
Grouped by where you notice it.

---

## 0. Three sources of truth

Before changing anything, get these. Guessing is what makes this codebase slow.

| Question | Ask the OS, not the app |
|---|---|
| Did the app really launch? | window **class** and title, not PID: `WinUIDesktopWin32WindowClass` / `AVEX OS Control Center` = real; an ordinary dialog whose title is `AVEX OS Control Center` = the apphost could not find .NET. |
| Why did it die? | `%LOCALAPPDATA%\AVEXOS\logs\bootstrap.log` — written by handlers installed before any XAML is parsed. |
| Did a change really take? | the value Windows holds, e.g. `(Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper).Wallpaper` |

Useful one-liners:

```powershell
Get-Process AVEXOS.App | Select-Object Id,StartTime,Path   # Path also tells you which output dir you ran
dotnet --list-runtimes | Select-String NETCore.App          # is the runtime there at all?
Get-ChildItem "$env:LOCALAPPDATA\AVEXOS\backups\bk-*.json" | Sort-Object Name -Descending | Select-Object -First 3
```

```bash
ls -la --time-style=+%H:%M:%S src/AVEXOS.App/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/AVEXOS.App.dll
```

`capture-window.ps1` combines the first two questions: it enumerates every window owned by the PID
with class, title and rect, then does a `PrintWindow(hwnd, hdc, 2)` capture and reports a
black/white/other pixel histogram so you can see that real content rendered — not just that a window
handle exists.

---

## 1. Launch problems

### The window never appears, exit code `0xC000027B`

Managed exceptions in XAML construction often surface as this native code, not a stack trace. The
cause in this project was a `XamlParseException` while building the page:

```
Failed to create a 'Microsoft.UI.Xaml.Controls.IconElement' from the text 'Color'. [Line: 13 Position: 75]
```

WinUI 3's `Symbol` enum is a reduced subset versus UWP, so `Icon="Color"` / `Icon="PaintBrush"`
shorthand fails. **Read `bootstrap.log` first** — it has the line and position. Fix by using
`<FontIcon Glyph="&#xE790;" />`. See DEVELOPER_GUIDE §6.

### A dialog says you must install .NET, but .NET is installed

The user-scoped SDK is not on `PATH`, so the apphost cannot locate the runtime.

```bat
set "DOTNET_ROOT=C:\Users\<you>\AppData\Local\Microsoft\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
```

`scripts/launch-app.cmd` does this. The trap: this failure mode produces a live process, a window
handle and a `MainWindowHandle`. It has been mistaken for a successful launch here. Confirm via
§0's window-class test.

### `dotnet build` / `dotnet test` → MSB1008 "Only one project can be specified"

Pass the solution, or one project at a time. `dotnet test AVEXOS.slnx` is not supported here; run the
two test projects separately.

### `./AVEXOS.App.exe` or `dotnet exec` → exit 127 in Git Bash

`dotnet` is not on `PATH` inside Git Bash. Export `DOTNET_ROOT` and prefix `PATH` (see
[INSTALLATION.md](../setup/INSTALLATION.md) §1), or call the `.cmd` script.

### `cmd` sets a variable then fails with "Access is denied"

`set VAR=value&& cmd` glues `value&&` into the value. Put a space before `&&`:
`set "VAR=value" && cmd`. Prefer a script file over chained inline commands.

### Packaged launch does nothing

Developer Mode is disabled on this host, so `WindowsPackageType`-based runs and `dotnet run` for the
packaged flavor are blocked. Unpackaged is the supported loop. Not a bug to chase.

---

## 2. Build says it succeeded but nothing changed

**This is the single most time-consuming trap in the repo, and it produces no error.**

`AVEXOS.App` sets `<Platforms>x64</Platforms>` and `AVEXOS.slnx` pins Platform `x64`. Two different
output trees exist:

| Command | Output directory |
|---|---|
| `dotnet build AVEXOS.slnx` | `src\AVEXOS.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\` ← what `launch-app.cmd` runs |
| `dotnet build src\AVEXOS.App\AVEXOS.App.csproj` | `src\AVEXOS.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64\` |

Building the csproj reports `Build succeeded.` / `0 Error(s)` and writes somewhere else. The app then
runs a binary from before your edit, so behaviour contradicts the source you are staring at. Observed
here: a fix plus a green build, and every runtime probe still showed the old behaviour — until the DLL
timestamp (`03:25:02`) was compared against the source timestamp (`03:42:12`).

**Habit:** always build the solution, and when a result seems impossible, compare timestamps before
believing anything you observe.

### `MSB3021` / `MSB3027` could not copy `AVEXOS.App.dll`

The app is running and holds a lock. `taskkill /IM AVEXOS.App.exe /F`, rebuild. This presents as ~10
build *errors* that are not compile errors at all — check whether the message is a copy failure before
reading code.

---

## 3. The app misbehaves on a click

### "An async operation was not properly started. Only a single ContentDialog can be open at any time."

Something blocked the UI thread waiting on a dialog — historically
`dialog.ShowAsync().GetAwaiter().GetResult()` inside the approval hook. The pipeline's `approve`
parameter is `Func<ChangeRequest, Task<bool>>` and must be awaited. Related: do not sprinkle
`ConfigureAwait(false)` in this path, because apply/verify/restore are apartment-sensitive and must
stay on the UI thread.

### Status says "Restored original wallpaper" but the wallpaper did not change

Two distinct causes, both real here:

1. **The stale-binary trap** (§2) — you are not running the fixed code.
2. **The original bug** — the page cached "original" state at load and refreshed it after each apply,
   so Restore re-applied the new value. Fixed by sourcing restore from the newest backup
   (DEVELOPER_GUIDE §5).

Either way the app's own message is not the oracle. Read the registry (§0). If the registry did not
change, the app lied; if it did change, your reading of the desktop was wrong.

### Applying a theme changes nothing, or always the same theme

`ThemesPage` originally had no `SelectionChanged` handler, so `_selectedTheme` stayed pinned to the
first item and "AVEX Light" applied "AVEX Dark". Fixed 2026-11-08 and verified with
`scripts/theme-probe.ps1`: selecting row 0 renders content-area luminance ≈ 14.3, row 1 ≈ 102.5.

If a theme is **missing from the list entirely**, that is not a load failure —
`BuiltInThemeCatalog` deserialises each embedded `assets/themes/*.json` and **silently drops any theme
that fails validation**. Run the file through `ThemeValidator` and read its `Errors`; a common cause is
declaring a target at level `"D"`, which is an error by design (a theme must not reference an
unsupported surface).

Remember the scope: theme apply sets `RequestedTheme` on the **AVEX window only**. It has never
restyled Windows, and no theme change is expected to.

---

## 4. Driving the UI from a script

### Clicks land on nothing, or the wrong control

`GetWindowRect` is **DPI-virtualised** (this desktop is at 125 %) while UIA `BoundingRectangle`
reports **physical** pixels. Mixing the two puts the cursor somewhere else. Observed: a button UIA
placed at physical (624,411) was computed at logical (130,130) from the window rect. Drive both the
window-relative maths and the click coordinates from UIA rects.

### Invoking a navigation item does nothing

`NavigationViewItem` supports **SelectionItemPattern, not InvokePattern**. `Invoke()` returns without
error and changes nothing. Either `Select()` it or click its centre.

### An element is "not found" but is visibly on screen

You are on the wrong page, or the name you searched is a `TextBlock` elsewhere in the tree. `Themes`
matches both the nav item and the Themes page heading, so `FindFirst` can hand you the heading.
`ListItem` names are also shared between the nav pane and content lists — the theme rows report
`AVEXOS.ThemeEngine.ThemeDocument`, not "AVEX Dark"; the visible label is a child `Text` element.
Enumerate all `ListItem`s and filter by `Name` rather than trusting one lookup.

### PowerShell harness prints nothing, or executes its own text

- Functions return everything they write, so diagnostics from a helper called inside `if (...)` are
  swallowed into the condition. Use `Write-Host` for harness chatter, `Write-Output` for return
  values. (`gui-cycle.ps1` pipes `Click-ByName ... | Out-Null`, which discards its own progress
  lines — a missing "clicked" line can mean suppressed, not failed.)
- Windows PowerShell 5.1 decodes `.ps1` files as **ANSI unless there is a UTF-8 BOM**. One em dash
  inside a string mid-script made the rest of the file execute as separate commands. Keep these
  scripts ASCII-only.
- Use `-STA` for UIA/WinUI interop:
  `powershell -NoProfile -ExecutionPolicy Bypass -STA -File scripts/gui-cycle.ps1`.
- `PrintWindow` needs flag `2` (`PW_RENDERFULLCONTENT`) for WinUI surfaces; without it you capture a
  blank or stale frame.

---

## 5. Changes refused

### "Blocked: Protected system location (C:\Windows)" or "Outside the AVEX OS data roots."

Working as designed. `ProtectedPathPolicy` is **deny-by-default**: a path is canonicalised — every
component resolved from the drive root, so a link above a directory is followed even when that directory
only exists *through* the link — `\\?\` and `\\.\` prefixes stripped, alternate-data-stream suffixes cut,
trailing dots and spaces trimmed, `..` folded — and then must land under
`%LOCALAPPDATA%\AVEXOS` or `%APPDATA%\AVEXOS`, and must not land under Windows, Program Files,
ProgramData\Microsoft, `C:\Recovery`, `System Volume Information`, `$Recycle.Bin`, `Boot`, `EFI` or any
volume root. The **parent directory** is checked separately, so a permitted filename inside a forbidden
folder is still refused.

Two things that change the *reason string* rather than the outcome, and one that changes the outcome:

- 8.3 short names are expanded where the volume can resolve them (`C:\PROGRA~1\…` →
  `C:\Program Files\…`, refused as protected). One it cannot resolve stays literal —
  `C:\Users\NEVIN~1\AppData\Local\AVEXOS\x.json` does not string-match the allowed root and is refused as
  "Outside the AVEX OS data roots." Spell AVEX paths in full.
- A traversal that misses a protected root (`…\AVEXOS\..\..\Windows\System32`) folds to
  `…\AppData\Windows\System32` and is refused by the allowlist, not the protected list.
- Since 2026-11-08 the guard no longer approves a path just because the leaf does not exist: a link
  inside an allowed root pointing at `C:\Windows` is refused **at the link and below it**. What the
  string policy still cannot see is a link swapped in *after* the decision — see
  [ARCHITECTURE.md §7](../architecture/ARCHITECTURE.md) and
  [THREAT_MODEL.md](../security/THREAT_MODEL.md) T-T6.
See [ARCHITECTURE.md §7](../architecture/ARCHITECTURE.md) for the exact limits.

To fix it, point the operation at an AVEX data path. Do not add an allowlist entry to make a
convenience work; `C:\Program Files` and `C:\ProgramData\Microsoft` are protected on purpose.

### "Machine-wide registry writes are not permitted."

Any `HKLM` / `HKLM`-alias key is rejected outright, no exceptions, because that is where elevation
would creep in from. Only the six user-scope roots in
`RegistryPolicy.AllowedWriteRoots` are writable
([THREAT_MODEL.md](../security/THREAT_MODEL.md) §4). A target that genuinely needs HKLM is not a
Level B target — mark it unsupported in the matrix.

### "Registry key is outside the allowed user-scope list."

The key is user-scope but not allowlisted. Either express the change through an allowed root or add
the target to the matrix as unsupported. Adding a root is a security review, not a bug fix.

### A path that "obviously" isn't in Windows gets blocked

Three causes, in the order they are actually found:

1. **It resolves into a protected root.** Check for a junction or symlink pointing there —
   `EnsureWritable` evaluates the resolved target when the final component exists. That is the whole
   point of the guard: a permitted-looking path that *resolves* into a protected root is refused.
2. **It resolves into the allowed root but does not spell it.** The comparison is textual after
   canonicalisation, so the **same file** can be refused if addressed the long way round:
   `C:\Users\NEVIN~1\AppData\Local\AVEXOS\x.json` does not string-match
   `%LOCALAPPDATA%\AVEXOS`, because 8.3 forms are never expanded. Short name here → denied as "Outside
   the AVEX OS data roots". Use the long path; this is a false refusal, not a protection.
3. **Its parent is outside the roots.** "Parent rejected: …" means the filename was fine and the folder
   above it was not.

Note that causes 2 and 3 both report `Outside the AVEX OS data roots`, so the reason string alone does
not tell you which one you hit — re-read the path, do not guess.

---

## 6. Data, files and desktop state

### Wallpaper goes blank or solid black after cleanup

Snapshots store a **path**, not bytes. If `payload.wallpaperPath` points into
`%LOCALAPPDATA%\AVEXOS\wallpapers\` and you deleted that directory, the desktop is configured to a
file that no longer exists. Restore the value to a real image
([BACKUP_AND_RECOVERY.md](../operations/BACKUP_AND_RECOVERY.md) §7) — this is why the uninstall note
says restore first.

### The wallpaper did not repaint after a restore

`SystemParametersInfo` is called with `SPIF_UPDATEINIFILE | SPIF_SENDCHANGE`, so the normal path does
broadcast. If it did not repaint, sign out and back in — that is deterministic. Explorer restart
usually suffices; do not chase it as an AVEX bug without checking the registry value first, because
the value and the paint are two different claims.

### Activity page is empty

`avex.db` is recreated on demand; deleting it loses history but not configuration. Note that both the
audit and operations tables come from the same `SqliteStore`; its two `Recent()` methods are explicit
interface implementations, so calling them through the wrong interface queries the wrong table.

### ~~`NU1903` warnings on every build~~ — resolved 2026-11-08

`SQLitePCLRaw.lib.e_sqlite3` advisory [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)
was resolved by upgrading `Microsoft.Data.Sqlite` from 10.0.0 to 10.0.14 (pulls `SQLitePCLRaw` 2.1.13).
Current build: 0 vulnerable-package warnings.

---

## 7. Still stuck

Collect before asking:

1. `bootstrap.log` (tail) and `Get-Process AVEXOS.App | Select Id,StartTime,Path`
2. the exact command you ran, plus `dotnet --version` and `dotnet --list-runtimes`
3. a full-solution build log: `dotnet build AVEXOS.slnx 2>&1 | Out-File build.log`
4. the DLL timestamp vs the source timestamp you changed (§2)
5. the target's real state from the OS, not the app's status line (§0)
6. what you expected, and what the app claimed happened

The two things most often wrong in a "the fix doesn't work" report here are a stale binary (§2) and
trusting the app's own status text (§3). Check both before anything else.
