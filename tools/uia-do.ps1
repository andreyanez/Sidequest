<#
.SYNOPSIS
Acts on one control of the running dev build, found by its accessible name.

.DESCRIPTION
Drives the app the way a screen reader or other assistive tool would, which also proves
the control has a name. Actions: invoke (buttons), toggle, select (list items), setvalue
(text fields), focus, exists. Several controls can share a name - a button and the text
inside it, or a list item and the button it holds - so narrow with -Type and -Index.

Only attaches to a Sidequest started from this repo's bin folder.

.EXAMPLE
.\tools\uia-do.ps1 -Name 'Sidequests'
.\tools\uia-do.ps1 -Name 'Enviar objetivo' -Type Button -Index 1 -Action focus
.\tools\uia-do.ps1 -Name 'Nombre' -Type Edit -Action setvalue -Value 'Renovar pasaporte'
#>
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [ValidateSet('invoke', 'toggle', 'select', 'setvalue', 'focus', 'exists')][string]$Action = 'invoke',
    [string]$Type = '',
    [int]$Index = 0,
    [string]$Value = '',
    [string]$BinDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin')
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]

$proc = Get-Process Sidequest -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($BinDir, [StringComparison]::OrdinalIgnoreCase) } |
    Select-Object -First 1
if (-not $proc) { Write-Output "No Sidequest running from $BinDir"; exit 1 }

$byProcess = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $proc.Id)
$byName = New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $Name)
$found = @($A::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.AndCondition($byProcess, $byName))))
if ($Type) { $found = @($found | Where-Object { $_.Current.ControlType.ProgrammaticName -eq "ControlType.$Type" }) }

if ($found.Count -le $Index) { Write-Output "NOT FOUND: '$Name' $Type (index $Index of $($found.Count))"; exit 2 }
$el = $found[$Index]
$kind = $el.Current.ControlType.ProgrammaticName.Replace('ControlType.', '')

switch ($Action) {
    'exists'   { Write-Output "found $kind '$Name' ($($found.Count) matches)" }
    'focus'    { $el.SetFocus(); Write-Output "focused $kind '$Name'" }
    'toggle'   { $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Write-Output "toggled $kind '$Name'" }
    'select'   { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Write-Output "selected $kind '$Name'" }
    'setvalue' { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value); Write-Output "set $kind '$Name' = '$Value'" }
    default    { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Output "invoked $kind '$Name'" }
}
Start-Sleep -Milliseconds 700
