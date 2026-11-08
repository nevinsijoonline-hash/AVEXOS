Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$sig = @"
using System;
using System.Runtime.InteropServices;
public static class MC {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  public const uint DOWN = 0x0002, UP = 0x0004;
}
"@
Add-Type -TypeDefinition $sig

$GOKU = 'C:\Users\Nevin Sijo\AppData\Local\Packages\Microsoft.Windows.Photos_8wekyb3d8bbwe\LocalState\PhotosAppBackground\goku-perfected-3840x2160-25454.jpg'
$AVEX = 'C:\Users\Nevin Sijo\AppData\Local\AVEXOS\wallpapers\avex-wallpaper.png'

function RealWallpaper {
  try { (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction Stop).Wallpaper } catch { "<unreadable>" }
}
function Label($p) {
  if (-not $p) { return "<empty>" }
  if ($p -eq $GOKU) { return "ORIGINAL (goku)" }
  if ($p -eq $AVEX) { return "AVEX wallpaper" }
  return "other: $p"
}

$proc = Get-Process AVEXOS.App -ErrorAction SilentlyContinue
if (-not $proc) { Write-Output "APP NOT RUNNING"; exit 1 }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

function FindByName($name, $timeoutMs = 6000) {
  $cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $name)
  $deadline = (Get-Date).AddMilliseconds($timeoutMs)
  while ((Get-Date) -lt $deadline) {
    $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($el) { return $el }
    Start-Sleep -Milliseconds 250
  }
  return $null
}

function Click-ByName($name, $timeoutMs = 6000) {
  $el = FindByName $name $timeoutMs
  if (-not $el) { Write-Output "  !! '$name' NOT FOUND"; return $false }
  $r = $el.Current.BoundingRectangle
  $cx = [int]($r.X + $r.Width / 2); $cy = [int]($r.Y + $r.Height / 2)
  [MC]::SetCursorPos($cx, $cy) | Out-Null
  Start-Sleep -Milliseconds 200
  [MC]::mouse_event([MC]::DOWN, 0,0,0,[IntPtr]::Zero)
  Start-Sleep -Milliseconds 90
  [MC]::mouse_event([MC]::UP, 0,0,0,[IntPtr]::Zero)
  Write-Output "  clicked '$name' at ($cx,$cy)"
  return $true
}

function StatusLine {
  $tc = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Text)
  $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tc)
  $texts = @(); foreach ($t in $all) { $n = $t.Current.Name; if ($n) { $texts += $n } }
  $hit = $texts | Where-Object { $_ -match 'Applied|Restored|Blocked|Error|Declined|Verified|backup' } | Select-Object -Last 1
  if ($hit) { return $hit } else { return "<no status line>" }
}

Write-Output "=== BEFORE: real registry wallpaper = $(Label (RealWallpaper)) ===`n"

Click-ByName "Appearance" | Out-Null
Start-Sleep -Milliseconds 1500

Write-Output "--- A: Restore (should undo the earlier leftover AVEX apply) ---"
Click-ByName "Restore Original" | Out-Null
Start-Sleep -Milliseconds 3500
Write-Output "  app status : $(StatusLine)"
Write-Output "  real value : $(Label (RealWallpaper))"

Write-Output "`n--- B: Apply AVEX (must raise approval dialog) ---"
Click-ByName "Apply AVEX Wallpaper" | Out-Null
Start-Sleep -Milliseconds 2000
$dlg = FindByName "Apply" 5000
if ($dlg) {
  Write-Output "  approval dialog: SHOWN"
  Click-ByName "Apply" | Out-Null
} else { Write-Output "  approval dialog: *** NOT SHOWN ***" }
Start-Sleep -Milliseconds 4000
Write-Output "  app status : $(StatusLine)"
Write-Output "  real value : $(Label (RealWallpaper))"

Write-Output "`n--- C: Restore again (must return to ORIGINAL) ---"
Click-ByName "Restore Original" | Out-Null
Start-Sleep -Milliseconds 4000
Write-Output "  app status : $(StatusLine)"
Write-Output "  real value : $(Label (RealWallpaper))"

Write-Output "`n=== FINAL: real registry wallpaper = $(Label (RealWallpaper)) ==="
