<#
.SYNOPSIS
Types into the running dev build, and reports what has focus afterwards.

.DESCRIPTION
Keys use SendKeys syntax: "Pedir cita{ENTER}", "{TAB}{TAB}", "{ESC}", "{DELETE}".

Two traps in docs/dev-notes.md shape this. SetForegroundWindow fails silently when
another process owns the last input, so it borrows the foreground thread's input queue
as tab-walk.ps1 does. And that borrowing can activate even a window a modal dialog has
disabled, so the target is found with EnumWindows as the process's first visible,
enabled window - the dialog whenever one is up - never through UIA, which can miss it.

Only attaches to a Sidequest started from this repo's bin folder.

.EXAMPLE
.\tools\send-keys.ps1 -Keys 'Pedir cita{ENTER}'
#>
param(
    [Parameter(Mandatory = $true)][string]$Keys,
    [int]$SettleMs = 500,
    [string]$BinDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin')
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
if (-not ('SidequestKeys' -as [type])) {
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class SidequestKeys {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
  [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
  [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
  [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);

  // EnumWindows goes front to back, and ShowDialog disables every other window of the
  // thread, so the first visible, enabled one is where a real keystroke would go. Popups
  // (the "⋯" menu, tooltips) are skipped: they are untitled, never take activation, and
  // WPF routes their keys through the window that owns them.
  public static IntPtr InputWindow(uint target) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == target && IsWindowVisible(h) && IsWindowEnabled(h) && GetWindowTextLength(h) > 0) {
        found = h; return false;
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }

  public static bool Force(IntPtr target) {
    IntPtr fg = GetForegroundWindow();
    if (fg == target) return true;
    uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
    uint me = GetCurrentThreadId();
    bool attached = (fgThread != me) && AttachThreadInput(fgThread, me, true);
    try { BringWindowToTop(target); SetForegroundWindow(target); }
    finally { if (attached) AttachThreadInput(fgThread, me, false); }
    return GetForegroundWindow() == target;
  }
}
"@
}

$proc = Get-Process Sidequest -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($BinDir, [StringComparison]::OrdinalIgnoreCase) } |
    Select-Object -First 1
if (-not $proc) { Write-Output "No Sidequest running from $BinDir"; exit 1 }

$hwnd = [SidequestKeys]::InputWindow([uint32]$proc.Id)
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "No enabled window to type into"; exit 3 }
# Better to send nothing than to type into whatever app has the foreground.
if (-not [SidequestKeys]::Force($hwnd)) { Write-Output "Could not take the foreground; nothing sent"; exit 3 }
Start-Sleep -Milliseconds 250

[System.Windows.Forms.SendKeys]::SendWait($Keys)
Start-Sleep -Milliseconds $SettleMs

$focused = [System.Windows.Automation.AutomationElement]::FocusedElement.Current
Write-Output ("sent {0} -> focus: {1} '{2}'" -f $Keys, $focused.ControlType.ProgrammaticName.Replace('ControlType.', ''), $focused.Name)
