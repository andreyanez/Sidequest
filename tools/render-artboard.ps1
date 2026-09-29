<#
.SYNOPSIS
Renders an artboard to a PNG at 1:1, for comparing against a capture of the app.

.DESCRIPTION
Uses headless Edge with a throwaway profile under %TEMP%, so the user's own browser
profile is never touched. The artboards load DM Sans from Google Fonts, so this needs the
network; the virtual time budget gives the font time to arrive before the shot is taken.

.EXAMPLE
.\tools\render-artboard.ps1 -Name FnSidequests -OutPath $env:TEMP\art-FnSidequests.png
#>
param(
    [Parameter(Mandatory = $true)][string]$Name,     # e.g. FnSidequests
    [Parameter(Mandatory = $true)][string]$OutPath,
    [int]$Width = 400,
    [int]$Height = 720
)

$edge = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
if (-not (Test-Path $edge)) { throw "Edge not found at $edge" }

$file = Join-Path (Split-Path $PSScriptRoot -Parent) "docs\design\reference\$Name.html"
if (-not (Test-Path $file)) { throw "No artboard $file" }

$profile = Join-Path $env:TEMP 'sidequest-artboard-edge'
New-Item -ItemType Directory -Force -Path $profile | Out-Null
$OutPath = [IO.Path]::GetFullPath($OutPath)

$arguments = @(
    '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
    '--force-device-scale-factor=1',
    "--user-data-dir=$profile",
    "--window-size=$Width,$Height",
    '--virtual-time-budget=8000',
    "--screenshot=$OutPath",
    ([Uri]$file).AbsoluteUri
)
$p = Start-Process -FilePath $edge -ArgumentList $arguments -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit(30000)) { $p.Kill(); throw "Edge timed out rendering $Name" }
if (-not (Test-Path $OutPath)) { throw "No screenshot written for $Name" }
Write-Output "rendered $Name -> $OutPath"
