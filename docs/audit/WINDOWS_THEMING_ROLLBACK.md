# Windows Theming Rollback Guide

**Date Applied:** 2026-11-08  
**Scope:** Full AVEX OS theme applied to Windows 11 host system

---

## What Was Changed

### 1. Registry Changes (13 keys)

#### DWM Colorization
- `HKCU\Software\Microsoft\Windows\DWM\ColorizationColor` → `0xFF8B6E` (AVEX accent #6E8BFF in BGR)
- `HKCU\Software\Microsoft\Windows\DWM\ColorizationAfterglow` → `0xFF9980` (AVEX accent hover #8099FF)
- `HKCU\Software\Microsoft\Windows\DWM\EnableWindowColorization` → `1`

#### Theme Personalization
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\ColorPrevalence` → `1`
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\EnableTransparency` → `1`
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` → `0` (dark)
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\SystemUsesLightTheme` → `0` (dark)

#### Control Panel Colors
- `HKCU\Control Panel\Colors\Menu` → `30 33 37` (AVEX surface #1E2125)
- `HKCU\Control Panel\Colors\MenuText` → `236 237 239` (AVEX text #ECEDEF)
- `HKCU\Control Panel\Colors\MenuHilight` → `110 139 255` (AVEX accent #6E8BFF)
- `HKCU\Control Panel\Colors\Scrollbar` → `44 49 55` (AVEX surface variant #2C3137)

### 2. Desktop Wallpaper
- Set to: `C:\Users\Public\AVEXOS\wallpapers\avex-logo-wallpaper.png`
- Previous wallpaper backed up in registry

### 3. Windows Terminal
- **File:** `C:\Users\Nevin Sijo\AppData\Local\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json`
- **Backup:** `settings.json.avex-backup` (same directory)
- **Changes:**
  - Added "AVEX OS" color scheme with 20 color entries
  - Set as default color scheme for all profiles

### 4. Microsoft Edge
- **Registry backup:** `C:\Users\Nevin Sijo\AppData\Local\edge-registry-backup.reg`
- **Changes:**
  - `HKCU\Software\Microsoft\Edge\Preferences\dark_theme` → `1`
  - `HKCU\Software\Microsoft\Edge\VisualPreferences\DarkMode` → `1`

### 5. Microsoft Office
- **Registry backup:** `C:\Users\Nevin Sijo\AppData\Local\office-registry-backup.reg`
- **Changes:**
  - `HKCU\Software\Microsoft\Office\16.0\Common\UI Theme` → `3` (Black theme)
  - `HKCU\Software\Microsoft\Office\16.0\Common\OfficeStartTheme` → `"Dark Gray"`

---

## Rollback Instructions

### Quick Rollback (All Changes)

1. **Import registry backups:**
   ```cmd
   reg import "C:\Users\Nevin Sijo\Documents\windows-theme-backup.reg"
   reg import "C:\Users\Nevin Sijo\AppData\Local\edge-registry-backup.reg"
   reg import "C:\Users\Nevin Sijo\AppData\Local\office-registry-backup.reg"
   ```

2. **Restore Windows Terminal:**
   ```cmd
   copy "C:\Users\Nevin Sijo\AppData\Local\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json.avex-backup" "C:\Users\Nevin Sijo\AppData\Local\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json"
   ```

3. **Restart Explorer:**
   ```cmd
   taskkill /F /IM explorer.exe
   explorer.exe
   ```

4. **Restart applications:**
   - Close and reopen Microsoft Edge
   - Close and reopen all Microsoft Office applications
   - Close and reopen Windows Terminal

### Manual Rollback (Selective)

#### Revert DWM Colorization
```cmd
reg delete "HKCU\Software\Microsoft\Windows\DWM" /v ColorizationColor /f
reg delete "HKCU\Software\Microsoft\Windows\DWM" /v ColorizationAfterglow /f
reg delete "HKCU\Software\Microsoft\Windows\DWM" /v EnableWindowColorization /f
```

#### Revert to Light Theme
```cmd
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" /v AppsUseLightTheme /t REG_DWORD /d 1 /f
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" /v SystemUsesLightTheme /t REG_DWORD /d 1 /f
```

#### Revert Control Panel Colors
```cmd
reg delete "HKCU\Control Panel\Colors" /v Menu /f
reg delete "HKCU\Control Panel\Colors" /v MenuText /f
reg delete "HKCU\Control Panel\Colors" /v MenuHilight /f
reg delete "HKCU\Control Panel\Colors" /v Scrollbar /f
```

#### Revert Office Theme
```cmd
reg add "HKCU\Software\Microsoft\Office\16.0\Common" /v "UI Theme" /t REG_DWORD /d 0 /f
```

#### Revert Edge Theme
```cmd
reg delete "HKCU\Software\Microsoft\Edge\Preferences" /v dark_theme /f
reg delete "HKCU\Software\Microsoft\Edge\VisualPreferences" /v DarkMode /f
```

---

## What Was NOT Changed

### UXTheme Patching (Not Applied)
System dialog theming (file open/save dialogs, properties windows, legacy control panel applets) was **NOT** modified because:
- Requires third-party tool installation (UltraUXThemePatcher or SecureUXTheme)
- Modifies system files (uxtheme.dll, uxtheme.dll.mun)
- High risk of system instability if done incorrectly
- Requires custom .msstyles theme file (not created)
- Requires system restart

The registry-based changes applied cover most visible UI elements. System dialogs will use the default Windows 11 dark theme with AVEX accent colors applied via DWM colorization.

---

## Verification Checklist

After applying the theme, verify:
- [ ] Window borders use AVEX accent color (#6E8BFF)
- [ ] Taskbar is dark (not light)
- [ ] Context menus are dark with AVEX colors
- [ ] Windows Terminal new tabs use AVEX OS color scheme
- [ ] Microsoft Edge uses dark theme
- [ ] Microsoft Office uses Black theme
- [ ] Desktop wallpaper shows AVEX logo
- [ ] File Explorer uses dark theme

---

## Backup Files Created

| File | Location | Purpose |
|------|----------|---------|
| `windows-theme-backup.reg` | `C:\Users\Nevin Sijo\Documents\` | Registry backup before AVEX theme |
| `edge-registry-backup.reg` | `C:\Users\Nevin Sijo\AppData\Local\` | Edge registry keys backup |
| `office-registry-backup.reg` | `C:\Users\Nevin Sijo\AppData\Local\` | Office registry keys backup |
| `settings.json.avex-backup` | Windows Terminal LocalState | Terminal settings backup |

---

## Notes

- **System restore point:** Attempted but failed (requires administrator privileges)
- **Explorer restart:** Required for registry changes to take full effect
- **Application restarts:** Required for Edge, Office, and Terminal to pick up new themes
- **Color format:** Registry uses BGR format (0xBBGGRR), not RGB (0xRRGGBB)
- **AVEX color palette:**
  - Background: #141517
  - Surface: #1E2125
  - Surface variant: #2C3137
  - Accent: #6E8BFF
  - Accent hover: #8099FF
  - Text: #ECEDEF

---

## Support

If you experience issues after applying the theme:
1. Import the registry backups in reverse order (Office → Edge → Windows)
2. Restore Windows Terminal settings
3. Restart Explorer
4. Restart affected applications
5. If problems persist, reboot the system
