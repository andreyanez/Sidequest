<#
.SYNOPSIS
Draws Sidequest.ico, the exe's icon, from the tray's mark.

.DESCRIPTION
Windows reads an exe's icon from the file before the app runs, so it has to exist when
the project builds. This builds the app and has it draw the icon with the code that
draws the tray icon (Sidequest.exe --write-icon), next to Sidequest.csproj. The colour is
the tray's on this machine: light on a dark taskbar, dark on a light one. Run it after
changing the mark, then build again so the exe picks it up.

.EXAMPLE
.\tools\make-icon.ps1
#>
$repo = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $repo 'bin\Debug\net8.0-windows\Sidequest.exe'
$out = Join-Path $repo 'Sidequest.ico'

dotnet build (Join-Path $repo 'Sidequest.csproj') --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

# The switch reads no data, but no dev run goes without a sandbox.
$env:SIDEQUEST_DATA = Join-Path $env:TEMP 'sq-icon'
if (Test-Path $out) { Remove-Item $out }
$p = Start-Process -FilePath $exe -ArgumentList '--write-icon', "`"$out`"" -PassThru -Wait
if (-not (Test-Path $out)) { throw "No icon written (exit code $($p.ExitCode))" }
Write-Output "wrote $out"
