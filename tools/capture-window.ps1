param(
    [Parameter(Mandatory = $true)][string]$ProcessName,
    [Parameter(Mandatory = $true)][string]$OutPath,
    [string]$TitleLike = ''
)

# Captures only the target process's own window rectangle - never the whole desktop.
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public class Cap {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public class Win { public string Title; public IntPtr Handle; public int Left, Top, Width, Height; }
  public static List<Win> Windows(uint target) {
    var res = new List<Win>();
    EnumWindows((h, l) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == target && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        var sb = new StringBuilder(512); GetWindowText(h, sb, 512);
        res.Add(new Win { Title = sb.ToString(), Handle = h, Left = r.Left, Top = r.Top,
                          Width = r.Right - r.Left, Height = r.Bottom - r.Top });
      }
      return true;
    }, IntPtr.Zero);
    return res;
  }
}
"@

$proc = Get-Process $ProcessName -ErrorAction Stop | Select-Object -First 1
$all = [Cap]::Windows([uint32]$proc.Id)
if ($all.Count -eq 0) { Write-Output "NO VISIBLE WINDOWS for $ProcessName (pid $($proc.Id))"; exit 1 }

Write-Output "visible windows:"
foreach ($w in $all) { Write-Output ("  '{0}'  {1},{2}  {3}x{4}" -f $w.Title, $w.Left, $w.Top, $w.Width, $w.Height) }

$candidates = if ($TitleLike) { $all | Where-Object { $_.Title -like $TitleLike } } else { $all }
if (-not $candidates) { Write-Output "NO WINDOW MATCHING '$TitleLike'"; exit 1 }

$t = $candidates | Sort-Object { $_.Width * $_.Height } -Descending | Select-Object -First 1

# CopyFromScreen grabs whatever pixels are on screen, so the target must be on top.
[void][Cap]::SetForegroundWindow($t.Handle)
Start-Sleep -Milliseconds 600
if ([Cap]::GetForegroundWindow() -ne $t.Handle) {
    Write-Output "WARNING: could not foreground the target; the capture may show another app"
}

$bmp = New-Object System.Drawing.Bitmap $t.Width, $t.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($t.Left, $t.Top, 0, 0, $bmp.Size)
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

Write-Output ("captured '{0}' {1}x{2} -> {3}" -f $t.Title, $t.Width, $t.Height, $OutPath)
