# AVEX OS — Architecture

**Status:** Phase 1 deliverable · **Stack:** C# / .NET 10 / WinUI 3 (Windows App SDK 2.6.5) / SQLite / JSON

> **Read the markers.** This document describes both what runs today and what the architecture is
> aiming at. Sections are labelled **[built]** for behaviour verified against the code on this host,
> and **[planned]** for shapes that are design intent with no implementing code. Do not treat a
> **[planned]** entry as a feature. The changelog is
> [docs/contributing/CHANGELOG.md](../contributing/CHANGELOG.md).

---

## 1. Principles

1. **Transform the experience, preserve the operating system.** No write, rename, replace or delete anywhere under `C:\Windows`, and no indirect route to it.
2. **Deny by default.** Every filesystem and registry write passes a single guard that fails closed.
3. **Reversible by construction.** A change that cannot be backed up and restored is not offered.
4. **Truthful capability reporting.** The UI shows what is actually supported, with a reason. Nothing is marked applied until verification reads the value back.
5. **One design language.** All AVEX surfaces consume the same design tokens.
6. **Least privilege.** The app runs as the user. Elevation is a last resort, explicit, and never used to bypass a rule.

---

## 2. Process model

| Process | Role | Elevation |
|---|---|---|
| `AVEXOS.App.exe` | WinUI 3 Control Center; all UI | Standard user |
| Elevated helper (deferred) | Only if a future Level B target requires admin | Separate, per-operation, explicit consent |

**[built]** Phase 1–5 require **no elevation**. The app runs **unpackaged** (`WindowsPackageType=None`,
`WindowsAppSDKSelfContained=true`, `RuntimeIdentifier=win-x64`, `Platforms=x64`). There is no MSIX,
no installer and no elevated helper.

### Development toolchain (verified on this host)

```
DOTNET_ROOT = %LOCALAPPDATA%\Microsoft\dotnet
dotnet build AVEXOS.slnx -c Debug     # net10.0-windows10.0.26100.0, win-x64, WASDK 2.6.5
```

Packaged launch fails because Developer Mode is disabled; unpackaged launch is the only supported
dev loop. `scripts/launch-app.cmd` sets `DOTNET_ROOT` and starts the built exe from its output
directory, because the SDK is user-scoped and not on `PATH` — without `DOTNET_ROOT` the apphost
shows a missing-runtime dialog instead of a window. See
[INSTALLATION.md](../setup/INSTALLATION.md).

---

## 3. Layering

**[built]** Six projects exist; the dependency direction is strictly downward.

```
┌──────────────────────────────────────────────────────────┐
│ AVEXOS.App            WinUI 3 shell + pages (code-behind)  │
├──────────────────────────────────────────────────────────┤
│ AVEXOS.Core           contracts, models, ChangePipeline   │
├──────────────────────────────────────────────────────────┤
│ ThemeEngine   Backup   WindowsIntegration                  │
├──────────────────────────────────────────────────────────┤
│ AVEXOS.WindowsIntegration   ← the only layer that writes  │
├──────────────────────────────────────────────────────────┤
│ AVEXOS.Security         path policy + guard + SQLite sink │
├──────────────────────────────────────────────────────────┤
│ OS: Win32 / Registry / Filesystem                         │
└──────────────────────────────────────────────────────────┘
```

`AVEXOS.Discovery`, `Compatibility`, `IconManager`, `Monitoring`, `Diagnostics`, `AI` and `Plugins`
are **[planned]** — none of them exist. Today discovery lives in `WindowsIntegration`
(`AppDiscoveryService`, `CapabilityRegistry`) and the local proposal generator lives in `ThemeEngine`
(`LocalProposalService`).

**Hard rule:** only `WindowsIntegration` may call `Registry` write APIs, `SystemParametersInfo` or
`SHChangeNotify`, and each of those calls is checked by `Security` first
(`RegistryPolicy.IsWritable` in [`WallpaperService.Apply`](../../src/AVEXOS.WindowsIntegration/WallpaperService.cs#L29),
`IFileSystemGuard.EnsureWritable` before any file copy). `Microsoft.Win32` is referenced by
`AVEXOS.WindowsIntegration` and by nothing else.

One documented exception, and one that is not a host change: `App.Trace` in
[`App.xaml.cs`](../../src/AVEXOS.App/App.xaml.cs#L19) appends the crash log to
`%LOCALAPPDATA%\AVEXOS\logs\bootstrap.log` with a plain `File.AppendAllText`, bypassing the guard.
It is bootstrap-only — it must work when the guard itself has failed — and it writes inside the AVEX
data root. `AvexPaths.EnsureCreated` and `SqliteStore`'s constructor create their own directories the
same way.

The rule is enforced by review, not by tooling: an **automated architecture test is [planned]** and
does not exist. `AVEXOS.WindowsIntegration` reads the HKLM Uninstall tree directly for discovery,
which is a read, not a write.

---

## 4. Projects

**[built]** — these six exist in `AVEXOS.slnx` and compile:

| Project | Responsibility | Forbidden |
|---|---|---|
| `AVEXOS.App` | Shell, navigation, pages, design-token consumption | Any registry/FS host write |
| `AVEXOS.Core` | Interfaces, models, `ChangePipeline`, result types | Platform APIs |
| `AVEXOS.Security` | Path canonicalisation, protected-path policy, guard, `RegistryPolicy`, SQLite store | Performing changes |
| `AVEXOS.WindowsIntegration` | Win32/registry services (the write layer), app discovery, capability registry | Business decisions |
| `AVEXOS.ThemeEngine` | Theme schema, validation, built-in catalog, local proposal generator | Direct writes |
| `AVEXOS.Backup` | JSON snapshot store and listing | Writing outside AVEX data dirs |

**[planned]** — no project, no code, no placeholder:

| Project | Would own |
|---|---|
| `AVEXOS.Discovery` | Installed-app inventory from sources beyond the Uninstall tree |
| `AVEXOS.Compatibility` | Adapter registry, capability matrix, version gating |
| `AVEXOS.IconManager` | Icon library, folder/file-type/shortcut icons |
| `AVEXOS.Monitoring` | Update/version drift, broken-target detection |
| `AVEXOS.Diagnostics` | Logs, diagnostic bundles, health snapshots |
| `AVEXOS.AI` | Provider adapters, prompt→structured proposal |
| `AVEXOS.Plugins` | Sandboxed extension loading (post-1.0) |

---

## 5. The single change pipeline

**[built]** [`ChangePipeline.ExecuteAsync<TState>`](../../src/AVEXOS.Core/ChangePipeline.cs) is the only
mutating entry point the UI uses. It takes the target's own read/apply/verify/restore delegates, so a
capability never mutates the host except through it:

```csharp
public Task<ChangeResult> ExecuteAsync<TState>(
    ChangeRequest request,
    Func<TState> capture,                  // read current value
    Action<TState> apply,                  // perform the change
    Func<bool> verify,                     // read back and compare
    Action<TState> restore,                // undo, used only on failure
    Func<ChangeRequest, Task<bool>> approve,  // UI consent, awaited on the UI thread
    Func<bool>? verifyRollback = null,     // optional: verify restore succeeded
    Func<ChangeRequest, Task<bool>>? confirmHighRisk = null)  // required for High-risk changes
```

Stage order as it actually runs, against the 14-stage plan:

```
1  target identity       ← ChangeRequest.TargetId, supplied by the caller
2  read current value    ← capture()
3  capability check      ✗ not in the pipeline; CapabilityRegistry is consulted at discovery only
4  guard                 ← EnsureWritable(request.PathToWrite), when the change writes a file
5  classify risk         ← ChangeRequest.Risk, recorded and now used to gate High-risk changes
6  create backup         ← _backups.Create(targetId, original, description) — aborts the change on failure
7  operation record      ← saved as Pending
8  approval              ← await approve(request); decline ⇒ Denied, nothing applied
8b high-risk confirm    ← if Risk==High, await confirmHighRisk(request); missing or declined ⇒ Denied
9  apply                 ← apply(original), i.e. the new value
10 read back and verify  ← verify(); false ⇒ rollback
11 mark state            ← Verified / RolledBack / Denied / Failed
12 audit entry           ← one row per outcome
13 roll back on failure  ← restore(original), then verifyRollback() if provided; failure ⇒ Failed state
14 diagnostic bundle     ✗ not implemented; bootstrap.log only
```

No honest gaps remain in the stage order.

Risk gating was added on 2026-11-08: stage 8b requires a `confirmHighRisk` delegate for High-risk
changes. If the delegate is not provided or returns false, the operation is denied. Low and Medium
risk changes proceed with only the standard `approve` delegate.

Rollback verification was added on 2026-11-08: stage 13 now calls `verifyRollback()` after `restore()`
when the delegate is provided. If verification fails, the operation is marked `Failed` instead of
`RolledBack`. The `AppearancePage` passes a `verifyRollback` delegate that checks the wallpaper path
matches the original state.

`ChangeResult` carries `Pending | Denied | Applied | Verified | RolledBack | Failed` — never a bare
`bool`. `Succeeded` is true only for `Applied`/`Verified`. The message text is direction-neutral: a
successful restore reads "Verified. Backup …" — the same wording as an apply, so the UI does not claim
a direction the operation did not take.

`approve` is awaited **without** `ConfigureAwait(false)`: `apply`, `verify` and `restore` are
apartment-sensitive (`SystemParametersInfo`, registry writes) and must stay on the UI thread.

---

## 6. Contracts

**[built]** exactly as declared in
[`Contracts.cs`](../../src/AVEXOS.Core/Contracts.cs) — synchronous where the work is synchronous, and
every mutating service is driven by `ChangePipeline`, not called raw:

```csharp
public interface IPathPolicy      { string Canonicalize(string path); bool IsAllowed(string canonical, out string reason); }
public interface IFileSystemGuard { GuardResult EnsureWritable(string path); GuardResult EnsureDirectory(string path); }
public interface IWallpaperService{ AppearanceState Read(); void Apply(AppearanceState state); string ImportWallpaper(string sourcePath); }
public interface IAppDiscoveryService { Task<IReadOnlyList<DiscoveredApplication>> DiscoverAsync(CancellationToken ct); }
public interface IBackupStore     { BackupSnapshot Create<T>(string targetId, T payload, string description); T? Load<T>(string id); IReadOnlyList<BackupSnapshot> List(int count = 50); }
public interface IAuditLog        { void Append(AuditEntry entry); IReadOnlyList<AuditEntry> Recent(int count = 100); }
public interface IOperationStore  { void Save(OperationRecord record); IReadOnlyList<OperationRecord> Recent(int count = 100); int CountByState(ChangeState state); }
public interface ISettingsStore   { string? Get(string key); void Set(string key, string value); }
public interface IClock           { DateTimeOffset UtcNow { get; } }
public interface IThemeValidator  { ValidationResult Validate(ThemeDocument theme); }
```

`SqliteStore` implements `IAuditLog`, `IOperationStore` and `ISettingsStore`; the two `Recent` methods
collide, so they are **explicit interface implementations** — casting to the wrong interface silently
queries the wrong table. `IAuditLog.Recent` and `IOperationStore.Recent` must be called through their
declared interface type.

**[planned]** — referenced by nothing, declared nowhere: `IChangeTarget`, `IAppearanceAdapter`,
`IAiProvider`, `CapabilityReport`, `Detection`, `Verification`, `ThemeApplication`, `AuditFilter`. The
async signatures sketched here in the Phase 1 draft (`IBackupStore.CreateAsync`,
`IAuditLog.AppendAsync`) were not built; the shipped ones are above.

### Adapter capability levels

`CapabilityLevel` (A/B/C/D) exists and is reported. The "UI treatment" column is the design intent;
what ships is an **inventory**: `ApplicationsPage` binds each row's display name, publisher and
`CapabilityReason` — **the level letter itself is never rendered** — and offers no apply button at all,
because no adapter exists. `HomePage` derives one aggregate from it
(`apps.Count(a => a.Capability != CapabilityLevel.D)` as "supported").

| Level | Meaning | UI treatment (planned) |
|---|---|---|
| A | Native theme API | Full theming |
| B | Documented Windows API | Full, with backup |
| C | Supported external config | Apply with warning |
| D | Unsupported / unverified | Listed, disabled, reason shown |

`CapabilityRegistry.Known` currently matches three display-name substrings — `windows terminal`,
`visual studio code`, `avex` — and returns Level D with "No documented theming API. Follows system
light/dark only." for everything else. The A/B/C grades for Terminal and VS Code are a claim about
*their* settings files; **AVEX OS does not write them**. Nothing reads those settings today.

---

## 7. Protected-path policy

Deny-by-default, checked on the **canonical** path after resolving reparse points.

```
Protected roots (deny writes, renames, deletes, creates):
  C:\Windows                      (all descendants, incl. System32, SysWOW64, WinSxS)
  C:\Program Files                (best effort)
  C:\Program Files (x86)
  C:\ProgramData\Microsoft
  C:\$Recycle.Bin, C:\System Volume Information, C:\Recovery
  C:\Boot, C:\EFI
  Any volume root
  Any path whose canonical form resolves into a protected root
```

Canonicalisation **[built]**, from
[`ProtectedPathPolicy.cs`](../../src/AVEXOS.Security/ProtectedPathPolicy.cs): trims quotes and
whitespace, strips `\\?\`, `\\.\` and `\\?\UNC\` prefixes, drops `:stream` ADS suffixes, folds the path
with `Path.GetFullPath` (which handles `..` and drive-relative forms), then walks **every** component
from the drive root, resolving each one against the already-resolved prefix, and finally trims trailing
dots and spaces per segment. Comparison is ordinal-ignore-case, so `C:\wInDoWs` is caught.

Two properties, both measured rather than assumed — a throwaway console project referencing
`AVEXOS.Security`, run on .NET 10.0.14 on this host, and re-run on 2026-11-08 after the change below to
confirm the verdicts had not moved:

- **8.3 short names are expanded only when the filesystem can resolve them.**
  `C:\PROGRA~1\Common~1\x.dll` canonicalises to `C:\Program Files\Common Files\x.dll` and is denied with
  `Protected system location (C:\Program Files).` — so the common case *is* caught by the protected-root
  check. But `C:\WINDOWS\SYSTEM~1\kernel32.dll` stops at `C:\Windows\SYSTEM~1\kernel32.dll`: the first
  component is matched and re-cased to what is on disk, the second is left literal because that short
  name does not resolve here (8.3 generation is not available for it). The path is still denied — its
  resolved prefix is protected — so nothing escapes.
  The direction this actually bites is **over-refusal**: `C:\Users\NEVIN~1\AppData\Local\AVEXOS\x.json`
  (a short name that does not exist on this volume) is never expanded, so it does not string-match the
  allowed root and is refused as `Outside the AVEX OS data roots.` — the *same file* the app would
  happily open by its long path. Short names are therefore a false-refusal hazard, and only become a
  security question if the allowed roots are ever widened to a directory reachable by an unexpanded
  short name.
  Note `Environment.GetFolderPath(Windows)` returns `C:\WINDOWS` in uppercase on this host, which is why
  denial reasons print `Protected system location (C:\WINDOWS).`
- **Reparse resolution walks every component — it used to resolve only one, and that was a real gap.**
  The old walk started at the leaf, and when the leaf did not exist it climbed to the *first ancestor
  that did* and resolved only that one. A link sitting above an existing directory was therefore never
  examined. Measured on this host with a junction at `%LOCALAPPDATA%\AVEXOS\jw-probe → C:\Windows`
  (removed afterwards with `rmdir`; nothing was ever written through it — only the decision function was
  called), **before the fix**:

  | Input | Canonical | Guard verdict |
  |---|---|---|
  | `…\AVEXOS\jw-probe` | `C:\Windows` | **DENY** `Protected system location (C:\WINDOWS).` |
  | `…\AVEXOS\jw-probe\System32` (exists via the link) | literal, unchanged | **DENY** `Parent rejected: Protected system location (C:\WINDOWS).` |
  | `…\AVEXOS\jw-probe\System32\x.dll` (leaf does not exist) | literal, unchanged | **ALLOW** `Allowed` |
  | `C:\Users\Public\avexjtest\j\System32\kernel32.dll` (outside the roots) | literal, unchanged | DENY, but `Outside the AVEX OS data roots.` — the protected check never fired |

  The third row was the hole: a link inside the allowed root plus a **non-existent** filename below it
  sailed through the string policy, and `File.Copy` would have followed the link. The parent check saved
  the two-component case by accident, and the only thing preventing damage was the OS access token —
  precisely what a defence-in-depth guard must not lean on.

  `ResolveReparsePoints` now builds the path component by component from the drive root, resolving each
  one against the **already-resolved** prefix, so the link above `System32` is followed and the leaf
  below it is judged on the resolved target. What is proven, by
  `Denies_Path_Below_Junction_In_Allowed_Root` (a real junction into `C:\Windows` inside a temporary
  allowed root, **watched failing against the shipped code before the fix**):

  - `…\j\System32\x.dll` canonicalises to `C:\Windows\System32\x.dll` and both `IsAllowed` and
    `FileSystemGuard.EnsureWritable` refuse it.
  - `…\j\System32` — the directory that exists *through* the link — is refused by `EnsureWritable` too.

  One consequence measured after the change and accepted deliberately: a link that sits **outside** an
  allowed root and points **into** it now resolves inside that root, so the decision flips from DENY to
  ALLOW. Bytes still land inside the declared roots, which is what the policy is for; the app has no
  code path that writes to a caller-supplied directory, so the case is unreachable today. The opposite
  direction tightened: a link inside the root pointing out is now refused with
  `Outside the AVEX OS data roots.` where the old one-deep walk approved it. Still open: mount points
  are not handled, symlink chains are untested here (creating one needs Developer Mode), and the guard
  still does no handle-based re-check after deciding — see
  [THREAT_MODEL.md](../security/THREAT_MODEL.md) §5 risks 1 and 4.

The five literal roots (`C:\Recovery`, `C:\System Volume Information`, `C:\$Recycle.Bin`, `C:\Boot`,
`C:\EFI`) are hardcoded; Windows, Program Files, Program Files (x86) and `ProgramData\Microsoft` come
from `Environment.GetFolderPath`, so they follow a relocated Windows install.

**Write targets that are allowed today: `%LOCALAPPDATA%\AVEXOS` and `%APPDATA%\AVEXOS` — nothing
else.** The constructor accepts extra roots, but `App.xaml.cs:46` registers
`new ProtectedPathPolicy()` with no arguments. Explicitly opted-in user folders (`Desktop`,
`Documents/{chosen}`) for icon operations are **[planned]** and would need a change at that call site.

`FileSystemGuard.EnsureWritable` runs the same policy on the resolved **parent** directory and denies
with "Parent rejected: …" if it escapes **[built]**. `RegistryPolicy.IsWritable` rejects every HKLM
write outright and allowlists six HKCU roots **[built]**. `HKCU\Software\Classes` — the file-association
hive — was on that list with no consumer until 2026-11-08, when spec rule 22 was enforced by deleting
the entry; `RegistryPolicyTests` now pins the hive as refused and the remaining roots as allowed. The
list is matched with `StartsWith`, so a sibling key whose name begins with a root is also approved —
measured and tracked as risk #9 in [THREAT_MODEL.md](../security/THREAT_MODEL.md).

---

## 8. Data model (SQLite)

**[built]** — `%LOCALAPPDATA%\AVEXOS\avex.db`, created by
[`SqliteStore.cs`](../../src/AVEXOS.Security/SqliteStore.cs) with `CREATE TABLE IF NOT EXISTS`. Three
tables, not the seven the Phase 1 draft listed:

| Table | Columns | Notes |
|---|---|---|
| `AuditEvents` | Utc, Category, TargetId, OperationId, Outcome, Detail | **no primary key**; `Id` does not exist |
| `Operations` | Id (PK), TargetId, Kind, Risk, State, UpdatedUtc, Detail | `INSERT OR REPLACE` on `Id` |
| `Settings` | Key (PK), Value | Values are plain strings; theme state is `theme.id` + `theme.mode` |

Timestamps are stored as `DateTimeOffset.ToString("o")` text and read back with
`DateTimeOffset.Parse`, so `ORDER BY Utc DESC` is a lexical sort on a round-trippable format. There is
no migration story and no schema version — changing a table means deleting `avex.db`.

`Applications`, `Adapters`, `Backups` and `Themes` were in the draft and **do not exist**. Discovery
results are never persisted, and backups live on disk as JSON (see §2 and §5 of
[BACKUP_AND_RECOVERY.md](../operations/BACKUP_AND_RECOVERY.md)), which means *the backup index is the
filename sort order* — `bk-yyyyMMdd-HHmmss-{guid8}.json` — not a database table.

**[planned]** tables, listed nowhere in code: the four above, plus any audit filtering.

---

## 9. Theme model

**[built]** — deserialized by `System.Text.Json` into
[`ThemeDocument.cs`](../../src/AVEXOS.ThemeEngine/ThemeDocument.cs) with
[`AvexJson.Options`](../../src/AVEXOS.Core/AvexJson.cs) (camelCase, case-insensitive read, comments and
trailing commas allowed, indented write). The token groups are **flat top-level properties**, not
nested under a `tokens` object — a file written with `"tokens": { … }` deserialises with empty
dictionaries and fails validation on the required colours:

```json
{
  "id": "avex.dark",
  "name": "AVEX Dark",
  "version": "1.0.0",
  "mode": "dark",
  "category": "builtin",
  "author": "AVEX OS",
  "description": "Deep charcoal surfaces with a restrained graphite palette and a single cool accent.",
  "fontFamily": "Segoe UI Variable",
  "color": { "bg.base": "#141517", "surface.1": "#1E2125", "text.primary": "#ECEDEF", "accent": "#6E8BFF" },
  "radius": { "sm": 2, "md": 3, "lg": 5 },
  "space":  { "xs": 4, "sm": 8, "md": 12, "lg": 16, "xl": 24 },
  "motion": { "fast": 130, "standard": 165 },
  "type":   { "h1": 20, "body": 13, "caption": 11 },
  "targets": {
    "avex.controlcenter": { "level": "A" },
    "windows.desktop.wallpaper": { "level": "B" },
    "windows.system.theme-mode": { "level": "C" }
  }
}
```

That is a trimmed excerpt of [`avex-dark.json`](../../assets/themes/avex-dark.json), which is embedded
into `AVEXOS.ThemeEngine` as a manifest resource named `AVEXOS.ThemeEngine.Themes.avex-dark.json` —
the file on disk is not read at run time, so editing it needs a rebuild.

[`ThemeValidator.cs`](../../src/AVEXOS.ThemeEngine/ThemeValidator.cs) checks, and only this: id matches
`^[a-z][a-z0-9]*(\.[a-z0-9]+)+$`; name present; semver; `mode` is `dark` or `light`; `fontFamily`
present (its **value is never resolved against installed fonts**); the seven required colour keys
(`bg.base`, `bg.raised`, `surface.1`, `text.primary`, `text.secondary`, `accent`, `border.subtle`)
present and every colour `#RRGGBB`/`#RRGGBBAA`; radius 0..64, space 0..256, motion 0..2000 ms; each
target level in A–D, with **Level D a hard error**; and a warning if the theme declares no targets.

There is **no** JSON-schema validation, no icon-existence check, and no payload size cap — those were
draft claims. No user-import path exists either: `BuiltInThemeCatalog` deserialises the embedded
resources and `continue`s past any theme that fails validation, so an invalid theme is silently absent
from the picker with no reason shown (recorded in
[USER_GUIDE.md](../setup/USER_GUIDE.md) and [TROUBLESHOOTING.md](../operations/TROUBLESHOOTING.md)).

**How WinUI actually consumes a theme — read this before promising anything.** Applying a theme calls
[`ThemeService.Apply`](../../src/AVEXOS.App/ThemeService.cs), which persists `theme.id` and
`theme.mode` to the `Settings` table and sets `RequestedTheme` on the root element to `Light` or
`Dark`. That is the whole mechanism. The colours, radii and sizes you see come from the static
`ThemeDictionaries` in [`AvexTokens.xaml`](../../src/AVEXOS.App/Styles/AvexTokens.xaml); **no code
turns `color`/`radius`/`space`/`type` values into brushes or setters.** Two themes that both say
`"mode": "dark"` render identically no matter how different their tokens are, and editing a token in
`avex-dark.json` changes nothing on screen. Runtime `ResourceDictionary` generation from tokens is
**[planned]**.

---

## 10. Threading and performance

**[built]**, and the draft's claims were too generous:

- `ChangePipeline.ExecuteAsync` is `async` **only** because of the `approve` delegate. `capture`,
  `apply`, `verify` and `restore` are synchronous `Func`/`Action`, so they run to completion on the
  calling thread — the UI thread, from a page button handler. `SystemParametersInfo` and the registry
  writes therefore block the UI today; the interface shape leaves room for off-thread work, the code
  does not use it.
- `AppDiscoveryService.DiscoverAsync` returns a `Task` but contains **no `await`**: it enumerates
  HKLM 64-bit, HKLM 32-bit and HKCU uninstall keys synchronously on the UI thread. On this host that
  is a short, noticeable stall (~78 results after the `.Take(80)` cap).
- **There is no cache.** `ApplicationsPage` re-runs discovery on every `Loaded` (i.e. every navigation
  into the page) and on Refresh. The draft's "cached in SQLite, 24 h staleness check" does not exist,
  and there is no `Applications` table to cache into.
- Shell icon extraction, icon decoding and lazy thumbnail caching: **[planned]** — nothing in the tree
  reads an icon.
- No background service, no polling, no autorun entry **[built]** (nothing writes Run/Startup folders).
- Startup time, idle CPU and idle memory have **never been measured**. The draft's "< 1.5 s to first
  frame, ~0 % idle CPU, < 180 MB" are targets with no recorded result, not results.

---

## 11. Testing

**[built]** — six xUnit projects, 262 tests total (44 + 28 + 15 + 94 + 29 + 52), green on 2026-11-08 with
`dotnet test AVEXOS.slnx`:

| Suite | File | Covers |
|---|---|---|
| `AVEXOS.Security.Tests` | [ProtectedPathPolicyTests.cs](../../tests/AVEXOS.Security.Tests/ProtectedPathPolicyTests.cs) | 11 members, 15 cases: Windows paths ×5 `InlineData`, case variants, trailing dot, `\\?\` prefix, traversal out of the allowed root, allow inside the AVEX root, volume root, UNC, ADS suffix, the junction test, and `Denies_Path_Below_Junction_In_Allowed_Root` (§7's gap) |
| `AVEXOS.Security.Tests` | [RegistryPolicyTests.cs](../../tests/AVEXOS.Security.Tests/RegistryPolicyTests.cs) | 6 members, 14 cases: HKLM rejected (both spellings), `Control Panel\Desktop` allowed, three non-allowlisted user keys refused with the exact reason string, `Denies_The_File_Association_Hive` ×3 for `HKCU\Software\Classes` (§4), `Denies_Prefix_Shadowing_Attempts` ×3, and `Allows_Legitimate_Subkeys` ×2 |
| `AVEXOS.ThemeEngine.Tests` | [ThemeValidatorTests.cs](../../tests/AVEXOS.ThemeEngine.Tests/ThemeValidatorTests.cs) | id/name/version/mode/font rules, required and malformed colours, numeric ranges, Level D rejection |
| `AVEXOS.ThemeEngine.Tests` | [ThemeResourceBuilderTests.cs](../../tests/AVEXOS.ThemeEngine.Tests/ThemeResourceBuilderTests.cs) | 10 cases: color entries with Avex prefix, brush references, PascalCase conversion, radius/space/motion/type scalars, font family, color/brush count parity, brush-to-color referential integrity |
| `AVEXOS.Backup.Tests` | [JsonBackupStoreTests.cs](../../tests/AVEXOS.Backup.Tests/JsonBackupStoreTests.cs) | 3 cases: tampered backup rejected, unhashed backup rejected, valid hashed backup accepted |
| `AVEXOS.Core.Tests` | [ChangePipelineTests.cs](../../tests/AVEXOS.Core.Tests/ChangePipelineTests.cs) | 3 cases: rollback with verifyRollback=false reports Failed, rollback with verifyRollback=true reports RolledBack, rollback without verifyRollback reports RolledBack |
| `AVEXOS.Core.Tests` | [ChangePipelineRiskTests.cs](../../tests/AVEXOS.Core.Tests/ChangePipelineRiskTests.cs) | 3 cases: High-risk without confirmHighRisk denied, High-risk with confirmHighRisk=true succeeds, Low-risk without confirmHighRisk succeeds |
| `AVEXOS.WindowsIntegration.Tests` | [WallpaperServiceTests.cs](../../tests/AVEXOS.WindowsIntegration.Tests/WallpaperServiceTests.cs) | 4 cases: ImportWallpaper copies file, throws on missing source, throws on guard deny, overwrites existing |
| `AVEXOS.WindowsIntegration.Tests` | [MultiMonitorWallpaperTests.cs](../../tests/AVEXOS.WindowsIntegration.Tests/WallpaperServiceTests.cs) | 6 cases: monitor enumeration, unknown device handling, per-monitor settings persistence, apply-to-all |
| `AVEXOS.WindowsIntegration.Tests` | [MonitorSlideshowServiceTests.cs](../../tests/AVEXOS.WindowsIntegration.Tests/WallpaperServiceTests.cs) | 10 cases: defaults, persistence, enable/disable, advance, reset, independent per-monitor state |
| `AVEXOS.WindowsIntegration.Tests` | [MonitorDetectionTests.cs](../../tests/AVEXOS.WindowsIntegration.Tests/WallpaperServiceTests.cs) | 4 cases: non-null list, primary detection, fallback behavior |
| `AVEXOS.App.Tests` | [ThemeSchedulerServiceTests.cs](../../tests/AVEXOS.App.Tests/ThemeSchedulerServiceTests.cs) | 8 cases: add/remove schedules, enable/disable persistence, JSON round-trip, disabled scheduler skips checks, time-match triggers apply, deduplication, first-match-wins |
| `AVEXOS.App.Tests` | [ShortcutParserTests.cs](../../tests/AVEXOS.App.Tests/ShortcutParserTests.cs) | 44 cases: parsing valid/invalid shortcuts, format round-tripping, IsValid, IsSystemReserved, ConflictsWith, defaults completeness, special keys, case insensitivity |
| `AVEXOS.ThemeEngine.Tests` | [UserThemeServiceTests.cs](../../tests/AVEXOS.ThemeEngine.Tests/UserThemeServiceTests.cs) | 9 cases: export creates valid JSON, import saves to user dir, import rejects invalid themes/missing files/bad JSON, delete removes files, LoadAll returns imported themes, LoadAll returns empty when no dir, export-then-import roundtrip |
| `AVEXOS.Backup.Tests` | [SettingsBackupServiceTests.cs](../../tests/AVEXOS.Backup.Tests/SettingsBackupServiceTests.cs) | 11 cases: file creation/naming, JSON format, read/restore round-trip, tamper detection, missing file handling, listing order, deletion, retention cap at 10, empty settings, duplicate timestamps |
| `AVEXOS.Core.Tests` | [SystemInfoServiceTests.cs](../../tests/AVEXOS.Core.Tests/SystemInfoServiceTests.cs) | 16 cases: Windows version, .NET version, app version, theme ID/mode defaults and stored values, session duration, machine/user name, processor count, memory, wallpaper path defaults and slideshow paths |
| `AVEXOS.WindowsIntegration.Tests` | [AppDiscoveryServiceTests.cs](../../tests/AVEXOS.WindowsIntegration.Tests/AppDiscoveryServiceTests.cs) | 5 cases: returns results, required fields present, limited to 80, sorted by DisplayName, respects cancellation |
| `AVEXOS.Core.Tests` | [WorkspaceProfileServiceTests.cs](../../tests/AVEXOS.Core.Tests/WorkspaceProfileServiceTests.cs) | 22 cases: CRUD operations, apply wallpaper/theme, set-default, validation, JSON persistence round-trip |
| `AVEXOS.WindowsIntegration.Tests` | [FocusModeServiceTests.cs](../../tests/AVEXOS.WindowsIntegration.Tests/FocusModeServiceTests.cs) | 19 cases: config persistence, enable/disable lifecycle, wallpaper save/restore, idempotency, single-monitor edge case, DimNonPrimary flag, validation clamping |
| `AVEXOS.WindowsIntegration.Tests` | [PresentationModeServiceTests.cs](../../tests/AVEXOS.WindowsIntegration.Tests/PresentationModeServiceTests.cs) | 19 methods (26 cases): config defaults/persistence, enable/disable, blank secondary/all monitors, BMP generation, hex color parsing |
| `AVEXOS.WindowsIntegration.Tests` | [AutoBrightnessServiceTests.cs](../../tests/AVEXOS.WindowsIntegration.Tests/AutoBrightnessServiceTests.cs) | 17 cases: config persistence, enable/disable, brightness calculation (day/night/transition), clamping, tick behavior, disposal |

No integration, compatibility, UI or performance suite exists. `SqliteStore` and `ThemeService` (WinUI wiring) have **zero automated coverage** — the wallpaper read/backup/apply/verify/restore cycle is verified by hand with [`gui-cycle.ps1`](../../scripts/gui-cycle.ps1), and the theme picker by [`theme-probe.ps1`](../../scripts/theme-probe.ps1). `ThemeResourceBuilder`, `UserThemeService`, `ThemeSchedulerService`, `ShortcutParser`, `SettingsBackupService`, `SystemInfoService`, `MonitorDetection`, `MonitorSlideshowService`, multi-monitor `WallpaperService`, `WorkspaceProfileService`, `FocusModeService`, `PresentationModeService`, and `AutoBrightnessService` are covered by 221 unit tests.

Two weaknesses in the security suite, so nobody reads a green row as more than it is:

- `Denies_Alternate_Data_Stream` **asserts `True`**. The ADS suffix is stripped, the remaining path is
  inside the allowed root, so the test proves the stripping works — not that streams are denied. Its
  name describes the opposite of what it checks.
- Both junction tests — `Denies_Junction_Pointing_To_Windows` and
  `Denies_Path_Below_Junction_In_Allowed_Root` — create their link with `cmd /c mklink /J` and open with
  `if (!TryCreateJunction(...)) return;`. A missing `cmd.exe`, a `mklink` error, or the 5-second
  `WaitForExit` timeout therefore make them **pass having asserted nothing**. Junctions need no
  privilege, so on this host they do run — that is how §7's gap was proved red and then green — but a
  green row elsewhere does not by itself mean the link case was checked.
  `Denies_Traversal_From_Allowed_Root` is weaker than it looks too: folding
  `%LOCALAPPDATA%\AVEXOS\..\..\Windows\System32` lands in `…\AppData\Windows\System32`, which is not
  under `C:\Windows` at all, so it is denied as *outside the allowed roots* rather than for reaching a
  protected location. The allowlist is what makes that test green.

"The security suite is a release gate" is the intent, not a mechanism: nothing in the tree runs tests
automatically — there is no CI configuration and no git hook. A person must run `dotnet test` and
choose not to ship on red.

---

## 12. Repository layout

This tree **is not a git repository** (see [CONTRIBUTING.md](../contributing/CONTRIBUTING.md)); "root"
means the workspace folder `9f0855d1`. Everything stays outside `C:\Windows`.

```
AVEXOS.slnx     8 projects: src ×6, tests ×2
src/            AVEXOS.{Core, Security, ThemeEngine, Backup, WindowsIntegration, App}
tests/          AVEXOS.{Security.Tests, ThemeEngine.Tests}
assets/         themes/, wallpapers/   (themes are embedded at build; wallpapers are copied)
docs/           architecture/ security/ compatibility/ setup/ operations/ contributing/
scripts/        launch-app.cmd + capture-window.ps1, gui-cycle.ps1, uia-dump.ps1,
                theme-probe.ps1
```

**[planned]** and never created: `installer/`, `build/`, and the seven projects the draft listed —
`AVEXOS.{Discovery, Compatibility, IconManager, Monitoring, Diagnostics, AI, Plugins}`. Their jobs are
today split across the six that exist: discovery and Windows integration live in
`AVEXOS.WindowsIntegration`, backups in `AVEXOS.Backup`, diagnostics are `App.Trace` writing
`bootstrap.log`. `assets/` holds exactly three files — both theme JSONs and
`wallpapers/avex-wallpaper.png`; `branding/` and `icons/` are empty directories and `sounds/` was
never created.
