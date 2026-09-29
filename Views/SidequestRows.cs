using System.Windows;
using Sidequest.Models;
using Sidequest.Services;
using Brush = System.Windows.Media.Brush;

namespace Sidequest.Views;

// Rows for the Sidequest screens. Like ObjectiveRow, each wraps the model objects rather
// than copying them, and resolves its Sidequest's colours for the app's current theme -
// they depend on two axes, so they cannot live in a theme dictionary. The views rebuild
// their rows on ThemeService.Changed, which re-resolves them.
//
// Each overrides ToString, which is what UI Automation reads for a generated item.

/// <summary>One swatch of a theme picker (5.5, and "Cambiar tema" in 5.6).</summary>
public sealed class ThemeSwatch
{
    public ThemeSwatch(SidequestTheme theme)
    {
        Theme = theme;
        Brush = SidequestPalette.Brush(theme, ThemeService.Current);
        Label = LanguageService.Get($"L_Theme_{theme}");
    }

    public SidequestTheme Theme { get; }

    public Brush Brush { get; }

    public string Label { get; }

    public static List<ThemeSwatch> All() =>
        Enum.GetValues<SidequestTheme>().Select(theme => new ThemeSwatch(theme)).ToList();

    public override string ToString() => Label;
}

/// <summary>A Sidequest in the list (5.4): name, counter and progress, in its colours.</summary>
public sealed class SidequestButtonRow
{
    public SidequestButtonRow(SidequestProject project)
    {
        Project = project;

        var app = ThemeService.Current;
        ThemeBrush = SidequestPalette.Brush(project.Theme, app);
        TintBrush = SidequestPalette.Brush(project.Theme, app, SidequestPalette.Alpha.ListTint);
        BorderBrush = SidequestPalette.Brush(project.Theme, app, SidequestPalette.Alpha.ListBorder);
        TrackBrush = SidequestPalette.Brush(project.Theme, app, SidequestPalette.Alpha.ListProgressTrack);

        Counter = LanguageService.Format("L_CounterFmt", project.DoneCount, project.TotalCount);

        // The bar is two star columns, so the fill needs no measuring.
        DoneWidth = new GridLength(project.Progress, GridUnitType.Star);
        RestWidth = new GridLength(1 - project.Progress, GridUnitType.Star);
    }

    public SidequestProject Project { get; }

    public string Name => Project.Name;

    public string Counter { get; }

    public Brush ThemeBrush { get; }

    public Brush TintBrush { get; }

    public Brush BorderBrush { get; }

    public Brush TrackBrush { get; }

    public GridLength DoneWidth { get; }

    public GridLength RestWidth { get; }

    /// <summary>The name with its count spelled out, since "3 / 8" alone reads as a fraction.</summary>
    public string AccessibleName =>
        $"{Project.Name}, {LanguageService.Format("L_ItemsFmt", Project.DoneCount, Project.TotalCount)}";

    public override string ToString() => AccessibleName;
}

/// <summary>The detail's tinted band (5.6): the Sidequest's name, colours and progress.</summary>
public sealed class SidequestHeader
{
    public SidequestHeader(SidequestProject project)
    {
        Project = project;

        var app = ThemeService.Current;
        ThemeBrush = SidequestPalette.Brush(project.Theme, app);
        BandBrush = SidequestPalette.Brush(project.Theme, app, SidequestPalette.Alpha.HeaderTint);
        TrackBrush = SidequestPalette.Brush(project.Theme, app, SidequestPalette.Alpha.ProgressTrack);
        EditLineBrush = SidequestPalette.Brush(project.Theme, app, SidequestPalette.Alpha.SendBorder);

        Counter = LanguageService.Format("L_CounterFmt", project.DoneCount, project.TotalCount);
        ProgressName = LanguageService.Format("L_ItemsFmt", project.DoneCount, project.TotalCount);
        DoneWidth = new GridLength(project.Progress, GridUnitType.Star);
        RestWidth = new GridLength(1 - project.Progress, GridUnitType.Star);
    }

    public SidequestProject Project { get; }

    public string Name => Project.Name;

    public Brush ThemeBrush { get; }

    public Brush BandBrush { get; }

    public Brush TrackBrush { get; }

    /// <summary>The rename field's underline, at "Enviar objetivo"'s 55 %.</summary>
    public Brush EditLineBrush { get; }

    public string Counter { get; }

    public string ProgressName { get; }

    public GridLength DoneWidth { get; }

    public GridLength RestWidth { get; }
}

/// <summary>An item in a Sidequest's detail (5.6). The subclass is the group it sits in.</summary>
public abstract class DetailRow
{
    protected DetailRow(SidequestItem item, SidequestProject project)
    {
        Item = item;
        Project = project;
        ThemeBrush = SidequestPalette.Brush(project.Theme, ThemeService.Current);
    }

    public SidequestItem Item { get; }

    public SidequestProject Project { get; }

    public Brush ThemeBrush { get; }

    public static DetailRow For(SidequestItem item, SidequestProject project) =>
        SidequestRules.GroupOf(item) switch
        {
            ItemGroup.Current => new CurrentObjectiveRow(item, project),
            ItemGroup.Pending => new PendingItemRow(item, project),
            _ => new CompletedItemRow(item, project),
        };

    public override string ToString() => Item.Text;
}

/// <summary>In the panel and not done: carries the "Objetivo actual" pill and no actions.</summary>
public sealed class CurrentObjectiveRow : DetailRow
{
    public CurrentObjectiveRow(SidequestItem item, SidequestProject project) : base(item, project) { }

    public override string ToString() => $"{Item.Text}, {LanguageService.Get("L_CurrentObjective")}";
}

/// <summary>Not in the panel and not done: offers "Enviar objetivo", and delete on hover.</summary>
public sealed class PendingItemRow : DetailRow
{
    public PendingItemRow(SidequestItem item, SidequestProject project) : base(item, project)
    {
        SendBorderBrush = SidequestPalette.Brush(project.Theme, ThemeService.Current, SidequestPalette.Alpha.SendBorder);
    }

    public Brush SendBorderBrush { get; }
}

/// <summary>Done: a bare tick and struck-through text, no card.</summary>
public sealed class CompletedItemRow : DetailRow
{
    public CompletedItemRow(SidequestItem item, SidequestProject project) : base(item, project) { }

    /// <summary>Whether the tick can be clicked off here; see <see cref="SidequestRules.CanUntickInDetail"/>.</summary>
    public bool CanUntick => SidequestRules.CanUntickInDetail(Item);
}
