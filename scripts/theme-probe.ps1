param([switch]$LeaveRunning)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

$sig = @"
using System;
using System.Runtime.InteropServices;
public static class T {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
  public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
Add-Type -TypeDefinition $sig

$proc = Get-Process AVEXOS.App -ErrorAction SilentlyContinue
if (-not $proc) { Write-Output "APP NOT RUNNING"; exit 1 }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

function NameCond($v) {
  New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $v)
}
function TypeCond($t) {
  New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $t)
}
function Find-Name($n, $timeoutMs = 6000) {
  $d = (Get-Date).AddMilliseconds($timeoutMs)
  while ((Get-Date) -lt $d) {
    $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (NameCond $n))
    if ($el) { return $el }
    Start-Sleep -Milliseconds 200
  }
  return $null
}
function Click-Rect($el, $what) {
  if (-not $el) { Write-Output "  !! $what NOT FOUND"; return $false }
  $r = $el.Current.BoundingRectangle
  if ($r.Width -le 0) { Write-Output "  !! $what has empty rect"; return $false }
  $x = [int]($r.X + $r.Width / 2); $y = [int]($r.Y + $r.Height / 2)
  [T]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds 150
  [T]::mouse_event(2, 0,0,0,[IntPtr]::Zero)
  Start-Sleep -Milliseconds 60
  [T]::mouse_event(4, 0,0,0,[IntPtr]::Zero)
  Write-Output "  clicked $what at ($x,$y)"
  return $true
}

# Mean luminance of the rendered window, from a real PrintWindow capture (PW_RENDERFULLCONTENT).
function Get-Luminance {
  $script:Wins = @()
  $cb = {
    param($h, $l)
    $p2 = 0
    [T]::GetWindowThreadProcessId($h, [ref]$p2) | Out-Null
    if ($p2 -eq $script:WantPid -and [T]::IsWindowVisible($h)) {
      $r = New-Object T+RECT
      [T]::GetWindowRect($h, [ref]$r) | Out-Null
      if (($r.R - $r.L) -gt 300) { $script:Wins += [pscustomobject]@{ H=$h; L=$r.L; T=$r.T; R=$r.R; B=$r.B } }
    }
    return $true
  }
  $script:WantPid = $proc.Id
  [T]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
  $cap = $script:Wins | Sort-Object { ($_.R - $_.L) * ($_.B - $_.T) } -Descending | Select-Object -First 1
  if (-not $cap) { return $null }

  $w = $cap.R - $cap.L; $hgt = $cap.B - $cap.T
  $bmp = New-Object System.Drawing.Bitmap $w, $hgt
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [T]::PrintWindow($cap.H, $hdc, 2) | Out-Null
  $g.ReleaseHdc($hdc); $g.Dispose()

  $sum = 0.0; $n = 0
  for ($y = 120; $y -lt $hgt - 20; $y += 5) {
    for ($x = 600; $x -lt $w - 20; $x += 5) {   # content area, right of the navigation pane
      $p = $bmp.GetPixel($x, $y)
      $sum += 0.2126 * $p.R + 0.7152 * $p.G + 0.0722 * $p.B
      $n++
    }
  }
  $path = Join-Path $env:TEMP ("avex-theme-{0}.png" -f (Get-Random))
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  if ($n -eq 0) { return $null }
  return [pscustomobject]@{ Lum = [math]::Round($sum / $n, 1); Samples = $n; Shot = $path }
}

Write-Output "=== Theme apply probe (PID $($proc.Id)) ==="
Write-Output "Q: does the row you highlight decide what gets applied?"

function All-ListItems {
  $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (TypeCond ([System.Windows.Automation.ControlType]::ListItem)))
}

# The nav pane and the theme list both expose ListItem; tell them apart by Name.
$themesNav = All-ListItems | Where-Object { $_.Current.Name -eq 'Themes' } | Select-Object -First 1
if (-not $themesNav) { Write-Output "NO 'Themes' NAV ITEM"; exit 1 }
$np = $themesNav.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
$np.Select()
Write-Output "  'Themes' nav selected via SelectionItemPattern"
Start-Sleep -Milliseconds 2000

function Invoke-Button($name) {
  # Diagnostics must go to the host, not the pipeline: this function's return value
  # is consumed by an `if`, and Write-Output would be swallowed into it.
  $el = Find-Name $name
  if (-not $el) { Write-Host "  !! button '$name' NOT FOUND"; return $false }
  $ip = $null
  $ok = $el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$ip)
  if ($ok -and $ip) {
    $ip.Invoke()
    Write-Host "  invoked '$name' via InvokePattern"
    return $true
  }
  Write-Host "  '$name' has no InvokePattern; falling back to a real click"
  $r = $el.Current.BoundingRectangle
  $x = [int]($r.X + $r.Width / 2); $y = [int]($r.Y + $r.Height / 2)
  [T]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds 150
  [T]::mouse_event(2, 0,0,0,[IntPtr]::Zero)
  Start-Sleep -Milliseconds 60
  [T]::mouse_event(4, 0,0,0,[IntPtr]::Zero)
  Write-Host "  clicked '$name' at ($x,$y)"
  return $true
}

$rows = @(All-ListItems | Where-Object { $_.Current.Name -like '*ThemeDocument*' })
Write-Output "theme rows: $($rows.Count)"
if ($rows.Count -lt 2) { Write-Output "EXPECTED 2 THEME ROWS"; exit 1 }

$results = @()
foreach ($i in 0..($rows.Count - 1)) {
  $row = $rows[$i]
  $sel = $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
  $sel.Select()
  Write-Output "`n--- row $i selected (IsSelected=$($sel.Current.IsSelected)) ---"
  Start-Sleep -Milliseconds 400
  if (-not (Invoke-Button 'Apply to Control Center')) { Write-Output "  !! apply never triggered"; continue }
  Start-Sleep -Milliseconds 1800
  $l2 = Get-Luminance
  # Calibrated against this app's own tokens: avex.dark bg #141517 renders ~14,
  # avex.light #F4F5F7 renders ~102 over the sampled region (mixed text + cards).
  $v = if ($l2.Lum -gt 60) { 'LIGHT' } else { 'DARK' }
  Write-Output ("  content luminance = {0} over {1} samples  => {2}   [{3}]" -f $l2.Lum, $l2.Samples, $v, $l2.Shot)
  $results += [pscustomobject]@{ Row = $i; Lum = $l2.Lum; Verdict = $v }
}

Write-Output "`n=== summary ==="
$results | ForEach-Object { "  row $($_.Row) -> $($_.Verdict) (luminance $($_.Lum))" }
$spread = ($results.Lum | Measure-Object -Maximum).Maximum - ($results.Lum | Measure-Object -Minimum).Minimum
Write-Output "  luminance spread = $([math]::Round($spread,1))"
if (($results.Verdict | Select-Object -Unique).Count -gt 1) {
  Write-Output "RESULT: each row renders its own theme -- selection drives apply."
} else {
  Write-Output "RESULT: every row rendered the same theme -- selection is ignored, or apply never ran."
}
