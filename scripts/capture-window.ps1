Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$sig = @"
using System;
using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
  public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
Add-Type -TypeDefinition $sig

# Enumerate all windows owned by the AVEXOS process (main-window API misses WinUI child surfaces)
$targets = @()
$cb = {
  param($h, $l)
  $pid2 = 0
  [W]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
  if ($pid2 -eq $script:WantPid) {
    $sb = New-Object System.Text.StringBuilder 512
    [W]::GetClassName($h, $sb, 512) | Out-Null
    $cls = $sb.ToString()
    $tb = New-Object System.Text.StringBuilder 512
    [W]::GetWindowText($h, $tb, 512) | Out-Null
    $title = $tb.ToString()
    $vis = [W]::IsWindowVisible($h)
    $r = New-Object W+RECT
    [W]::GetWindowRect($h, [ref]$r) | Out-Null
    $script:Targets += [pscustomobject]@{ H=$h; Class=$cls; Title=$title; Visible=$vis; L=$r.L; T=$r.T; R=$r.R; B=$r.B }
  }
  return $true
}
$proc = Get-Process AVEXOS.App -ErrorAction SilentlyContinue
if (-not $proc) { Write-Output "PROCESS NOT RUNNING"; exit }
$script:WantPid = $proc.Id
$script:Targets = @()
[W]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null

Write-Output "=== Windows owned by PID $($proc.Id) ==="
$script:Targets | ForEach-Object {
  $w = $_.R - $_.L; $hgt = $_.B - $_.T
  Write-Output ("HWND={0} class='{1}' title='{2}' visible={3} size={4}x{5}" -f $_.H, $_.Class, $_.Title, $_.Visible, $w, $hgt)
}

# Capture the largest visible window with PrintWindow (PW_RENDERFULLCONTENT=2)
$cap = $script:Targets | Where-Object { $_.Visible -and ($_.R - $_.L) -gt 100 } |
       Sort-Object { ($_.R - $_.L) * ($_.B - $_.T) } -Descending | Select-Object -First 1
if (-not $cap) { Write-Output "NO CAPTURABLE WINDOW"; exit }

$w = $cap.R - $cap.L; $hgt = $cap.B - $cap.T
Write-Output "`n=== Capturing HWND $($cap.H) ($w x $hgt) ==="

# Retry: WinUI surfaces sometimes need a second render pass
for ($attempt = 1; $attempt -le 3; $attempt++) {
  $bmp = New-Object System.Drawing.Bitmap $w, $hgt
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  $ok = [W]::PrintWindow($cap.H, $hdc, 2)
  $g.ReleaseHdc($hdc)
  $g.Dispose()

  # Measure non-uniformity to prove content actually rendered
  $black = 0; $white = 0; $other = 0; $total = 0
  for ($y = 0; $y -lt $hgt; $y += 8) {
    for ($x = 0; $x -lt $w; $x += 8) {
      $p = $bmp.GetPixel($x, $y)
      $total++
      if ($p.R -lt 16 -and $p.G -lt 16 -and $p.B -lt 16) { $black++ }
      elseif ($p.R -gt 240 -and $p.G -gt 240 -and $p.B -gt 240) { $white++ }
      else { $other++ }
    }
  }
  Write-Output ("attempt {0}: PrintWindow={1} sampled={2} black={3} white={4} other={5}" -f $attempt, $ok, $total, $black, $white, $other)
  $out = "C:\Users\Nevin Sijo\Documents\Qoder\2026-11-07\9f0855d1\cap-$attempt.png"
  $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Output "saved: $out"
  if ($other -gt ($total * 0.05)) { break }
}
