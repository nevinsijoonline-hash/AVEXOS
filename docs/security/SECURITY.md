# AVEX OS — Security Model & Threat Model

**Status:** Phase 1 deliverable

**How to read this document.** Each claim is marked **[enforced]** where code in this repository
implements and exercises it, and **[design intent]** where it does not yet exist. Part of the original
Phase 1 text described mitigations that were never built; leaving them unlabeled in a security document
is worse than leaving them out, so the labels are load-bearing. For what the guard actually does and
does not resolve, see [ARCHITECTURE.md §7](../architecture/ARCHITECTURE.md); for the ranked live risks,
see [THREAT_MODEL.md](THREAT_MODEL.md).

---

## 1. Security objectives

| Objective | Enforcement | Status |
|---|---|---|
| Never modify `C:\Windows` or protected component stores | Canonical-path guard, deny-by-default, single write chokepoint | **[enforced]** for the one write path that exists (wallpaper), whose destination AVEX builds itself under `%LOCALAPPDATA%\AVEXOS\wallpapers\`. The policy has a real gap one component below a planted link — see the junction row in §3 |
| Never break an application to change its look | Documented mechanisms only; no patching/injection | **[enforced by absence]** — no injection, hooks, or patched targets exist in the tree |
| Every change reversible | Backup precedes apply; rollback on verification failure | **[enforced]** backup-before-apply is structural in `ChangePipeline`; rollback runs but is **not re-verified** |
| No silent privileged action | Risk classification + explicit approval + audit | **[partly]** approval and audit are real; `RiskLevel` is recorded and never gates anything |
| No secret leakage | API keys in Windows Credential Manager, never in files or logs | **[design intent]** — there is no `PasswordVault` or Credential Manager call anywhere in the tree, because there are no credentials |
| No silent data collection | Diagnostics are opt-in and scoped to AVEX state | **[enforced by absence]** — no network code and no telemetry exist; `bootstrap.log` is local-only but see §5 |

---

## 2. Trust boundaries

```
[User input / AI text]  ──► validation ──► structured proposal ──► pipeline   [design intent]
[Theme ZIP / JSON]      ──► schema + asset validation ──► sandboxed extraction ──► pipeline   [design intent]
[Plugin]                ──► out-of-process host (post-1.0), no direct Win32 ──► pipeline   [design intent]
[Pipeline]              ──► WindowsIntegration only ──► OS   [enforced]
```

What is real today is only the last line, and it is narrower than it looks:

- **There is no import path at all.** Themes come from two JSON files embedded in
  `AVEXOS.ThemeEngine` at build time; the user cannot supply a theme file, a ZIP, an image archive or a
  plugin. `ZipArchive` appears nowhere in the source. So the "sandboxed extraction" boundary has nothing
  to guard, and no untrusted theme can reach `ThemeValidator` from outside the build.
- **"AI text" is not an input.** `LocalProposalService` is deterministic keyword matching with no model
  and no network. The first line above describes a boundary that would need to be built before any
  provider is wired in; when it is, unvalidated proposal text reaching `ChangeRequest` is exactly the
  threat this row was written for.
- `App.Trace` writes `%LOCALAPPDATA%\AVEXOS\logs\bootstrap.log` **without** passing the guard
  ([`App.xaml.cs:19`](../../src/AVEXOS.App/App.xaml.cs)) — an accepted exception for startup diagnosis,
  since it runs before the container exists. It is the one place that bypasses the chokepoint.

---

## 3. STRIDE summary

| Threat | Vector | Mitigation as designed | What is actually in place |
|---|---|---|---|
| **Spoofing** | Malicious adapter impersonates a supported app | Adapters compiled-in and signed; no remote loading | **[design intent]** — there are no adapters at all. `CapabilityRegistry` only returns a level and a reason string; nothing writes to any app |
| **Tampering** | Theme payload changes an unrelated registry key | Declarative tokens; target IDs from a fixed registry, never the payload | **[enforced]** `ThemeDocument.Targets` maps id → level, and `ThemeService.Apply` ignores it entirely; the only registry keys reachable are the six in `RegistryPolicy`. A payload cannot name a key |
| **Repudiation** | User denies a change | Append-only audit with operation IDs and verification results | **[enforced]** `AuditEvents` is insert-only (no UPDATE/DELETE path) and `Operations` records `Verified`/`RolledBack`/`Denied`/`Failed`. Caveat: `AuditEvents` has **no primary key**, and `avex.db` is a plain user-writable file — it is a log, not evidence |
| **Information disclosure** | AI request or logs upload user data | Prompt + schema only, attachments by consent, no file enumeration | **[moot today]** nothing leaves the machine. Locally, `bootstrap.log` and `AuditEvents` do store full paths and display names of installed software; that stays inside `%LOCALAPPDATA%\AVEXOS` |
| **Denial of service** | Huge theme, zip bomb, icon bomb | Size caps (1 MB JSON), asset count, per-asset and extraction totals | **[design intent]** — no cap exists. `BuiltInThemeCatalog` deserialises embedded resources with no `MaxLength`, and `ThemeValidator` checks value ranges, not payload size. Unreachable via user input only because there is no import path |
| **Elevation of privilege** | Path traversal into protected dirs; junction redirect; elevated helper abused | Canonicalisation + reparse resolution + deny list; signed operation descriptor re-validated by a helper | **[partly enforced]** see the case table below. **There is no elevated helper**, no signed descriptor, and no `WindowsIdentity`/runas check — the app is single-instance user-privilege only, which is also why nothing here needs UAC |

### Specific privilege-escalation cases

| Attack | Guard behaviour, verified against the code |
|---|---|
| `..\..\Windows\System32\x.dll` | **[enforced, but not by the check its name suggests]** measured 2026-11-08: `%LOCALAPPDATA%\AVEXOS\..\..\Windows\System32\x.dll` folds to `C:\Users\Nevin Sijo\AppData\Windows\System32\x.dll`, which is **not** under `C:\Windows`, so `Denies_Traversal_From_Allowed_Root` passes on the allowlist ("Outside the AVEX OS data roots."), not on the protected-root match. Four `..` instead of two lands in `C:\Users\Windows\System32` — still outside both. The load-bearing property is that *nothing* outside the two AVEX roots is writable, so a traversal that misses the protected list still gets refused; a protected-root match is proven separately by the direct `C:\Windows\…` rows and by the short-name row below |
| Junction `%LOCALAPPDATA%\AVEXOS\j → C:\Windows` | **[enforced, both at the link and below it since 2026-11-08]** `Directory.ResolveLinkTarget(…, returnFinalTarget: true)` is now applied to **every** component from the drive root down, so the link is followed even when the directory beneath it exists *through* the link. Measured before the fix: the link path was refused as `Protected system location (C:\WINDOWS).` while `…\jw-probe\System32\x.dll` (leaf not yet existing) canonicalised to itself and `EnsureWritable` answered **ALLOW**. That is now `Denies_Path_Below_Junction_In_Allowed_Root`, which builds a real junction into `C:\Windows` inside a temporary allowed root, pins the canonical result to `C:\Windows\System32\x.dll`, and refuses it through both `IsAllowed` and `EnsureWritable` — red against the shipped code first, green after. Caveat: both junction tests `return` without asserting if `cmd /c mklink /J` fails, so a green row on another machine is not proof — see [ARCHITECTURE.md §11](../architecture/ARCHITECTURE.md). Consequence accepted: a link **outside** the roots pointing **into** them now resolves inside and is allowed, which is the promise being kept (the bytes land in the declared root) and is unreachable from any code path the app has. Full before/after table: [ARCHITECTURE.md §7](../architecture/ARCHITECTURE.md) |
| Symlink **chain** | **[unverified on this host]** `returnFinalTarget: true` would follow to the end, and the component-wise walk now applies it at every level, but creating a symlink requires Developer Mode or `SeCreateSymbolicLinkPrivilege`, so no test has ever exercised it here. Mount points are a separate mechanism and are not handled at all |
| `\\?\C:\Windows\...`, `\\.\C:\...`, UNC | **[enforced]** prefixes stripped before evaluation; UNC rejected. Tests: `Denies_Device_Prefix_Evasion`, `Denies_Unc_Paths` |
| `C:\WINDOWS`, `C:\WiNdOwS.`, trailing space/dot | **[enforced]** ordinal-ignore-case comparison plus per-segment trailing-dot/space trimming. Tests: `Denies_Case_Variants`, `Denies_Trailing_Dot_Evasion` |
| **8.3 short name** `C:\PROGRA~1\Common~1\x.dll` | **[enforced where it resolves]** measured on .NET 10.0.14 here, 2026-11-08: canonicalisation returned `C:\Program Files\Common Files\x.dll` and the guard denied it as `Protected system location (C:\Program Files).` Where a name does not resolve — `C:\WINDOWS\SYSTEM~1\kernel32.dll` kept its last component literal — the resolved prefix still matched a protected root, so it was denied anyway. The residual problem points the other way: `C:\Users\NEVIN~1\AppData\Local\AVEXOS\x.json` is never expanded and is therefore **refused** as "Outside the AVEX OS data roots." Short names are a false-refusal hazard, not a bypass, until the allowed roots are widened — which is the condition that would change that |
| Alternate data stream `file:stream` | **[enforced as stripping, not denial]** verified: `…\AVEXOS\wallpapers\x.png:hidden` canonicalises to `…\x.png` and the policy **allows** it. `Denies_Alternate_Data_Stream` asserts exactly that — it proves suffix stripping, the opposite of its name. No code writes a stream |
| Time-of-check/time-of-use swap after the guard | **[design intent]** — nothing here is true today. The pre-check half closed 2026-11-08 (a link planted *before* the check is now followed, row above), but this row is about the window *after* it. The guard is a pure string policy: no handle is opened, `FILE_FLAG_OPEN_REPARSE_POINT` appears nowhere, and no final-path re-verification happens after `File.Copy`. `ImportWallpaper` uses `overwrite: true` onto a predictable filename, so an attacker who can plant a symlink at `%LOCALAPPDATA%\AVEXOS\wallpapers\avex-wallpaper.png` **between** the check and the copy redirects the write. That requires the same user account, which already has write access to the directory — the practical impact is a redirected write, not privilege gain. The fix is `FileOptions.OpenReparsePoint` semantics or writing via a handle whose final path is re-checked |

---

## 4. Non-goals and hard prohibitions

AVEX OS will never: disable Defender, Firewall, UAC or Windows Update; bypass TrustedInstaller; patch
protected binaries; inject into protected processes; take ownership of system files; install
persistence without consent; run hidden commands; touch boot or recovery partitions.

Any feature request that requires one of these is answered by declaring the target **Level D —
unsupported**, with the reason shown in the Compatibility Center.

**[status of this section]** Every clause is currently honoured, and the strongest argument for that is
absence: no process-injection, service-installation, Run-key, scheduled-task, driver or shell-extension
code exists in the tree, so there is nothing to audit for a violation. The one honest qualification is
that "the reason shown in the Compatibility Center" describes a screen that does not exist yet — what
ships is `ApplicationsPage`, which prints `CapabilityReason` next to each discovered app, and Level D
rows are the majority.

---

## 5. Secrets

**[design intent — nothing to protect yet]** No credential store is used, because no provider is wired
in: `PasswordVault`, `CredentialManager` and any API-key handling appear zero times in the source. The
rules below are the contract for whoever adds the first provider, not a description of today.

- Provider credentials stored via `Windows.Security.Credentials.PasswordVault` (or Credential Manager
  fallback).
- Redaction filter on the logging sink for anything matching key-like patterns — **[not implemented]**;
  `App.Trace` writes raw exception text to `bootstrap.log` with no filtering. Today that is safe because
  no secret is in scope, and it becomes a live risk the moment a provider token is passed through a
  code path that can throw.
- No telemetry by default — **[enforced by absence]**: there is no network code at all, so there is no
  payload to enumerate. If enabled, the payload must be enumerated in the settings UI before the first
  send.

---

## 6. Release integrity

**[design intent — none of this exists]** There is no installer, no code signature, no update manifest,
no updater, and no CI in this repository (verified 2026-11-08: no `.yml`/`.yaml` pipeline files, no
`installer/` directory, no `*.pfx`, nothing in `assets/branding/` or `assets/icons/`). The app is run
from a debug build directory via `scripts/launch-app.cmd`. Consequences worth stating out loud:

- The `signed installer` / `signed update manifest` / "verified by signature **and** SHA-256" bullets
  describe a release process that has not been built. Nothing prevents a modified build from being
  launched, and nothing verifies what is launched.
- Backups are the closest thing to an integrity problem that **is** real today: `JsonBackupStore` writes
  unsigned, unhashed JSON and `Load<T>` accepts whatever is in the file, so a restore trusts a
  user-writable blob. Recorded in [THREAT_MODEL.md](THREAT_MODEL.md) §5.
- "Dependency scanning in CI; fails on known-vulnerable packages" — the only mechanism that exists is
  `dotnet build` emitting `NU1903` warnings when vulnerable packages are present. As of 2026-11-08,
  the `SQLitePCLRaw.lib.e_sqlite3` advisory (GHSA-2m69-gcr7-jv3q) is **resolved** (upgraded to 2.1.13
  via `Microsoft.Data.Sqlite` 10.0.14). Current build: 0 vulnerable-package warnings.

---

## 7. Security review gates

Security-sensitive code (guard, canonicalisation, integration layer, update verification, plugin host)
requires a second review and a passing protected-path test suite before merge.

**[partly manual, no mechanism]** There is no git repository in this tree, so there is no branch, no
merge and no hook to enforce anything; "before merge" is currently a human habit, not a gate
([CONTRIBUTING.md §1](../contributing/CONTRIBUTING.md) records this). `dotnet test` does exist and does
pass, and §3's case table says which of those tests are load-bearing and which two are weaker than
their names suggest. Of the five areas named above, only three have code: guard, canonicalisation and
the integration layer; update verification and the plugin host do not.
