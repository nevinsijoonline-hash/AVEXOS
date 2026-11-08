# Contributing

Read [README.md](../../README.md) "Read this first" before anything else. Most of what could go wrong
in a contribution here is not a coding mistake — it is a claim that AVEX OS can do something it cannot.

---

## 1. Working copy state

**This tree is not under version control.** There is no `.git`, no remote, no branch history, and no
pull-request flow available:

```
$ git rev-parse --is-inside-work-tree
fatal: not a git repository
```

So "commit", "branch" and "merge" below describe the rules to follow once the project is initialised,
not a workflow that exists today. Do not invent a history or a remote in documentation. Until then,
changes are made in place and verified by running them
([DEVELOPER_GUIDE.md](../setup/DEVELOPER_GUIDE.md) §2, §7).

When this does become a repository, initialize it without committing `obj/`, `bin/`, or anything under
`%LOCALAPPDATA%\AVEXOS` — that lives outside the tree, but built output inside it must stay ignored.

---

## 2. Before you write code

1. Say which **target id** you are touching (`windows.desktop.wallpaper`, `avex.controlcenter`, …) and
   which capability level it is. If the level is D, the correct change is to the matrix, not the code.
2. Confirm the mechanism is a documented, supported, user-scope API. If it needs a patched binary, an
   injected DLL, an undocumented hook, `HKLM`, or elevation — stop. That is prohibited by the
   specification, not merely unfashionable, and it is the exact technique the tools that "restyle the
   taskbar" use.
3. Know how you will prove it worked. "The API returned true" is not proof; reading the value back out
   of Windows is.

If any of those three fail, the honest contribution is a matrix row saying *unsupported, because…* —
which is a real, accepted deliverable in this project.

---

## 3. The gates every change must pass

Build and test from the repository root:

```bat
set "DOTNET_ROOT=C:\Users\<you>\AppData\Local\Microsoft\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
dotnet build AVEXOS.slnx
dotnet test tests\AVEXOS.Security.Tests\AVEXOS.Security.Tests.csproj --no-build
dotnet test tests\AVEXOS.ThemeEngine.Tests\AVEXOS.ThemeEngine.Tests.csproj --no-build
```

Green baseline on 2026-11-08: **0 errors, 6 distinct warnings, 178 tests passing (22 + 28 + 15 + 32 + 29 + 52)**.

- **The security suite is a release gate.** A failing `ProtectedPathPolicyTests` or
  `RegistryPolicyTests` case blocks the change, full stop. Those 24 cases are the reason the guard means
  something: each one is a request the app is promised to refuse.
- Do not increase the distinct warning count — six today: four `NU1903` (the SQLitePCLRaw advisory, one
  per project that transitively references it) and two `CS0168`. Both are documented debt, not a licence
  to add more. Do not chase MSBuild's headline total either; it counts occurrences and moves between 10
  and 12 depending on whether you rebuilt.
- Build the **solution**, never a single csproj — the two write to different output directories and a
  csproj build that "succeeds" will silently leave you testing an old binary
  ([TROUBLESHOOTING.md](../operations/TROUBLESHOOTING.md) §2). Stop the running app first; a live
  instance turns copy steps into MSB3021/MSB3027 "errors".

### Host-touching changes need host proof

Tests cannot cover what happens to a real desktop. Run the harnesses and paste their output into the
review:

| Change | Proof |
|---|---|
| anything that writes | `scripts/gui-cycle.ps1` (apply → approve → verify → restore) with the registry values shown before and after — this **does** change the reviewer's wallpaper and restores it |
| theme/token work | `scripts/theme-probe.ps1`, plus `scripts/capture-window.ps1` for the rendered result |
| launch/UI plumbing | `scripts/capture-window.ps1` — window class, title and pixel histogram |
| accessibility | `scripts/uia-dump.ps1`; names must be meaningful, since that is what a screen reader says |

End the session with the host in the state it started in. Leaving a user's desktop on the AVEX
wallpaper after "verifying" restore is a failed verification, not a minor mess.

---

## 4. Review checklist

The questions that have actually caught bugs here, in rough order of value:

1. **Does the claim match the code?** UI text, docs and changelog must not describe a surface AVEX
   does not write. "Applied" for an app-internal setting, or "themes your taskbar" for a Level D
   target, is a defect.
2. **Is verification real?** `verify` must read from the OS, not from a cache or the value just
   handed in. If `verify()` can only ever return true, the pipeline's rollback will never fire.
3. **Where does Restore get its state?** From the newest backup for that target id, not from anything
   the page remembered. The original implementation cached it and re-applied the new value.
4. **Is every host write inside `ChangePipeline`?** A service called directly from a button is a bug
   even when it works.
5. **Is the guarded path the written path?** Use `guard.CanonicalPath` for the write; re-using the
   caller's string reopens the symlink/junction route the guard just closed.
6. **Does anything block the UI thread?** `approve` is awaited, and no `ConfigureAwait(false)` around
   apartment-sensitive calls.
7. **What happens on partial failure?** Read-back failing should roll back; rollback failing should be
   recorded honestly as `Failed` with "Rollback failed", not as `RolledBack`.
8. **Does the diff stay in scope?** One topic per change. A fix plus an unrelated refactor is two
   reviews, and the safety-critical one gets less attention.
9. **Are the tests for the thing that broke?** A bug that was found on the host and only fixed on the
   host will come back; add a test where the logic is pure, and record the host evidence where it
   is not.
10. **Did the docs move with the code?** See §5.

---

## 5. Documentation is part of the change

Part 25 of the specification requires these, and accuracy is the requirement — not volume:

| File | Owes |
|---|---|
| `README.md` | what works, what doesn't, verified how |
| `docs/architecture/ARCHITECTURE.md` | layers, pipeline, contracts; **[built]** vs **[planned]** |
| `docs/architecture/DESIGN_SYSTEM.md` | tokens, type, components, a11y contract |
| `docs/compatibility/COMPATIBILITY.md` | level A–D per target + the host verification log |
| `docs/security/SECURITY.md` | policy, allowlist, escalation cases |
| `docs/security/THREAT_MODEL.md` | STRIDE entries, open risks, ranked |
| `docs/setup/INSTALLATION.md` · `USER_GUIDE.md` · `DEVELOPER_GUIDE.md` | build/run, day-to-day, how to extend |
| `docs/operations/BACKUP_AND_RECOVERY.md` · `TROUBLESHOOTING.md` | what is saved, how to recover, known failures |
| `docs/contributing/CONTRIBUTING.md` · `CHANGELOG.md` | these rules, dated changes |

Rules that follow from "documentation must accurately describe supported functionality":

- Mark anything designed-but-unbuilt as **[planned]** or "not implemented". A reader must never have
  to discover the gap by trying it.
- Report three states distinctly: **written**, **read back**, **visually confirmed**. If only the first
  two happened, say so.
- Numbers in docs are measured, not remembered: test counts, app counts, warning counts. Date them.
- Fix overclaims when found, even if the overclaim is in your own last commit. Both
  `README.md` and `ARCHITECTURE.md` were rewritten for this reason on 2026-11-08.
- Never soften a refusal. "Not supported because Windows exposes no documented API for it" is the
  correct sentence, and it is a deliverable.

---

## 6. Style

The code should read like the code around it. Specifics that are load-bearing here, not taste:

- Comments explain **why**, and only where the constraint is invisible — the STA reason in
  `ChangePipeline`, the filename-ordering dependency in `JsonBackupStore.List`, the reparse-point
  reason in `ProtectedPathPolicy`. Nothing that restates the line above it.
- No defensive branches for states that cannot occur. Validate at the boundaries (user input, AI
  output, registry, imported theme) — the interior is trusted.
- Names state the target: `windows.desktop.wallpaper`, not `target1`.
- `ChangeResult` / `GuardResult` carry reasons; surface them instead of collapsing to `bool`.
- No emoji in code, UI text, commit messages or docs.
- Harness scripts stay ASCII-only: Windows PowerShell 5.1 decodes non-BOM files as ANSI and a single
  non-ASCII character can break parsing mid-script.

---

## 7. What will be rejected

Not an exhaustive list, but every one of these has been proposed or shipped here at some point:

- Writing, renaming or replacing anything under `C:\Windows`, including via a link or junction that
  resolves there.
- Shell extension injection, `Explorer.exe` patching, icon-resource surgery, or a
  TrustedInstaller-ownership change.
- Disabling or configuring around Defender, Firewall or UAC — including "temporarily, for testing".
- An `HKLM` write, or elevation used to make a blocked path writable.
- Changing file extensions or file-type associations as a branding shortcut (spec rule 22).
- Shipping an AI-generated change that a human did not validate against the theme schema and the
  capability matrix — and never giving a model unrestricted host access.
- Presenting a Level D target as supported, or reporting an appearance change as applied before it has
  been read back.
- Sacrificing functionality for appearance: a themed app that cannot be used is a broken app.
- An unverifiable claim in docs, a changelog, or UI text.

## 8. Adding tests

xunit; one test project per subject project, mirroring the `src` name with a `.Tests` suffix. Pure
logic gets unit tests — the path policy and the theme validator each have a suite. Behaviour that only
exists when it touches a live desktop does **not** get a fake test; it gets a harness run and a dated
line in [COMPATIBILITY.md](../compatibility/COMPATIBILITY.md) §6.

Priority for the missing coverage, roughly by how much it protects the safety story:
`ProtectedPathPolicy` edge cases beyond the current 14 → `ChangePipeline` (rollback, verify failure,
decline) → `JsonBackupStore` ordering and round-trip → `WallpaperService` read-back → discovery.
