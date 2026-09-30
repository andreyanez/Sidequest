<#
.SYNOPSIS
Finds where an app capture and an artboard render disagree on flat colour.

.DESCRIPTION
Meant for a screen whose layout already matches: the light pass, say, where the dark one
was measured to the pixel. Only pixels that are flat in both images - the same colour as
all eight neighbours - are compared, so text and edges, which WPF and Chrome antialias
differently, drop out. What's left is fills, and a fill in the wrong colour is usually the
wrong token: Bg where Inv belongs looks right in dark, where the two are one colour.

Disagreements are grouped by colour pair, largest first, with the box each pair spans.
Regions are in the artboard's coordinates; the app capture is read at an offset, since its
card sits inside the window's 12px shadow margin.

.EXAMPLE
.\tools\compare-colours.ps1 -Art art-light.png -App app-light.png -Rect '1,38,399,480'
#>
param(
    [Parameter(Mandatory = $true)][string]$Art,
    [Parameter(Mandatory = $true)][string]$App,
    # "x0,y0,x1,y1", inclusive, in artboard coordinates
    [Parameter(Mandatory = $true)][string]$Rect,
    [int]$AppOffsetX = 12,
    [int]$AppOffsetY = 12,
    # The capture's shadow margin, which shows whatever is behind the window: never compared.
    [int]$AppMargin = 12,
    # Largest channel difference still counted as the same colour.
    [int]$Tolerance = 3,
    # Pairs with fewer pixels than this are left out.
    [int]$MinPixels = 12
)

Add-Type -AssemblyName System.Drawing
if (-not ('SidequestFlat' -as [type])) {
    # Newer runtimes split Bitmap's interfaces into private assemblies beside System.Drawing,
    # and the compiler wants them as soon as a Bitmap is disposed.
    $drawing = Split-Path ([System.Drawing.Bitmap].Assembly.Location)
    $private = Get-ChildItem $drawing -Filter 'System.Private.Windows*.dll' | ForEach-Object FullName
    $refs = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Color].Assembly.Location,
              'System.Collections', 'System.Runtime', 'System.Linq', 'System.Runtime.InteropServices',
              'System.Drawing.Primitives') + @($private)
    Add-Type -ReferencedAssemblies $refs -TypeDefinition @"
using System; using System.Drawing; using System.Drawing.Imaging; using System.Collections.Generic;
using System.Linq; using System.Runtime.InteropServices;
public static class SidequestFlat {
  static int[] Pixels(Bitmap b) {
    var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var px = new int[b.Width * b.Height];
    Marshal.Copy(d.Scan0, px, 0, px.Length);
    b.UnlockBits(d);
    return px;
  }
  static bool Same(int a, int b, int tol) {
    return Math.Abs(((a >> 16) & 255) - ((b >> 16) & 255)) <= tol
        && Math.Abs(((a >> 8) & 255) - ((b >> 8) & 255)) <= tol
        && Math.Abs((a & 255) - (b & 255)) <= tol;
  }
  static bool Flat(int[] px, int w, int h, int x, int y, int tol) {
    if (x < 1 || y < 1 || x >= w - 1 || y >= h - 1) return false;
    int c = px[y * w + x];
    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
      if (!Same(c, px[(y + dy) * w + x + dx], tol)) return false;
    return true;
  }
  public static string[] Compare(string artPath, string appPath, int x0, int y0, int x1, int y1,
                                 int ox, int oy, int margin, int tol, int min) {
    using (var art = new Bitmap(artPath)) using (var app = new Bitmap(appPath)) {
      int aw = art.Width, ah = art.Height, pw = app.Width, ph = app.Height;
      var a = Pixels(art); var p = Pixels(app);
      var groups = new Dictionary<string, int[]>();   // count, minX, minY, maxX, maxY
      int compared = 0;
      for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) {
        int px = x + ox, py = y + oy;
        if (x >= aw || y >= ah || px < margin || py < margin || px >= pw - margin || py >= ph - margin) continue;
        if (!Flat(a, aw, ah, x, y, tol) || !Flat(p, pw, ph, px, py, tol)) continue;
        compared++;
        int ca = a[y * aw + x], cp = p[py * pw + px];
        if (Same(ca, cp, tol)) continue;
        string key = string.Format("art #{0:X6} / app #{1:X6}", ca & 0xFFFFFF, cp & 0xFFFFFF);
        int[] g;
        if (!groups.TryGetValue(key, out g)) { g = new[] { 0, x, y, x, y }; groups[key] = g; }
        g[0]++; g[1] = Math.Min(g[1], x); g[2] = Math.Min(g[2], y); g[3] = Math.Max(g[3], x); g[4] = Math.Max(g[4], y);
      }
      var lines = new List<string>();
      lines.Add(string.Format("{0} flat pixels compared", compared));
      foreach (var kv in groups.OrderByDescending(k => k.Value[0]))
        if (kv.Value[0] >= min)
          lines.Add(string.Format("  {0}: {1,6} px in x {2}-{3} y {4}-{5}", kv.Key, kv.Value[0], kv.Value[1], kv.Value[3], kv.Value[2], kv.Value[4]));
      if (lines.Count == 1) lines.Add("  no disagreement");
      return lines.ToArray();
    }
  }
}
"@
}

$r = $Rect.Split(',') | ForEach-Object { [int]$_ }
[SidequestFlat]::Compare([IO.Path]::GetFullPath($Art), [IO.Path]::GetFullPath($App),
    $r[0], $r[1], $r[2], $r[3], $AppOffsetX, $AppOffsetY, $AppMargin, $Tolerance, $MinPixels)
