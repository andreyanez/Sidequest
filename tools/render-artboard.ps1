<#
.SYNOPSIS
Renders an artboard to a PNG at 1:1, for comparing against a capture of the app.

.DESCRIPTION
Uses headless Edge with a throwaway profile under %TEMP%, so the user's own browser
profile is never touched. The artboards load DM Sans from Google Fonts, so this needs the
network; the virtual time budget gives the font time to arrive before the shot is taken.

Only FnMain has a light artboard (FnMainClaro). -Light makes one for any other screen by
swapping 4.2's dark values for its light ones in a copy under %TEMP%, as FnMainClaro does
for FnMain. It refuses an artboard with a colour the table doesn't cover, rather than
leave it dark.

.EXAMPLE
.\tools\render-artboard.ps1 -Name FnSidequests -OutPath $env:TEMP\art-FnSidequests.png
.\tools\render-artboard.ps1 -Name FnSidequests -OutPath $env:TEMP\art-FnSidequests-light.png -Light
#>
param(
    [Parameter(Mandatory = $true)][string]$Name,     # e.g. FnSidequests
    [Parameter(Mandatory = $true)][string]$OutPath,
    [int]$Width = 400,
    [int]$Height = 720,
    [switch]$Light
)

$edge = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
if (-not (Test-Path $edge)) { throw "Edge not found at $edge" }

$file = Join-Path (Split-Path $PSScriptRoot -Parent) "docs\design\reference\$Name.html"
if (-not (Test-Path $file)) { throw "No artboard $file" }

$OutPath = [IO.Path]::GetFullPath($OutPath)
if (Test-Path $OutPath) { Remove-Item $OutPath }

if ($Light) {
    # 4.2, dark -> light: the neutral tokens, then the eight Sidequest themes.
    $tokens = @{
        '#1d1f23' = '#ffffff'; '#202328' = '#ecece8'; '#2a2d33' = '#e2e2de'; '#3a3e46' = '#c4c4be'
        '#e8e9ec' = '#17181a'; '#9a9ea8' = '#5b5f68'; '#111214' = '#efefec'
        '#d4a24a' = '#9a6b1f'; '#4fb5a8' = '#1f6f6b'; '#8b93e6' = '#4a4f9c'; '#e0795f' = '#a1452f'
        '#a3b357' = '#5f6b2a'; '#c47fb5' = '#7a3f6e'; '#6aa5d8' = '#3d6285'; '#a0a4ad' = '#55554f'
        # Not a token: the stripes FnObjetivo draws where its sample images would be.
        '#24272c' = '#f0f0ed'
    }
    $html = Get-Content -LiteralPath $file -Raw -Encoding UTF8

    # Bg and Inv are one colour in dark. Inv is text, and the switch's knob; the rest is Bg.
    $html = [regex]::Replace($html, '(translateX\(16px\);\s*background:\s*)#16171a', '${1}#ffffff', 'IgnoreCase')
    $html = [regex]::Replace($html, '((?<![-\w])color:\s*)#16171a', '${1}#ffffff', 'IgnoreCase')
    $html = [regex]::Replace($html, '#16171a', '#f6f6f4', 'IgnoreCase')

    $html = [regex]::Replace($html, '#[0-9a-fA-F]{6}\b', {
        param($m)
        $light = $tokens[$m.Value.ToLower()]
        if ($light) { $light } else { $m.Value }
    })

    # The tints are rgba() of a theme colour: the same alpha over its light value.
    $html = [regex]::Replace($html, 'rgba\(\s*(\d+),\s*(\d+),\s*(\d+),\s*([\d.]+)\s*\)', {
        param($m)
        $dark = '#{0:x2}{1:x2}{2:x2}' -f [int]$m.Groups[1].Value, [int]$m.Groups[2].Value, [int]$m.Groups[3].Value
        $light = $tokens[$dark]
        if (-not $light) { return $m.Value }
        $r, $g, $b = 1, 3, 5 | ForEach-Object { [Convert]::ToInt32($light.Substring($_, 2), 16) }
        'rgba({0}, {1}, {2}, {3})' -f $r, $g, $b, $m.Groups[4].Value
    })

    $known = @($tokens.Values) + '#f6f6f4', '#ffffff'
    $left = @([regex]::Matches($html, '#[0-9a-fA-F]{6}\b') | ForEach-Object { $_.Value.ToLower() } |
        Where-Object { $_ -notin $known } | Sort-Object -Unique)
    $left += @([regex]::Matches($html, 'rgba\([^)]*\)') | ForEach-Object { $_.Value } | Where-Object {
        $rgb = [regex]::Match($_, '(\d+),\s*(\d+),\s*(\d+)').Groups
        ('#{0:x2}{1:x2}{2:x2}' -f [int]$rgb[1].Value, [int]$rgb[2].Value, [int]$rgb[3].Value) -notin $known
    })
    if ($left.Count -gt 0) { throw "No light value for $($left -join ', ') in $Name - add it to the table." }

    $file = Join-Path $env:TEMP "sidequest-artboard-$Name-light.html"
    Set-Content -LiteralPath $file -Value $html -Encoding UTF8 -NoNewline
}

# The msedge.exe started here can hand the work to another browser process and exit
# at once, so its exit says nothing about the shot: wait for the file itself, and only
# then clear the profile away, once the processes using it are gone. (Deleting it while
# they still ran is what used to lose shots.)
function EdgeUsing($profileName) {
    Get-CimInstance Win32_Process -Filter "Name = 'msedge.exe'" |
        Where-Object { $_.CommandLine -like "*$profileName*" }
}

for ($attempt = 1; $attempt -le 2 -and -not (Test-Path $OutPath); $attempt++) {
    $profileName = "sidequest-artboard-edge-" + [guid]::NewGuid().ToString('N')
    $profile = Join-Path $env:TEMP $profileName
    $arguments = @(
        '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
        '--force-device-scale-factor=1',
        "--user-data-dir=$profile",
        "--window-size=$Width,$Height",
        '--virtual-time-budget=8000',
        "--screenshot=$OutPath",
        ([Uri]$file).AbsoluteUri
    )
    Start-Process -FilePath $edge -ArgumentList $arguments -WindowStyle Hidden | Out-Null

    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline -and -not (Test-Path $OutPath)) { Start-Sleep -Milliseconds 250 }
    while ((Get-Date) -lt $deadline -and (EdgeUsing $profileName)) { Start-Sleep -Milliseconds 250 }
    Remove-Item -Recurse -Force $profile -ErrorAction SilentlyContinue
}

if (-not (Test-Path $OutPath)) { throw "No screenshot written for $Name after 2 attempts" }
Write-Output "rendered $Name -> $OutPath"
