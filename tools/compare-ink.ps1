<#
.SYNOPSIS
Compares where things are drawn in an artboard render and an app capture.

.DESCRIPTION
For each region, finds the bounding box of "ink" - pixels that differ from the region's
most common colour - in both images, so an element's position and size can be compared
to the pixel. Regions are given in the artboard's own coordinates; the app capture is
read at an offset, since its card sits inside the window's 12px shadow margin.

For text, a bounding box misleads: WPF hints small glyphs about 1px taller than Chrome
draws them. -Profile prints ink per row instead, where the heaviest bottom row is the
baseline. See "Checking a screen" in docs/dev-notes.md.

.EXAMPLE
.\tools\compare-ink.ps1 -Art art.png -App app.png -Rects 'button:4,103,396,175'
.\tools\compare-ink.ps1 -Art art.png -App app.png -Rects 'count:340,120,370,140' -Profile
#>
param(
    [Parameter(Mandatory = $true)][string]$Art,
    [Parameter(Mandatory = $true)][string]$App,
    # "label:x0,y0,x1,y1", inclusive, in artboard coordinates
    [Parameter(Mandatory = $true)][string[]]$Rects,
    [int]$AppOffsetX = 12,
    [int]$AppOffsetY = 12,
    [int]$Threshold = 40,
    [switch]$Profile
)

Add-Type -AssemblyName System.Drawing
if (-not ('SidequestInk' -as [type])) {
    $refs = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Color].Assembly.Location,
              'System.Collections', 'System.Runtime', 'System.ComponentModel.Primitives')
    Add-Type -ReferencedAssemblies $refs -TypeDefinition @"
using System; using System.Drawing; using System.Collections.Generic; using System.Text;
public static class SidequestInk {
  static int Background(Bitmap b, int ox, int oy, int x0, int y0, int x1, int y1) {
    var counts = new Dictionary<int,int>();
    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) {
      int c = b.GetPixel(x + ox, y + oy).ToArgb();
      counts[c] = counts.TryGetValue(c, out var n) ? n + 1 : 1;
    }
    int bg = 0, best = -1;
    foreach (var kv in counts) if (kv.Value > best) { best = kv.Value; bg = kv.Key; }
    return bg;
  }
  static int Distance(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
  public static string Box(Bitmap b, int ox, int oy, int x0, int y0, int x1, int y1, int thr) {
    var B = Color.FromArgb(Background(b, ox, oy, x0, y0, x1, y1));
    int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, n = 0;
    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) {
      if (Distance(b.GetPixel(x + ox, y + oy), B) <= thr) continue;
      n++; minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
    }
    if (maxX < 0) return String.Format("no ink (bg #{0:X6})", B.ToArgb() & 0xFFFFFF);
    return String.Format("x {0}-{1} y {2}-{3} ({4}x{5}, {6} px) bg #{7:X6}",
      minX, maxX, minY, maxY, maxX - minX + 1, maxY - minY + 1, n, B.ToArgb() & 0xFFFFFF);
  }
  public static string Rows(Bitmap b, int ox, int oy, int x0, int y0, int x1, int y1) {
    var B = Color.FromArgb(Background(b, ox, oy, x0, y0, x1, y1));
    var sb = new StringBuilder();
    for (int y = y0; y <= y1; y++) {
      int sum = 0;
      for (int x = x0; x <= x1; x++) sum += Distance(b.GetPixel(x + ox, y + oy), B);
      if (sum > 0) sb.AppendFormat("{0}:{1} ", y, sum / 10);
    }
    return sb.ToString().TrimEnd();
  }
}
"@
}

$a = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Art))
$b = [System.Drawing.Bitmap]::FromFile((Resolve-Path $App))
try {
    foreach ($r in $Rects) {
        if ($r -notmatch '^([^:]+):(\d+),(\d+),(\d+),(\d+)$') { Write-Output "bad region '$r'"; continue }
        $label = $Matches[1]
        $x0 = [int]$Matches[2]; $y0 = [int]$Matches[3]; $x1 = [int]$Matches[4]; $y1 = [int]$Matches[5]
        if ($Profile) {
            Write-Output ("{0,-16} art {1}" -f $label, [SidequestInk]::Rows($a, 0, 0, $x0, $y0, $x1, $y1))
            Write-Output ("{0,-16} app {1}" -f '', [SidequestInk]::Rows($b, $AppOffsetX, $AppOffsetY, $x0, $y0, $x1, $y1))
        }
        else {
            Write-Output ("{0,-16} art {1}" -f $label, [SidequestInk]::Box($a, 0, 0, $x0, $y0, $x1, $y1, $Threshold))
            Write-Output ("{0,-16} app {1}" -f '', [SidequestInk]::Box($b, $AppOffsetX, $AppOffsetY, $x0, $y0, $x1, $y1, $Threshold))
        }
    }
}
finally { $a.Dispose(); $b.Dispose() }
