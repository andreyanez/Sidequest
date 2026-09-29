namespace Sidequest.Models;

public enum AppTheme
{
    Light,
    Dark,
}

public enum AppLanguage
{
    English,
    Spanish,
}

/// <summary>3's settings. WeeklyReminder's schedule and popup fields went with D1.</summary>
public sealed class AppSettings
{
    /// <summary>3 makes dark the default.</summary>
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>"Sistema" in Apariencia: follow the Windows light/dark setting.</summary>
    public bool FollowSystemTheme { get; set; }

    /// <summary>Null until chosen, so a first run can follow the Windows display language.</summary>
    public AppLanguage? Language { get; set; }

    public bool StartWithWindows { get; set; }

    /// <summary>Whether the main panel floats above other windows.</summary>
    public bool KeepOnTop { get; set; } = true;

    /// <summary>
    /// Whether the main panel is rolled up. Persisted on purpose, unlike the old popup's
    /// collapsed state: the panel is a fixture, and it should be found as it was left.
    /// </summary>
    public bool Collapsed { get; set; }

    /// <summary>
    /// Where the user dragged the main panel. Null means "anchor to the bottom-right",
    /// which is also the fallback if the saved spot is no longer on any screen.
    /// </summary>
    public double? MainLeft { get; set; }

    public double? MainTop { get; set; }
}
