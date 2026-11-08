# User Guide

AVEX OS Control Center is one window with five pages. This guide describes what each page actually does
today. Where a capability is not implemented, it says so rather than describing a future behaviour.

## Before you start

- AVEX OS changes **your** desktop settings, not the whole machine. Another user account is unaffected.
- Every change it makes is backed up first and can be undone.
- Nothing is applied without you clicking a confirmation, except **Restore**, which acts immediately
  because clicking it is itself the instruction to undo.

## Home

A status board. Shows the active theme, light/dark mode, the Windows build, how many applications were
discovered, how many targets are supported, how many backups exist and how many operations are pending.

These numbers are read from the local database and the live system — they are not decorative. If
"Backups" is zero you have not made a change yet.

## Appearance

The one fully working change flow.

**Desktop Wallpaper** shows the path Windows is currently using, read live from the system.

- **Apply AVEX Wallpaper** — copies the bundled AVEX wallpaper into
  `%LOCALAPPDATA%\AVEXOS\wallpapers\`, saves your current wallpaper setting to a backup, asks you to
  confirm, applies the new wallpaper, then **reads the value back** to confirm it actually took. The
  status line tells you which backup was created.
- **Restore Original** — loads the most recent backup for the wallpaper target and applies it, then
  verifies. This returns the desktop to whatever it was before that change.

Worth knowing:

1. The copy and the backup happen **before** the confirmation dialog. If you click Cancel, your
   wallpaper is unchanged but both files are already on disk — inside AVEX OS's own folders, never in
   your documents.
2. If verification fails, AVEX OS **rolls the change back automatically** and tells you. The rollback is
   not re-verified, so if the rollback itself fails the status line says "Rollback failed: …" and you
   need **Restore Original**; see [BACKUP_AND_RECOVERY.md](../operations/BACKUP_AND_RECOVERY.md) §4.
3. Verification compares the wallpaper **path** only — the fill style and tiling are trusted from the
   same write.
4. "Restore" always uses the newest backup for that target. If you apply twice, the newest backup holds
   the value from just before the second apply — not your original desktop. To step further back, open
   `%LOCALAPPDATA%\AVEXOS\backups\` and note the older `bk-*.json` file.

## Themes

Lists the two built-in themes (**AVEX Dark** and **AVEX Light**) and generates a theme proposal from a
plain-English description. Select a row, then "Apply to Control Center" — what applies is the theme you
selected, confirmed on 2026-11-08 by measuring the rendered window (content-area luminance 14.3 for
dark, 102.5 for light). Your choice is remembered between launches.

**Important limitation:** applying a theme changes the appearance of **the AVEX OS window only**. It sets
the light/dark request on the Control Center. It does not restyle Windows, the taskbar, Explorer or any
other application, and it will not make your desktop look like the theme preview. Within the app, the
visible effect is the dark/light switch: the colours, radii, spacing and font sizes listed in a theme
file are validated but not yet rendered, so **two themes that both say `"mode": "dark"` look identical**
when applied, however different their palettes are. The proposal generator
is local and deterministic — it is keyword matching, not an AI model, and it makes no network calls. A
generated proposal is previewable text only; it is not applied to anything, and the palette it proposes
would not change the rendering today either.

A theme that fails validation does not appear in the list at all, and nothing tells you why — this is a
known gap.

## Applications

A read-only inventory of installed software, gathered from the registry Uninstall keys. Machine-wide and
per-user entries are merged, system components and updates are filtered out, and results are capped at 80.

For each app it reports what AVEX OS can actually do to it. Most entries will show **no supported
customization**, because safely restyling a third-party application requires either cooperation from that
application or one of the techniques AVEX OS is forbidden from using. This page is deliberately blunt.

Discovery reads your installed-software list on page load. Nothing is sent anywhere — there is no network
code in the app.

## Activity

The audit trail: every operation, its target, risk level, outcome and the backup it produced. Use this to
answer "what did AVEX OS change, and when?".

## What AVEX OS will not do

These are not missing features waiting for a later release — they are excluded by design, because the
only ways to achieve them involve patching Windows binaries, injecting into other processes, or writing
to protected system locations:

- Restyle the taskbar, Start menu, window title bars or system dialogs
- Change how Explorer's chrome or context menus look
- Change the appearance of files you own by rewriting the files themselves
- Change file extensions or file-type associations as a cosmetic shortcut
- Disable Defender, Firewall or UAC to make something "work"

If another tool claims to do these, it is doing them the way these rules forbid.

## If something looks wrong

See [TROUBLESHOOTING.md](../operations/TROUBLESHOOTING.md).
