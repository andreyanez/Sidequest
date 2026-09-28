<#
.SYNOPSIS
Dumps the running app's accessibility tree, marking what is keyboard-focusable.

.DESCRIPTION
Shows what a screen reader would announce. Two things this catches that a screenshot
cannot: controls with no accessible name (icon buttons whose label is only a ToolTip),
and list rows that fall back to their type name because the row class has no ToString.

Run it against a sandboxed instance (see tools\seed-sandbox.ps1).

.EXAMPLE
.\tools\uia-tree.ps1
#>
param(
    [string]$BinDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin')
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$proc = Get-Process Sidequest -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($BinDir, [StringComparison]::OrdinalIgnoreCase) } |
    Select-Object -First 1
if (-not $proc) { Write-Output "No Sidequest running from $BinDir"; exit 1 }

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { Write-Output "window not found"; exit 1 }

$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker

function Walk($e, $depth) {
    if ($depth -gt 8) { return }
    $c = $e.Current
    $type = $c.ControlType.ProgrammaticName.Replace('ControlType.', '')
    $name = $c.Name
    $flag = ''
    if ([string]::IsNullOrWhiteSpace($name)) {
        $name = ''
        # An interactive control with no name is a defect, not a style choice.
        if ($c.IsKeyboardFocusable) { $flag = '  <-- focusable with no name' }
    }
    $foc = if ($c.IsKeyboardFocusable) { 'FOCUSABLE' } else { '         ' }
    $off = if ($c.IsOffscreen) { ' (offscreen)' } else { '' }
    Write-Output ("{0} {1}{2,-12} {3}{4}{5}" -f $foc, ('  ' * $depth), $type, $name, $off, $flag)

    $child = $walker.GetFirstChild($e)
    while ($child) { Walk $child ($depth + 1); $child = $walker.GetNextSibling($child) }
}

Walk $win 0
