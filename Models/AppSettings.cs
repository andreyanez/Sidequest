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

public sealed class AppSettings
{
    /// <summary>The app is a reminder app; fewer than two nudges a day defeats the point.</summary>
    public const int MinPopupTimes = 2;

    /// <summary>Times of day the popup appears. Always kept sorted and at least <see cref="MinPopupTimes"/> long.</summary>
    public List<TimeOnly> PopupTimes { get; set; } = new()
    {
        new TimeOnly(9, 0),
        new TimeOnly(15, 0),
    };

    public List<DayOfWeek> ActiveDays { get; set; } = new()
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    };

    public int SnoozeMinutes { get; set; } = 15;

    /// <summary>Seconds before an ignored popup closes itself. 0 keeps it open.</summary>
    public int AutoCloseSeconds { get; set; } = 60;

    /// <summary>Whether the popup floats above other windows. Off suits leaving it open all day.</summary>
    public bool KeepOnTop { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.Light;

    // ---- 3's shape. The fields above that phase 2 deletes with the old screens
    // (PopupTimes, ActiveDays, SnoozeMinutes, AutoCloseSeconds, LastFired) still exist
    // only so the current UI keeps building while the new one is written.

    /// <summary>"Sistema" in Apariencia: follow the Windows light/dark setting.</summary>
    public bool FollowSystemTheme { get; set; }

    /// <summary>
    /// Whether the main panel is rolled up. Persisted on purpose, unlike the old popup's
    /// collapsed state: the panel is a fixture, and it should be found as it was left.
    /// </summary>
    public bool Collapsed { get; set; }

    /// <summary>Main panel position; replaces PopupLeft/PopupTop.</summary>
    public double? MainLeft { get; set; }

    public double? MainTop { get; set; }

    /// <summary>Null until chosen, so a first run can follow the Windows display language.</summary>
    public AppLanguage? Language { get; set; }

    /// <summary>
    /// Where the user dragged the popup to. Null means "anchor to the bottom-right",
    /// which is also the fallback if the saved spot is no longer on any screen.
    /// </summary>
    public double? PopupLeft { get; set; }

    public double? PopupTop { get; set; }

    /// <summary>Last time the scheduler fired, so a restart doesn't re-fire a slot already shown.</summary>
    public DateTime? LastFired { get; set; }

    /// <summary>Drops duplicates, sorts, and pads back up to the minimum if needed.</summary>
    public void NormalizePopupTimes()
    {
        var times = PopupTimes.Distinct().OrderBy(t => t).ToList();

        // Pad with sensible spread-out defaults rather than refusing to load.
        var fallbacks = new[] { new TimeOnly(9, 0), new TimeOnly(15, 0), new TimeOnly(12, 0), new TimeOnly(18, 0) };
        foreach (var f in fallbacks)
        {
            if (times.Count >= MinPopupTimes) break;
            if (!times.Contains(f)) times.Add(f);
        }

        PopupTimes = times.OrderBy(t => t).ToList();
    }
}
