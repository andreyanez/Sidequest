namespace WeeklyReminder.Models;

public enum AppTheme
{
    Light,
    Dark,
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

    public bool StartWithWindows { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.Light;

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
