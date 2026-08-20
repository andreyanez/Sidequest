using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace WeeklyReminder.Models;

/// <summary>The plan for one week, keyed by the Monday it starts on.</summary>
public sealed class WeekPlan
{
    public DateOnly WeekStart { get; set; } = CurrentWeekStart();

    public ObservableCollection<TaskItem> Items { get; set; } = new();

    /// <summary>Monday of the week containing <paramref name="date"/>.</summary>
    public static DateOnly WeekStartFor(DateOnly date)
    {
        int delta = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-delta);
    }

    public static DateOnly CurrentWeekStart() =>
        WeekStartFor(DateOnly.FromDateTime(DateTime.Now));

    /// <summary>Display only — it would otherwise be written to week.json and go stale on rollover.</summary>
    [JsonIgnore]
    public string RangeLabel
    {
        get
        {
            var end = WeekStart.AddDays(6);
            return $"{WeekStart:ddd d MMM} – {end:ddd d MMM}";
        }
    }

    /// <summary>
    /// Advance to the current week, clearing one-off items and unticking recurring ones.
    /// Returns true if anything changed and the plan should be saved.
    /// </summary>
    public bool RollForwardToCurrentWeek()
    {
        var current = CurrentWeekStart();
        if (WeekStart == current) return false;

        var carried = Items.Where(i => i.RepeatsWeekly).ToList();
        Items.Clear();
        foreach (var item in carried)
        {
            item.Done = false;
            Items.Add(item);
        }

        WeekStart = current;
        return true;
    }
}
