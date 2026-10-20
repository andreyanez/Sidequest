<#
.SYNOPSIS
Starts the dev build on a freshly seeded sandbox, in a chosen theme and language.

.DESCRIPTION
Stops the copy it started last time (never an installed copy, nor one started some other
way), reseeds the sandbox with the artboards' sample data, writes a settings.json that
fixes the theme, the language and the window position so captures are repeatable, and
launches the build with SIDEQUEST_DATA pointing at the sandbox.

.EXAMPLE
.\tools\run-sandbox.ps1 -Theme Light -Language English
.\tools\run-sandbox.ps1 -Exe .\publish\Sidequest.exe     # a Release build, before installing it
.\tools\run-sandbox.ps1 -Stop                             # frees the build for dotnet build
#>
param(
    # Only stop the copy started last time, and launch nothing.
    [switch]$Stop,
    [ValidateSet('Dark', 'Light')][string]$Theme = 'Dark',
    [ValidateSet('Spanish', 'English')][string]$Language = 'Spanish',
    # Start with no Sidequests at all, for the empty states.
    [switch]$Empty,
    # FnSidequestsCompacto's fourteen Sidequests, for the long list (5.4).
    [switch]$Many,
    # FnMainOverflowB's panel, with its long titles (5.1).
    [switch]$Overflow,
    # Start on the collapsed one-row panel (5.2).
    [switch]$Collapsed,
    # Start where a fresh install does, in the work area's bottom-right corner, rather than
    # at the fixed spot captures use.
    [switch]$Corner,
    # The card's height (Settings.MainHeight); 0 leaves the app's default, 640.
    [double]$Height = 0,
    # Passed to the app, e.g. '--settings' or '--sidequests' to open a screen directly.
    [string[]]$AppArgs = @(),
    [string]$Sandbox = (Join-Path $env:TEMP 'sq-sandbox'),
    # The build to run. Must be inside this repo, so an installed copy is never started here.
    [string]$Exe = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Debug\net8.0-windows\Sidequest.exe')
)

$repo = Split-Path $PSScriptRoot -Parent
$exe = [IO.Path]::GetFullPath($Exe)
if (-not $exe.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing $exe - only a build inside $repo is run against a sandbox."
}

# The whole point is to stay away from live data.
$appData = [Environment]::GetFolderPath('ApplicationData')
$Sandbox = [IO.Path]::GetFullPath($Sandbox)
if ($Sandbox.StartsWith($appData, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing a sandbox inside $appData - that is where the real data lives."
}
# It is emptied below, so it must be a folder of its own, never a drive or a user folder.
if ($Sandbox.TrimEnd('\').Split('\').Count -lt 4) {
    throw "Refusing $Sandbox as a sandbox - it is emptied on every run, so it must sit deeper."
}
if (-not (Test-Path $exe)) { throw "No build at $exe - run dotnet build first." }

# Stop the copy this script started last time, and only that one: a copy of the same build
# can be running on someone's trial data, and the app is single-instance, so launching
# beside it would only bring that copy to the front.
$pidFile = Join-Path $Sandbox 'run-sandbox.pid'
$ours = if (Test-Path $pidFile) { (Get-Content $pidFile -Raw).Trim() } else { '' }
foreach ($p in Get-Process Sidequest -ErrorAction SilentlyContinue) {
    if (-not $p.Path -or -not $p.Path.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase)) { continue }
    if ("$($p.Id) $($p.StartTime.Ticks)" -eq $ours) { $p.Kill(); $p.WaitForExit(5000) | Out-Null; continue }
    throw ("Sidequest $($p.Id), started {0:HH:mm}, was not started by this script - " -f $p.StartTime) +
          "most likely a trial run on other data. Quit it from its tray menu first."
}
if ($Stop) { return }

if (Test-Path $Sandbox) { Remove-Item -Recurse -Force $Sandbox }
$data = Join-Path $Sandbox 'Sidequest'
New-Item -ItemType Directory -Force -Path $data | Out-Null

if ($Empty) { Set-Content -LiteralPath (Join-Path $data 'sidequests.json') -Value '[]' }
else { & (Join-Path $PSScriptRoot 'seed-sandbox.ps1') -Sandbox $Sandbox -Many:$Many -Overflow:$Overflow | Out-Null }

# A fixed spot on the primary screen, so one capture lines up with the next.
$settings = [ordered]@{
    Theme = $Theme; Language = $Language; KeepOnTop = $true; Collapsed = [bool]$Collapsed
}
if (-not $Corner) { $settings.MainLeft = 200; $settings.MainTop = 100 }
if ($Height -gt 0) { $settings.MainHeight = $Height }
$settings | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $data 'settings.json') -Encoding UTF8

$env:SIDEQUEST_DATA = $Sandbox
$p = if ($AppArgs.Count -gt 0) { Start-Process -FilePath $exe -ArgumentList $AppArgs -PassThru }
     else { Start-Process -FilePath $exe -PassThru }
Set-Content -LiteralPath $pidFile -Value "$($p.Id) $($p.StartTime.Ticks)"
Start-Sleep -Milliseconds 2500
if ($p.HasExited) { throw "Sidequest exited at once (code $($p.ExitCode))" }
Write-Output "Sidequest $($p.Id) on $Sandbox ($Theme, $Language)"
