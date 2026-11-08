# Backup & Recovery

What AVEX OS saves before it changes anything, how it puts it back, and — just as important — what it
does **not** back up.

Checked against the code and against the live desktop on 2026-11-08.

---

## 1. The rule the design starts from

A change that cannot be backed up and restored is not offered. In the pipeline this is not a
politeness: [`ChangePipeline.ExecuteAsync`](../../src/AVEXOS.Core/ChangePipeline.cs) creates the
snapshot **before** it calls `apply`, and if `Create` throws, the operation stops and returns
`Failed` with `Backup failed: …`. Nothing is touched. The same is true if reading the current state
fails first.

So for the one flow that ships (wallpaper):

```
read current  →  write snapshot  →  record pending  →  ask you  →  apply  →  read back  →  done
                   ↑ abort here if this fails           ↑ abort here, nothing applied
```

---

## 2. Where snapshots live

```
%LOCALAPPDATA%\AVEXOS\backups\bk-<yyyyMMdd>-<HHmmss>-<8 hex>.json
```

One file per snapshot, human-readable JSON, no database index. `BackupDir` is defined in
[`AvexPaths.cs`](../../src/AVEXOS.Core/AvexPaths.cs#L15) and every path in it is derived from
`%LOCALAPPDATA%\AVEXOS`, which `ProtectedPathPolicy` allowlists and `C:\Windows` never is.

A real snapshot, from this host:

```json
{
  "id": "bk-20261108-031909-8bf51076",
  "targetId": "windows.desktop.wallpaper",
  "createdUtc": "2026-11-08T03:19:09.3362459+00:00",
  "description": "Apply AVEX wallpaper",
  "payload": {
    "wallpaperPath": "C:\\Users\\Nevin Sijo\\AppData\\Local\\Packages\\...\\goku-perfected-3840x2160-25454.jpg",
    "wallpaperStyle": 10,
    "tileWallpaper": false
  }
}
```

`payload` is the **original** value, not the new one — the snapshot answers "what was there before I
touched it". For wallpaper that is `AppearanceState`: the path Windows was pointing at, the style
(`10` = fill on this host), and the tile flag.

Note what is *not* in the payload: no image bytes. The snapshot stores a **path**. See §6.

---

## 3. Restoring, the supported way

Press **Restore Original** on the Appearance page.

That button does not "undo the last click" and it does not use anything the UI remembered — those
were the original design's bug. It:

1. lists snapshots and picks the **newest one whose `targetId` is `windows.desktop.wallpaper`**;
2. deserialises its `payload`;
3. runs it through `ChangePipeline` like any other change — capture current, back that up too, apply
   the saved value, read it back and verify.

Verified end to end on this host, twice, against the real registry value rather than the app's own
idea of itself:

| Step | `HKCU\Control Panel\Desktop` → `Wallpaper` |
|---|---|
| before | the AVEX image (left over from an earlier apply) |
| after Restore | the original image |
| after Apply + approval | the AVEX image |
| after Restore again | the original image |

Because step 3 creates a snapshot of the pre-restore state, restoring is itself reversible. Your
history is a list of states, not a one-way trip.

The table above is a measurement of that scripted run, **not** a statement about the machine. Checked
2026-11-08, this host is on the AVEX image: the newest snapshot is
`bk-20261108-032502-8af92aad` ("Apply AVEX wallpaper") and `HKCU\Control Panel\Desktop\Wallpaper`
still points at `%LOCALAPPDATA%\AVEXOS\wallpapers\avex-wallpaper.png`. That snapshot's `payload` holds
the original image path, so **Restore Original** returns to it. To find out where you actually are,
read the registry value — the app's status line only tells you what the last click did.

---

## 4. Automatic rollback (different mechanism)

If `apply` throws, or `verify` reads back something different from what was written, the pipeline
calls its `restore` delegate with the state it captured **in memory** a moment earlier, records
`RolledBack`, and appends an audit row. No snapshot file is read.

Two limits to know:

- If that rollback itself throws, the result is `Failed` with "Rollback failed: …" — and the host is
  left in a half-changed state. The audit trail says so, but nothing recovers it automatically. Use
  §3 to put the value back.
- **The rollback is not re-verified.** The pipeline trusts `restore()` and does not read the value
  back afterwards. Recorded as a gap in
  [ARCHITECTURE.md](../architecture/ARCHITECTURE.md) §5; it matters only for a failing apply.

---

## 5. What counts as "newest"

`JsonBackupStore.List()` sorts by **filename descending**, not by the `createdUtc` field, then takes
the first `count` (the Appearance page asks for 50). That is only correct because the id format
`bk-yyyyMMdd-HHmmss-…` sorts lexically in time order.

Consequences:

- If the id format ever changes, restore silently picks the wrong snapshot. There is no test for
  this, so it would not be caught until someone pressed Restore.
- A wallpaper original older than the newest 50 snapshots of *any* target becomes invisible to the
  button. Nothing prunes snapshots, so reaching that takes a lot of applies — but the limit exists
  and nothing warns you as you approach it.
- Clock changes affect ordering: ids are UTC, but a machine whose clock jumped backwards can produce a
  filename that sorts oddly.

---

## 6. What is NOT backed up

Say this plainly, because it is where recovery expectations go wrong:

| Not backed up | Why, and what it means |
|---|---|
| **The image file itself** | The snapshot holds a path. If `%LOCALAPPDATA%\AVEXOS\wallpapers\avex-wallpaper.png` is deleted, restore points the desktop at a missing file and Windows shows a blank background. This is why deleting the AVEX data directory can strand a desktop — see [INSTALLATION.md](../setup/INSTALLATION.md) §6. |
| **The original user file** | Never modified, so nothing to save. AVEX copies an imported wallpaper into its own directory rather than editing yours. |
| **AVEX's own settings** (`%LOCALAPPDATA%\AVEXOS\avex.db`) | Theme id/mode, operation history, audit log. Losing them loses history, not your desktop. |
| **Theme apply** | Applies to the AVEX window only, is not a host change, and is undone by picking another theme. It never goes through the pipeline and writes no snapshot. |
| **Windows' own wallpaper history** | `WallpaperMRUList`, `WallpaperChoice`, `CachedFiles` and the WCE background mode are not touched. Windows may still offer its own recent picks. |
| **Anything Level D** | Taskbar, Start menu, Explorer, context menus, third-party app chrome. Not implemented, so nothing to back up. There is deliberately no snapshot for a change AVEX never makes. |

There is also **no backup of the backups**: no hash, no signature, no copy elsewhere, no export.

---

## 7. If the app won't start

The app is the only supported restore path, so losing it is the interesting failure. Order of
attempt:

1. **Fix the launch first.** The usual cause here is a missing `DOTNET_ROOT`, which shows a "you must
   install .NET" dialog from a process that looks like the app.
   [TROUBLESHOOTING.md](../operations/TROUBLESHOOTING.md) §1 walks through this. Getting the app up is
   the fastest route to a clean restore.
2. **Read what should go back.** Open the newest `bk-*.json` for
   `windows.desktop.wallpaper` in `%LOCALAPPDATA%\AVEXOS\backups` and note
   `payload.wallpaperPath`, `wallpaperStyle`, `tileWallpaper`.
3. **Check the file still exists.** If the recorded path points into
   `%LOCALAPPDATA%\AVEXOS\wallpapers\` and that directory is gone, restoring the path will not
   restore a picture — pick a real image first.
4. **Put the values back and let Windows re-read them.** Write `Wallpaper`, `WallpaperStyle` and
   `TileWallpaper` under `HKCU\Control Panel\Desktop`, then sign out and back in. Logoff is
   deterministic; a broadcast refresh without restarting Explorer may not repaint.

> **Verification status for step 4.** Steps 1–3 are verified. Step 4's registry write and logoff
> behaviour has **not** been verified on this host, and an ad-hoc attempt to call
> `SystemParametersInfo(SPI_SETDESKWALLPAPER)` from a throwaway PowerShell script *did* fail here
> (it returned false and read back empty) — a malformed P/Invoke, not a Windows limitation. Do not
> tell a user that a script restore works. Prefer getting the app running.

For reference, what the app itself does — and what a manual restore must reproduce — is in
[`WallpaperService.Apply`](../../src/AVEXOS.WindowsIntegration/WallpaperService.cs#L29): two registry
string values, then `SystemParametersInfo` with `SPIF_UPDATEINIFILE | SPIF_SENDCHANGE`.

---

## 8. Verifying a restore worked

The status line prints "Verified. Backup …" for both apply and restore — the pipeline's success text is
direction-neutral by design. To confirm the restore actually took effect, check the value Windows holds:

```powershell
(Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper).Wallpaper
(Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name WallpaperStyle).WallpaperStyle
```

Then compare against the snapshot:

```powershell
$bk = Get-ChildItem "$env:LOCALAPPDATA\AVEXOS\backups\bk-*.json" | Sort-Object Name -Descending | Select-Object -First 1
(Get-Content $bk | ConvertFrom-Json).payload
```

Match on path, style and tile flag. All three agreeing is the signal; a look-alike desktop is not.

---

## 9. Audit trail

`%LOCALAPPDATA%\AVEXOS\avex.db` records what happened, in two tables:

- `Operations` — one row per operation id, its target, kind, risk and final state
  (`Pending`, `Denied`, `Applied`, `Verified`, `RolledBack`, `Failed`).
- `AuditEvents` — one row per outcome, with the detail string, including which snapshot id was
  created.

The Activity page shows `AuditEvents`, newest first. A verified row reads "Verified. Backup bk-…",
which is how you connect a change to the file you would restore from. Two caveats: `AuditEvents.Utc`
is the sort key, and a rolled-back operation's row is the one that tells you the rollback was not
re-verified (§4).

Nothing prunes either table, and the database is not backed up itself.
