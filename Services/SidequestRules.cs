using Sidequest.Models;

namespace Sidequest.Services;

/// <summary>Where an item sits in a Sidequest's detail (5.6).</summary>
public enum ItemGroup
{
    /// <summary>In the panel and not yet done: "Objetivo actual".</summary>
    Current,

    /// <summary>Not in the panel and not done: offered "Enviar objetivo".</summary>
    Pending,

    /// <summary>
    /// Done, wherever it is. FnSidequest lists a ticked objective that is still in the
    /// panel here too, not as the current objective.
    /// </summary>
    Completed,
}

/// <summary>
/// The rules of 1 and 5.4-5.6 that decide where things go and in what order. Kept out of
/// the views so they can be tested without a window. Nothing here saves; callers do.
/// </summary>
public static class SidequestRules
{
    /// <summary>5.5: the first theme no other Sidequest uses, or Amber once all eight are.</summary>
    public static SidequestTheme DefaultTheme(IEnumerable<SidequestProject> existing)
    {
        var used = existing.Select(p => p.Theme).ToHashSet();

        foreach (var theme in Enum.GetValues<SidequestTheme>())
            if (!used.Contains(theme)) return theme;

        return SidequestTheme.Amber;
    }

    /// <summary>
    /// A new Sidequest at the end of the list. SortIndex only grows (D7), so it takes the
    /// next index rather than the count, which a deletion would make collide.
    /// </summary>
    public static SidequestProject Create(ICollection<SidequestProject> projects, string name, SidequestTheme theme)
    {
        var project = new SidequestProject
        {
            Name = Clip(name, SidequestProject.MaxNameLength),
            Theme = theme,
            SortIndex = projects.Count == 0 ? 0 : projects.Max(p => p.SortIndex) + 1,
        };

        projects.Add(project);
        return project;
    }

    /// <summary>
    /// Renames a Sidequest. A blank name is refused and leaves the old one, since a
    /// Sidequest must be findable by name. Returns whether anything changed.
    /// </summary>
    public static bool Rename(SidequestProject project, string name)
    {
        var clipped = Clip(name, SidequestProject.MaxNameLength);
        if (clipped.Length == 0 || clipped == project.Name) return false;

        project.Name = clipped;
        return true;
    }

    /// <summary>A new pending item at the end of its Sidequest (D7).</summary>
    public static SidequestItem AddItem(SidequestProject project, string text)
    {
        var item = new SidequestItem
        {
            Text = Clip(text, SidequestItem.MaxTextLength),
            SortIndex = project.Items.Count == 0 ? 0 : project.Items.Max(i => i.SortIndex) + 1,
        };

        project.Items.Add(item);
        return item;
    }

    /// <summary>
    /// 1's "Enviar objetivo": the item becomes an objective at the end of the panel, which
    /// is ordered by PanelIndex across every Sidequest.
    /// </summary>
    public static void SendToPanel(SidequestItem item, IEnumerable<SidequestProject> projects)
    {
        if (item.InPanel) return;

        var last = projects
            .SelectMany(p => p.Items)
            .Where(i => i.InPanel && i.PanelIndex is not null)
            .Select(i => i.PanelIndex!.Value)
            .DefaultIfEmpty(-1)
            .Max();

        item.PanelIndex = last + 1;
        item.InPanel = true;
    }

    /// <summary>1: back to its Sidequest as it stands - Done is kept.</summary>
    public static void ReturnToSidequest(SidequestItem item)
    {
        item.InPanel = false;
        item.PanelIndex = null;
    }

    public static ItemGroup GroupOf(SidequestItem item) =>
        item.Done ? ItemGroup.Completed
        : item.InPanel ? ItemGroup.Current
        : ItemGroup.Pending;

    /// <summary>5.6's fixed order: current objectives, pending, completed; SortIndex within each.</summary>
    public static IReadOnlyList<SidequestItem> DetailOrder(SidequestProject project) =>
        project.Items.OrderBy(GroupOf).ThenBy(i => i.SortIndex).ToList();

    /// <summary>
    /// 5.6: "el estado Done se edita donde se ve el checkbox". A completed item can be
    /// unticked from the detail only while it is not in the panel; in the panel, the
    /// panel's checkbox is the one that edits it.
    /// </summary>
    public static bool CanUntickInDetail(SidequestItem item) => item.Done && !item.InPanel;

    private static string Clip(string text, int max)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }
}
