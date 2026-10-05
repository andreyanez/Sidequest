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
    /// A new Sidequest at the end of the list. Nothing reorders the list, so it takes the
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

    /// <summary>
    /// Renames an item, which renames its objective too, being the same object (5.1, 5.6).
    /// A blank name is not saved and the old one stays. Returns whether anything changed.
    /// </summary>
    public static bool RenameItem(SidequestItem item, string text)
    {
        var clipped = Clip(text, SidequestItem.MaxTextLength);
        if (clipped.Length == 0 || clipped == item.Text) return false;

        item.Text = clipped;
        return true;
    }

    /// <summary>
    /// The Notas tab's notes (5.6): plain text, at most 500 characters, addresses
    /// included. Lines are kept as "\n", which is how the counter counts them. Returns
    /// whether anything changed.
    /// </summary>
    public static bool SetNotes(SidequestProject project, string notes)
    {
        var text = notes.Replace("\r\n", "\n");
        if (text.Length > SidequestProject.MaxNotesLength) text = text[..SidequestProject.MaxNotesLength];
        if (text == project.Notes) return false;

        project.Notes = text;
        return true;
    }

    /// <summary>A new pending item, after every other item in its Sidequest.</summary>
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

    /// <summary>
    /// D8 on the panel (5.1): moves the objective at <paramref name="from"/> to
    /// <paramref name="to"/>, done or not. <paramref name="panel"/> is the panel's items in
    /// the order shown; they are numbered afresh from 0, which keeps that order whatever
    /// indexes they held before. Returns whether anything moved.
    /// </summary>
    public static bool MoveInPanel(IList<SidequestItem> panel, int from, int to)
    {
        if (from < 0 || from >= panel.Count) return false;
        to = Math.Clamp(to, 0, panel.Count - 1);
        if (from == to) return false;

        var order = panel.ToList();
        var item = order[from];
        order.RemoveAt(from);
        order.Insert(to, item);

        for (var i = 0; i < order.Count; i++) order[i].PanelIndex = i;
        return true;
    }

    /// <summary>
    /// D8 in a Sidequest (5.6): moves an item to another place in its own group, current
    /// objectives among themselves and pending items among themselves. Completed items do
    /// not move. The group's items trade the SortIndex values they already hold, so the
    /// other groups keep theirs, and an item that changes group later still lands near
    /// where it was. Returns whether anything moved.
    /// </summary>
    public static bool MoveInGroup(SidequestProject project, SidequestItem item, int to)
    {
        var group = GroupOf(item);
        if (group == ItemGroup.Completed) return false;

        // Equal indexes would leave the new order to chance; spread them out first.
        if (project.Items.Select(i => i.SortIndex).Distinct().Count() != project.Items.Count)
        {
            var spread = project.Items.OrderBy(i => i.SortIndex).ToList();
            for (var i = 0; i < spread.Count; i++) spread[i].SortIndex = i;
        }

        var members = DetailOrder(project).Where(i => GroupOf(i) == group).ToList();
        var from = members.IndexOf(item);
        to = Math.Clamp(to, 0, members.Count - 1);
        if (from < 0 || from == to) return false;

        var places = members.Select(i => i.SortIndex).OrderBy(i => i).ToList();
        members.RemoveAt(from);
        members.Insert(to, item);

        for (var i = 0; i < members.Count; i++) members[i].SortIndex = places[i];
        return true;
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
