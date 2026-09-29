<#
.SYNOPSIS
Starts the dev build on a freshly seeded sandbox, in a chosen theme and language.

.DESCRIPTION
Stops any Sidequest already running from this repo's bin folder (never an installed
copy), reseeds the sandbox with the artboards' sample data, writes a settings.json that
fixes the theme, the language and the window position so captures are repeatable, and
launches the build with SIDEQUEST_DATA pointing at the sandbox.

.EXAMPLE
.\tools\run-sandbox.ps1 -Theme Light -Language English
#>
param(
    [ValidateSet('Dark', 'Light')][string]$Theme = 'Dark',
    [ValidateSet('Spanish', 'English')][string]$Language = 'Spanish',
    # Start with no Sidequests at all, for the empty states.
    [switch]$Empty,
    [string]$Sandbox = (Join-Path $env:TEMP 'sq-sandbox')
)

$repo = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $repo 'bin'
$exe = Join-Path $repo 'bin\Debug\net8.0-windows\Sidequest.exe'

# The whole point is to stay away from live data.
$appData = [Environment]::GetFolderPath('ApplicationData')
if ([IO.Path]::GetFullPath($Sandbox).StartsWith($appData, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing a sandbox inside $appData - that is where the real data lives."
}
if (-not (Test-Path $exe)) { throw "No build at $exe - run dotnet build first." }

Get-Process Sidequest -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($bin, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object { $_.Kill(); $_.WaitForExit(5000) | Out-Null }

if (Test-Path $Sandbox) { Remove-Item -Recurse -Force $Sandbox }
$data = Join-Path $Sandbox 'Sidequest'
New-Item -ItemType Directory -Force -Path $data | Out-Null

if ($Empty) { Set-Content -LiteralPath (Join-Path $data 'sidequests.json') -Value '[]' }
else { & (Join-Path $PSScriptRoot 'seed-sandbox.ps1') -Sandbox $Sandbox | Out-Null }

# A fixed spot on the primary screen, so one capture lines up with the next.
[ordered]@{
    Theme = $Theme; Language = $Language; KeepOnTop = $true; Collapsed = $false
    MainLeft = 200; MainTop = 100
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $data 'settings.json') -Encoding UTF8

$env:SIDEQUEST_DATA = $Sandbox
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 2500
if ($p.HasExited) { throw "Sidequest exited at once (code $($p.ExitCode))" }
Write-Output "Sidequest $($p.Id) on $Sandbox ($Theme, $Language)"
