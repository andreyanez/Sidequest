<#
.SYNOPSIS
Reports the panel's real tab order, one Tab press at a time.

.DESCRIPTION
Reading focus order out of the XAML is unreliable - a ContentControl or an ItemsControl
takes a tab stop of its own without saying so. This drives the running app and reports
what actually has focus after each press.

Run it against a sandboxed instance (see tools\seed-sandbox.ps1), never one pointed at
%APPDATA%.

.EXAMPLE
.\tools\tab-walk.ps1 -Steps 24
#>
param(
    [int]$Steps = 24,
    # Only ever attaches to a build from this repo, so a normally installed copy of the
    # app is never driven by accident.
    [string]$BinDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin')
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class TabWalkFg {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);

  // Windows refuses SetForegroundWindow from a process that did not receive the last
  // input, which makes a plain call fail silently and send every Tab somewhere else.
  // Borrowing the current foreground thread's input queue lifts that refusal.
  public static bool Force(IntPtr target) {
    IntPtr fg = GetForegroundWindow();
    if (fg == target) return true;
    uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
    uint me = GetCurrentThreadId();
    bool attached = (fgThread != me) && AttachThreadInput(fgThread, me, true);
    try {
      ShowWindow(target, 9);      // SW_RESTORE
      BringWindowToTop(target);
      SetForegroundWindow(target);
    } finally {
      if (attached) AttachThreadInput(fgThread, me, false);
    }
    return GetForegroundWindow() == target;
  }
}
"@

$proc = Get-Process Sidequest -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($BinDir, [StringComparison]::OrdinalIgnoreCase) } |
    Select-Object -First 1
if (-not $proc) { Write-Output "No Sidequest running from $BinDir"; exit 1 }

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { Write-Output "window not found"; exit 1 }

$hwnd = [IntPtr]$win.Current.NativeWindowHandle
[void][TabWalkFg]::Force($hwnd)
Start-Sleep -Milliseconds 500

function Describe($e) {
    if (-not $e) { return "<none>" }
    $c = $e.Current
    $name = $c.Name
    if ([string]::IsNullOrWhiteSpace($name)) { $name = '(UNNAMED - no AutomationProperties.Name)' }
    return ("{0,-14} '{1}'" -f $c.ControlType.ProgrammaticName.Replace('ControlType.', ''), $name)
}

# WPF remembers where focus was left, so the first press does not necessarily start at
# the top of the window. Read the order from the cycle, not from press number one.
Write-Output ("start focus: " + (Describe ([System.Windows.Automation.AutomationElement]::FocusedElement)))
for ($i = 1; $i -le $Steps; $i++) {
    if ([TabWalkFg]::GetForegroundWindow() -ne $hwnd) {
        [void][TabWalkFg]::Force($hwnd)
        Start-Sleep -Milliseconds 300
        if ([TabWalkFg]::GetForegroundWindow() -ne $hwnd) {
            # Better to skip than to report a focus escape that never happened.
            Write-Output ("  tab {0,2}: SKIPPED - could not hold foreground" -f $i)
            continue
        }
    }
    [System.Windows.Forms.SendKeys]::SendWait("{TAB}")
    Start-Sleep -Milliseconds 220

    $f = [System.Windows.Automation.AutomationElement]::FocusedElement
    $inApp = $false
    try { $inApp = ($f.Current.ProcessId -eq $proc.Id) } catch {}
    $mark = if ($inApp) { " " } else { "!" }   # ! = focus left the app
    Write-Output ("{0}tab {1,2}: {2}" -f $mark, $i, (Describe $f))
}
