# AVEX OS Threat Model

Scope: the AVEX OS Control Center as it runs today on Windows 11 build 26300, x64, unpackaged,
as the logged-in user. This document describes real assets and real boundaries, not an aspirational
deployment.

## 1. Assets

| Asset | Where | Why it matters |
|---|---|---|
| User desktop appearance settings | `HKCU\Control Panel\Desktop` | Changing them unexpectedly is a visible, annoying regression |
| Original-value backups | `%LOCALAPPDATA%\AVEXOS\backups\*.json` | The only way to undo a change; losing them makes changes irreversible |
| Audit log and operation records | `%LOCALAPPDATA%\AVEXOS\avex.db` | Evidence of what AVEX OS touched |
| Imported wallpaper assets | `%LOCALAPPDATA%\AVEXOS\wallpapers\` | Files copied from user-chosen locations |
| The user's own files | anywhere on disk | Must never be modified or enumerated without reason |
| The OS itself | `C:\Windows`, `HKLM`, protected processes | Must never be touched — this is the whole safety premise |

## 2. Trust boundaries

1. **User → Control Center UI.** The user types prompts and clicks Apply/Restore.
2. **UI → ChangePipeline.** The pipeline is the only mutating entry point; nothing else may write.
3. **Pipeline → FileSystemGuard / RegistryPolicy.** The last checkpoint before any host write.
4. **WindowsIntegration → OS APIs.** `SystemParametersInfo`, registry, filesystem.
5. **Proposal generator → ThemeValidator.** Untrusted generated JSON becomes a validated document.

Anything crossing boundary 3 or 4 is the high-consequence path. Boundaries 1–2 are where a malicious
or buggy theme/extension would try to smuggle a target.

## 3. Threats

### Spoofing
- **T-S1 — Impersonating AVEX OS.** An unrelated app writes to `%LOCALAPPDATA%\AVEXOS` and is trusted
  by the user. *Mitigation:* none implemented; there is no signing or integrity check yet.
- **T-S2 — Elevation of privilege via the app's own token.** The app runs as the user, so it can already
  do everything the user can. *Mitigation:* self-restraint enforced at boundary 3, not by OS ACLs.

### Tampering
- **T-T1 — Path traversal to a protected root.** `..\..\..\Windows\...` in a request path.
  *Mitigation:* `Path.GetFullPath` folds the traversal **before** the decision, measured:
  `%LOCALAPPDATA%\AVEXOS\..\..\Windows\System32\x.dll` → `C:\Users\Nevin Sijo\AppData\Windows\System32\x.dll`.
  Note that this does not land in `C:\Windows` at all, so `Denies_Traversal_From_Allowed_Root` is green
  because the folded path is **outside the allowed roots**, not because a protected root was detected.
  Either way the write is refused — the allowlist is the load-bearing half of this defence.
- **T-T2 — Junction or symlink redirect.** A link inside an allowed directory pointing at `C:\Windows`.
  *Mitigation:* **enforced for both shapes since 2026-11-08, proved by a test that was red first.** Every
  component is resolved from the drive root down, so the link is followed even when the directory below
  it exists *through* the link: `…\AVEXOS\jw-probe` canonicalises to `C:\Windows` and is refused, and
  `…\AVEXOS\jw-probe\System32\x.dll` — which the previous one-deep walk **approved** under both
  `IsAllowed` and `EnsureWritable` — is now refused too. The regression test is
  `Denies_Path_Below_Junction_In_Allowed_Root`, which builds a real junction into `C:\Windows` inside a
  **temporary** allowed root — passed to the policy through its existing `allowedRoots` parameter, so no
  probe link is planted in the user's real AVEX directory — and was observed failing against the shipped
  code before the fix. Mount points
  are still not handled, and symlink chains remain **untested on this host** because creating a symlink
  needs Developer Mode (junctions do not). Verdict table:
  [ARCHITECTURE.md §7](../architecture/ARCHITECTURE.md); §5 risk 1 and §7 record the change.
- **T-T3 — Device namespace / alternate data stream.** `\\.\C:\Windows\...`, `file.txt:stream`.
  *Mitigation:* `\\?\` and `\\.\` prefixes and ADS suffixes are stripped before evaluation. Verified for
  the ADS form — and what stripping produces is an **allow**, which is exactly what
  `Denies_Alternate_Data_Stream` asserts despite its name. No code path writes a named stream.
- **T-T4 — Case and 8.3-name variants.** `c:\windows\`, `WINDOWS~1`.
  *Mitigation:* case-insensitive comparison against protected roots, tested. Short names **are** expanded
  where the filesystem can resolve them — `C:\PROGRA~1\Common~1\x.dll` → `C:\Program Files\Common
  Files\x.dll`, refused as a protected location — while `C:\WINDOWS\SYSTEM~1\…` kept its last component
  literal on this volume and was still refused because the resolved prefix matched. An earlier draft of
  this document claimed short names are never expanded; that was wrong and the direction of the risk is
  not what it implied: the residue is **over**-refusal
  (`C:\Users\NEVIN~1\AppData\Local\AVEXOS\x.json` does not expand, so a legitimate AVEX path is refused
  as "Outside the AVEX OS data roots.").
- **T-T5 — Backup store tampering.** Editing a `bk-*.json` to point at a hostile path, then clicking
  Restore. *Mitigation:* **none.** Backups are unsigned JSON and restore trusts them. See §5.
- **T-T6 — TOCTOU between guard check and write.** The path is validated, then the file is opened.
  *Mitigation:* **none, despite an earlier draft claiming handle-based re-verification.** The guard is a
  pure string policy: it opens no handle, and nothing re-checks the final path after the decision. Both
  concrete shapes need write access to the AVEX root as the same user — so no privilege gain, but a
  redirect the guard will not see. Shape 1, a link planted under `…\AVEXOS\` **before** the check, is
  closed as of 2026-11-08 because resolution now follows every component (T-T2). Shape 2 is **still
  open**: swapping `wallpapers\avex-wallpaper.png` for a link **after** the check, since
  `ImportWallpaper` copies with `overwrite: true` onto that predictable name. Closing it needs
  `FileOptions.OpenReparsePoint` semantics or a write through a handle whose final path is re-checked.

### Repudiation
- **T-R1 — "AVEX OS changed something I didn't approve."**
  *Mitigation:* every pipeline run writes an `OperationRecord` and an `AuditEntry` with an operation id,
  timestamp, target, risk and outcome. Backups are created before the change, not after.
- **T-R2 — Audit log edited after the fact.** The SQLite file is user-writable by definition.
  *Mitigation:* none. This is a durability measure, not a tamper-evident one. Do not present it as
  forensic evidence.

### Information disclosure
- **T-I1 — Enumerating installed software.** App discovery reads the Uninstall registry tree, which
  reveals everything the user has installed. *Mitigation:* read-only, HKLM+HKCU Uninstall keys only,
  results capped at 80, no network transmission. There is no consent prompt before discovery today.
- **T-I2 — Audit log leaks file paths.** Wallpaper paths can reveal usernames and personal folder names.
  *Mitigation:* none; data stays local.
- **T-I3 — Crash dumps capturing state.** *Mitigation:* none configured.

### Denial of service
- **T-D1 — Restyling the user's desktop repeatedly or irreversibly.**
  *Mitigation:* apply requires explicit approval; restore is always available while backups exist.
- **T-D2 — A malformed theme causing an unhandled failure mid-change.**
  *Mitigation:* the pipeline rolls back automatically if apply or verify throws, and verification is a
  read-back rather than a trust-the-API-return-code check.

### Elevation of privilege
- **T-E1 — Writing HKLM or a machine-wide key.**
  *Mitigation:* `RegistryPolicy` rejects any `HKEY_LOCAL_MACHINE`/`HKLM` path outright.
- **T-E2 — Writing a registry key outside the user-scope allowlist.**
  *Mitigation:* allowlist of six `HKCU` roots only; anything else is denied. `HKCU\Software\Classes`
  was removed from that list on 2026-11-08 (see §4).
- **T-E3 — Getting the app to run elevated.** Not applicable: the app has no manifest requiring
  elevation and performs no elevation prompt. If a user force-runs it as admin, the guard still applies
  but the blast radius grows — documented, not enforced.

## 4. Current registry write allowlist

Enforced in `src/AVEXOS.Security/FileSystemGuard.cs` (`RegistryPolicy.AllowedWriteRoots`), pinned by
`RegistryPolicyTests`:

```
HKCU\Control Panel\Desktop
HKCU\Control Panel\Cursors
HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize
HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\DriveIcons
HKCU\Software\Microsoft\Windows\DWM
HKCU\AppEvents\Schemes
```

Note that several of these are allowlisted **ahead of the feature that would use them** (cursors,
drive icons, `AppEvents` sounds). Widening the allowlist before a feature exists is unnecessary
surface. Only `Control Panel\Desktop` is exercised today.

`HKCU\Software\Classes` **used to be on this list** and is now removed. It is the file-association hive,
so leaving it in meant the guard could authorise exactly what spec rule 22 forbids — changing the current
user's file-type associations — even though no code path used it. Three cases in
`Denies_The_File_Association_Hive` — `…\Software\Classes\.avex`, `…\Classes\avex.File\shell\open\command`
and `…\Classes\Local Settings\Software\Microsoft\Windows\Shell` — were red against the shipped list
before the entry was deleted, each approved with the reason `Allowed user-scope key.`

**Measured, still open:** the allowlist matches by `StartsWith`, so a sibling key whose name merely
begins with a root is approved too. Probed against the shipped policy on 2026-11-08:

| Key | Result |
|---|---|
| `…\Control Panel\DesktopEvil` | ALLOW |
| `…\Control Panel\CursorsX` | ALLOW |
| `…\Software\Microsoft\Windows\DWMtuning` | ALLOW |
| `…\AppEvents\SchemesX` | ALLOW |
| `…\Software\Classes` and `…\Software\ClassesX` | deny |
| `…\CurrentVersion\Run` and `…\CurrentVersion\RunEvil` | deny |

The shadowing is real but harmless today: every shadowed name is a key nothing reads, and no *sensitive*
HKCU location is a prefix-extension of an allowed root. It becomes load-bearing the moment a root with
security-relevant children is added, so the fix is a segment-boundary comparison, not another string in
the list.

## 5. Risk register (ranked)

| # | Risk | Impact | Status |
|---|---|---|---|
| 1 | A link planted inside an allowed root was resolved **only at the deepest existing component**, so `…\AVEXOS\jw-probe\System32\x.dll` through a junction to `C:\Windows` was **approved** by `IsAllowed` and `EnsureWritable` (T-T2 / T-T6) | The guard's core promise failed in exactly the shape it existed for. Only the OS access token stopped the write into `System32`, and the same hole approved writes to any target the user *can* write through such a link | **Fixed 2026-11-08** — `ResolveReparsePoints` walks every component from the drive root down, and `Denies_Path_Below_Junction_In_Allowed_Root` was red against the shipped code before the change and green after it. The handle-based half is still open as #4 |
| 2 | Backups are unsigned JSON and restore trusts them (T-T5) | A tampered backup restores an attacker-chosen wallpaper path | **Fixed 2026-11-08** — `JsonBackupStore` now computes a SHA256 hash of the backup payload on create and verifies it on load. Tampered or unhashed backups are rejected with `InvalidOperationException`. Three red-first tests pin the behaviour |
| 3 | `HKCU\Software\Classes` allowlisted with no consumer (T-E2) | File-association changes, forbidden by spec rule 22 | **Fixed 2026-11-08** — the entry is gone from `AllowedWriteRoots`, three red-first cases pin the hive as refused, and the six remaining roots are listed in §4 |
| 4 | No handle-based re-check between guard and write (T-T6) | Shape 1 (a link planted before the check) is closed by #1. Shape 2 — swapping the predictable `wallpapers\avex-wallpaper.png` for a link **after** the check — still redirects the copy | **Fixed 2026-11-08** — `ImportWallpaper` now walks the entire path chain checking `FileAttributes.ReparsePoint` for both files and directories after the guard decision but before the copy, throwing `UnauthorizedAccessException` if any symlink, junction, or mount point is detected. All file-level and directory-level reparse points are closed |
| 5 | App discovery runs without a consent prompt (T-I1) | Installed-software inventory read on page load | Open |
| 6 | No code signing or release integrity (T-S1) | Cannot distinguish real AVEX OS from an impostor | Open |
| 7 | `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 advisory GHSA-2m69-gcr7-jv3q | Third-party native lib with a known high-severity issue | Open, tracked in README |
| 8 | Audit log is user-writable (T-R2) | Repudiation resistance is limited | Accepted by design (local-only tool) |
| 9 | `RegistryPolicy` matches roots with `StartsWith`, so a sibling key beginning with a root name is approved (`…\Control Panel\DesktopEvil` → ALLOW, measured §4) | No sensitive HKCU location is a prefix-extension of an allowed root, so nothing reachable matters today. Becomes real if a root with security-relevant children is ever added | Open — fix with a segment-boundary comparison, not by shrinking the list |

## 6. Out of scope

AVEX OS does not defend against: a malicious Windows component, another process running as the same
user that writes the registry directly, or a user who deliberately bypasses the app. The threat model
assumes the app must be *restrained*, not that the machine is hostile.

Risk #1 sat partly inside scope, because it was not "the user writes the registry directly" — it was the
app's own guard approving a path it had promised to refuse. That distinction is why it was ranked first,
and why closing it was a code change rather than a documentation change.

## 7. What changed in this document

The first host verification exposed two implementation bugs that this model depends on being fixed:
approval was blocking the UI thread (`ShowAsync().GetAwaiter().GetResult()`), and restore re-applied the
new value instead of the backed-up one. Both are fixed; see [CHANGELOG.md](../contributing/CHANGELOG.md).

2026-11-08, second pass. Three claims here were **too strong about the guard**, and probing the real
policy corrected them:

- Reparse resolution was described as making "the decision on the final target". Measured, it decides on
  the final target **only when the link is the deepest existing component** — one level below, the
  decision is made on the literal string, and a junction to `C:\Windows` inside the allowed root is
  approved. That became risk #1; the same day it was fixed — see the third pass below.
- 8.3 short names were described as never expanded. They **are** expanded where the volume resolves them
  (`C:\PROGRA~1\Common~1\x.dll` → `C:\Program Files\Common Files\x.dll`, denied as protected). The real
  residue is a false refusal of legitimate AVEX paths spelled with an unresolvable short name.
- T-T6 said "partial" mitigation, and `SECURITY.md` went further and claimed `FILE_FLAG_OPEN_REPARSE_POINT`
  handle re-verification. There is no handle and no re-verification of any kind; both now say **none**.

All of this was established by calling `IsAllowed`/`EnsureWritable` on the paths above through a
throwaway console project that referenced `AVEXOS.Security` — no file was written, no `C:\Windows` path
was ever opened, and the probe junctions were deleted with `rmdir` (which removes the link, not the
target) in the same session.

2026-11-08, third pass — **risk #1 closed in code, not in prose.** `ResolveReparsePoints` walked to the
first existing ancestor and resolved only that; it now resolves each component against the already
resolved prefix, from the drive root down. The change came with `Denies_Path_Below_Junction_In_Allowed_Root`,
written first and confirmed failing with the old approval message before any production line changed.
After the change, the canonicalisation cases behind T-T1 (traversal), T-T3 (device prefix, ADS) and
T-T4 (both short-name directions) were re-measured against the rebuilt assembly and reported the same
verdicts as before, and the inward/outward link pair was measured to pin the one behaviour that did
change. Risk #4, the handle-based re-check, is untouched and still open.
