using System.Windows.Threading;
using WeeklyReminder.Models;

namespace WeeklyReminder.Services;

/// <summary>
/// Decides when the popup should appear.
///
/// Deliberately polls the wall clock every 30s rather than sleeping until the next
/// slot: that way sleep/resume, hibernation and clock changes need no special
/// handling, and a slot missed while the machine was off still surfaces once on
/// the next tick.
/// </summary>
public sealed class Scheduler : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;
    private DateTime? _snoozedUntil;

    /// <summary>Raised on the UI thread when the popup is due.</summary>
    public event Action? Due;

    public Scheduler(AppSettings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TickInterval,
        };
        _timer.Tick += (_, _) => Evaluate(DateTime.Now);
    }

    public void Start()
    {
        _timer.Start();
        Evaluate(DateTime.Now);
    }

    public void Snooze()
    {
        _snoozedUntil = DateTime.Now.AddMinutes(Math.Max(1, _settings.SnoozeMinutes));
    }

    /// <summary>
    /// Marks the current slot as handled without firing — used when the popup is
    /// shown manually, so the next scheduled slot is still honoured.
    /// </summary>
    public void ClearSnooze() => _snoozedUntil = null;

    private void Evaluate(DateTime now)
    {
        if (_snoozedUntil is { } until)
        {
            if (now < until) return;

            _snoozedUntil = null;
            Fire(now);
            return;
        }

        if (!_settings.ActiveDays.Contains(now.DayOfWeek)) return;

        var slot = LatestDueSlot(now);
        if (slot is null) return;

        Fire(now);
    }

    /// <summary>
    /// The most recent slot today that has already passed and has not been fired yet.
    /// Only the latest one matters: if several were missed (machine asleep), the user
    /// wants one popup, not a queue of them.
    /// </summary>
    private DateTime? LatestDueSlot(DateTime now)
    {
        var lastFired = _settings.LastFired;
        var today = now.Date;

        DateTime? best = null;
        foreach (var time in _settings.PopupTimes)
        {
            var slot = today + time.ToTimeSpan();
            if (slot > now) continue;
            if (lastFired is { } last && slot <= last) continue;
            if (best is null || slot > best) best = slot;
        }

        return best;
    }

    private void Fire(DateTime now)
    {
        _settings.LastFired = now;
        Storage.SaveSettings(_settings);
        Due?.Invoke();
    }

    public void Dispose() => _timer.Stop();
}
