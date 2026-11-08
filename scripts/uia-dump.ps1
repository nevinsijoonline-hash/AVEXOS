Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process AVEXOS.App -ErrorAction SilentlyContinue
if (-not $proc) { Write-Output "APP NOT RUNNING"; exit 1 }
Write-Output "PID=$($proc.Id)"

$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
if ($null -eq $root) { Write-Output "UIA: could not bind to window"; exit 1 }
Write-Output "UIA bound: name='$($root.Current.Name)' control=$($root.Current.ControlType.ProgrammaticName)"

function Dump($el, $depth, $max) {
  if ($depth -gt $max) { return }
  $c = $el.Current
  $indent = ' ' * ($depth * 2)
  Write-Output ("{0}[{1}] '{2}' rect=({3},{4},{5},{6})" -f $indent, $c.ControlType.ProgrammaticName -replace 'ControlType\.','', $c.Name, [int]$c.BoundingRectangle.X, [int]$c.BoundingRectangle.Y, [int]$c.BoundingRectangle.Width, [int]$c.BoundingRectangle.Height)
  $walk = $el.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($ch in $walk) { Dump $ch ($depth+1) $max }
}

Dump $root 0 3

Write-Output "`n=== Buttons / clickable items found ==="
$btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,
     [System.Windows.Automation.ControlType]::Button)))
foreach ($b in $btns) {
  Write-Output ("BUTTON '{0}' rect=({1},{2},{3},{4})" -f $b.Current.Name,
    [int]$b.Current.BoundingRectangle.X, [int]$b.Current.BoundingRectangle.Y,
    [int]$b.Current.BoundingRectangle.Width, [int]$b.Current.BoundingRectangle.Height)
}
