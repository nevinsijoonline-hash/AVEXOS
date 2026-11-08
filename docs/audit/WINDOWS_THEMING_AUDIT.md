# Windows Theming Audit - AVEX OS Custom Theme

**Date:** 2026-11-08  
**Purpose:** Apply AVEX OS theme colors to all Windows products  
**Risk Level:** HIGH - System-level changes requiring admin privileges

---

## Proposed Changes

### 1. Window Colors (Registry)
**Risk:** LOW | **Reversible:** YES | **Requires Restart:** NO

**Changes:**
- `HKCU\Software\Microsoft\Windows\DWM\ColorizationColor` → AVEX accent (#6E8BFF)
- `HKCU\Software\Microsoft\Windows\DWM\ColorizationAfterglow` → AVEX accent hover (#8099FF)
- `HKCU\Software\Microsoft\Windows\DWM\EnableWindowColorization` → 1
- `HKCU\Software\Microsoft\Windows\DWM\ColorPrevalence` → 1

**Impact:** Window borders and title bars will use AVEX colors

---

### 2. File Explorer (Registry)
**Risk:** MODERATE | **Reversible:** YES | **Requires Restart:** NO

**Changes:**
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent` → AVEX accent color
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\EnableTransparency` → 1
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\UseCompactMode` → 1

**Impact:** Explorer accent colors and transparency effects

---

### 3. Taskbar & Start Menu (Registry)
**Risk:** MODERATE | **Reversible:** YES | **Requires Explorer Restart:** YES

**Changes:**
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent\StartColorMenu` → AVEX bg base (#141517)
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent\AccentColorMenu` → AVEX accent (#6E8BFF)
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` → 0 (dark)
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\SystemUsesLightTheme` → 0 (dark)

**Impact:** Taskbar and Start menu will use AVEX dark theme colors

---

### 4. Windows Terminal (JSON Config)
**Risk:** LOW | **Reversible:** YES | **Requires Restart:** NO

**Changes:**
- Modify `%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json`
- Add AVEX OS color scheme:
  ```json
  {
    "name": "AVEX OS",
    "background": "#141517",
    "foreground": "#ECEDEF",
    "cursorColor": "#6E8BFF",
    "selectionBackground": "#6E8BFF40",
    "black": "#141517",
    "red": "#D9534F",
    "green": "#3FA96B",
    "yellow": "#D9A23A",
    "blue": "#6E8BFF",
    "purple": "#8099FF",
    "cyan": "#2C3137",
    "white": "#ECEDEF",
    "brightBlack": "#1E2125",
    "brightRed": "#D9534F",
    "brightGreen": "#3FA96B",
    "brightYellow": "#D9A23A",
    "brightBlue": "#6E8BFF",
    "brightPurple": "#8099FF",
    "brightCyan": "#3A4048",
    "brightWhite": "#A2A9B4"
  }
  ```
- Set as default profile

**Impact:** Terminal will use AVEX OS color scheme

---

### 5. Microsoft Edge (Extension/Policy)
**Risk:** LOW | **Reversible:** YES | **Requires Restart:** NO

**Changes:**
- Create/modify Edge theme extension with AVEX colors
- Or set registry policy for default theme:
  - `HKLM\SOFTWARE\Policies\Microsoft\Edge\Theme` → Custom theme ID

**Impact:** Edge browser will use AVEX theme

---

### 6. Microsoft Office (Registry)
**Risk:** LOW | **Reversible:** YES | **Requires Restart:** YES

**Changes:**
- `HKCU\Software\Microsoft\Office\16.0\Common\OfficeStartTheme` → "Dark Gray" (closest to AVEX dark)
- `HKCU\Software\Microsoft\Office\16.0\Common\UserDialog\Theme` → 4 (Dark)

**Impact:** Office apps will use dark theme (closest available to AVEX)

---

### 7. Action Center & Notifications (Registry)
**Risk:** MODERATE | **Reversible:** YES | **Requires Restart:** NO

**Changes:**
- `HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications\ToastEnabled` → 1
- Accent color inheritance from system settings

**Impact:** Notifications will use system accent color

---

### 8. Lock Screen (Registry)
**Risk:** LOW | **Reversible:** YES | **Requires Restart:** NO

**Changes:**
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Lock Screen\Creative` → AVEX wallpaper path
- Already handled by AVEX OS wallpaper sync feature

**Impact:** Lock screen will show AVEX wallpaper

---

### 9. System Dialogs (UXTheme Patching)
**Risk:** HIGH | **Reversible:** NO (without restore) | **Requires Restart:** YES

**Changes:**
- Requires third-party tool: UltraUXThemePatcher or SecureUXTheme
- Patches uxtheme.dll to allow custom themes
- Install custom AVEX OS theme file (.theme)

**Impact:** All system dialogs will use AVEX theme

**WARNING:** This modifies system files and could break Windows updates or cause instability

---

### 10. Context Menus (Registry)
**Risk:** MODERATE | **Reversible:** YES | **Requires Explorer Restart:** YES

**Changes:**
- Limited customization via registry
- Background color: `HKCU\Control Panel\Colors\Menu` → AVEX surface color
- Text color: `HKCU\Control Panel\Colors\MenuText` → AVEX text primary

**Impact:** Right-click menus will use AVEX colors

---

## Rollback Plan

**Before proceeding, create restore point:**
```powershell
Checkpoint-Computer -Description "Before AVEX Theme" -RestorePointType "MODIFY_SETTINGS"
```

**Registry backup:**
```powershell
reg export HKCU\Software\Microsoft\Windows %TEMP%\windows-theme-backup.reg /y
```

**To rollback:**
1. Restore from restore point
2. Import registry backup
3. Restart Explorer
4. Restart system

---

## Required Privileges

- **Admin rights:** YES (for some registry keys and UXTheme patching)
- **System restart:** YES (for Office and UXTheme changes)
- **Explorer restart:** YES (for taskbar and context menu changes)

---

## Risk Assessment

| Change | Risk | Reversible | Impact |
|--------|------|------------|--------|
| Window colors | LOW | YES | Visual only |
| File Explorer | MODERATE | YES | Visual only |
| Taskbar/Start | MODERATE | YES | Visual + requires restart |
| Windows Terminal | LOW | YES | Visual only |
| Microsoft Edge | LOW | YES | Visual only |
| Microsoft Office | LOW | YES | Visual + requires restart |
| Action Center | MODERATE | YES | Visual only |
| Lock Screen | LOW | YES | Visual only |
| System Dialogs | HIGH | NO* | Visual + system files + requires restart |
| Context Menus | MODERATE | YES | Visual + requires restart |

*UXTheme patching is difficult to reverse without system restore

---

## Recommendation

**SAFE APPROACH (Items 1-8, 10):**
- Apply all changes except UXTheme patching
- Low to moderate risk
- Fully reversible
- Covers most visible UI elements

**FULL APPROACH (All items):**
- Includes UXTheme patching for system dialogs
- High risk, difficult to reverse
- Requires third-party tools
- Could break Windows updates

---

## Approval Required

**I need explicit approval before proceeding with ANY changes.**

Please confirm:
1. Which approach: SAFE (1-8, 10) or FULL (all)?
2. Acknowledge understanding of risks
3. Confirm you have a system restore point or backup
4. Authorize admin privilege usage

**Response format:**
- "APPROVE SAFE" - Apply safe changes only
- "APPROVE FULL" - Apply all changes including UXTheme patching
- "CANCEL" - Do not proceed

---

## Implementation Time Estimate

- SAFE approach: 30-45 minutes
- FULL approach: 60-90 minutes (including UXTheme patching and testing)

---

**Status:** AWAITING APPROVAL
