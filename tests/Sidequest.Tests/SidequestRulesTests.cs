using System.Collections.Generic;
using System.Linq;
using Sidequest.Models;
using Sidequest.Services;
using Xunit;

namespace Sidequest.Tests;

public class SidequestRulesTests
{
    private static SidequestProject Project(SidequestTheme theme, int sortIndex = 0, params SidequestItem[] items)
    {
        var project = new SidequestProject { Name = theme.ToString(), Theme = theme, SortIndex = sortIndex };
        foreach (var item in items) project.Items.Add(item);
        return project;
    }

    private static SidequestItem Item(string text, int sortIndex, bool done = false, int? panelIndex = null) => new()
    {
        Text = text,
        SortIndex = sortIndex,
        Done = done,
        InPanel = panelIndex is not null,
        PanelIndex = panelIndex,
    };

    // ---- 5.5: default theme -------------------------------------------------------

    [Fact]
    public void DefaultTheme_IsTheFirstOneNoSidequestUses()
    {
        // FnSidequestNuevo's own case: four Sidequests on the first four themes, so Olive.
        var existing = new[]
        {
            Project(SidequestTheme.Amber), Project(SidequestTheme.Teal),
            Project(SidequestTheme.Indigo), Project(SidequestTheme.Coral),
        };

        Assert.Equal(SidequestTheme.Olive, SidequestRules.DefaultTheme(existing));
    }

    [Fact]
    public void DefaultTheme_FillsAGap_BeforeGoingPastTheLastUsed()
    {
        var existing = new[] { Project(SidequestTheme.Amber), Project(SidequestTheme.Indigo) };

        Assert.Equal(SidequestTheme.Teal, SidequestRules.DefaultTheme(existing));
    }

    [Fact]
    public void DefaultTheme_IsAmber_WhenAllEightAreTaken()
    {
        var existing = System.Enum.GetValues<SidequestTheme>().Select(t => Project(t)).ToList();

        Assert.Equal(SidequestTheme.Amber, SidequestRules.DefaultTheme(existing));
    }

    [Fact]
    public void DefaultTheme_IsAmber_ForTheFirstSidequest()
    {
        Assert.Equal(SidequestTheme.Amber, SidequestRules.DefaultTheme(new List<SidequestProject>()));
    }

    // ---- Creating and renaming ------------------------------------------------------

    [Fact]
    public void Create_GoesAfterTheHighestSortIndex_NotTheCount()
    {
        // After a deletion the count and the indexes part ways, so the count would collide.
        var projects = new List<SidequestProject> { Project(SidequestTheme.Amber, 0), Project(SidequestTheme.Teal, 5) };

        var created = SidequestRules.Create(projects, "Nuevo", SidequestTheme.Plum);

        Assert.Equal(6, created.SortIndex);
        Assert.Same(created, projects.Last());
        Assert.Equal(SidequestTheme.Plum, created.Theme);
    }

    [Fact]
    public void Create_TrimsTheName_AndHoldsItToSixtyCharacters()
    {
        var projects = new List<SidequestProject>();

        Assert.Equal("Renovar pasaporte", SidequestRules.Create(projects, "  Renovar pasaporte  ", SidequestTheme.Amber).Name);
        Assert.Equal(SidequestProject.MaxNameLength, SidequestRules.Create(projects, new string('x', 80), SidequestTheme.Amber).Name.Length);
    }

    [Fact]
    public void Rename_RefusesABlankName_AndKeepsTheOldOne()
    {
        var project = Project(SidequestTheme.Teal);
        project.Name = "Mudanza";

        Assert.False(SidequestRules.Rename(project, "   "));
        Assert.Equal("Mudanza", project.Name);

        Assert.True(SidequestRules.Rename(project, " Mudanza de piso "));
        Assert.Equal("Mudanza de piso", project.Name);
    }

    [Fact]
    public void RenameItem_KeepsTheOldText_WhenTheNewOneIsBlankOrTheSame()
    {
        // 5.1: "un nombre vacío no se guarda (se restaura el anterior)".
        var item = Item("Cotizar camión de mudanza", 0, panelIndex: 0);

        Assert.False(SidequestRules.RenameItem(item, "  "));
        Assert.False(SidequestRules.RenameItem(item, " Cotizar camión de mudanza "));
        Assert.Equal("Cotizar camión de mudanza", item.Text);

        Assert.True(SidequestRules.RenameItem(item, " Cotizar camión con embalaje "));
        Assert.Equal("Cotizar camión con embalaje", item.Text);
        Assert.True(item.InPanel);
    }

    [Fact]
    public void RenameItem_HoldsTheTextToAHundredAndTwentyCharacters()
    {
        var item = Item("a", 0);

        SidequestRules.RenameItem(item, new string('x', 150));

        Assert.Equal(SidequestItem.MaxTextLength, item.Text.Length);
    }

    [Fact]
    public void SetNotes_KeepsLinesAsNewlines_AndStopsAtFiveHundred()
    {
        var project = new SidequestProject();

        Assert.True(SidequestRules.SetNotes(project, "uno\r\ndos"));
        Assert.Equal("uno\ndos", project.Notes);
        Assert.False(SidequestRules.SetNotes(project, "uno\ndos"));

        SidequestRules.SetNotes(project, new string('x', 600));
        Assert.Equal(SidequestProject.MaxNotesLength, project.Notes.Length);
    }

    [Fact]
    public void AddItem_IsPending_AtTheEndOfItsSidequest()
    {
        var project = Project(SidequestTheme.Teal, 0, Item("a", 0), Item("b", 7));

        var added = SidequestRules.AddItem(project, "  c  ");

        Assert.Equal("c", added.Text);
        Assert.Equal(8, added.SortIndex);
        Assert.False(added.Done);
        Assert.False(added.InPanel);
        Assert.Equal(ItemGroup.Pending, SidequestRules.GroupOf(added));
    }

    // ---- 1: objectives ----------------------------------------------------------------

    [Fact]
    public void SendToPanel_GoesAfterEveryObjective_InEverySidequest()
    {
        var sending = Item("new", 3);
        var projects = new[]
        {
            Project(SidequestTheme.Amber, 0, Item("a", 0, panelIndex: 4)),
            Project(SidequestTheme.Teal, 1, Item("b", 0, panelIndex: 9), sending),
        };

        SidequestRules.SendToPanel(sending, projects);

        Assert.True(sending.InPanel);
        Assert.Equal(10, sending.PanelIndex);
    }

    [Fact]
    public void SendToPanel_StartsAtZero_OnAnEmptyPanel()
    {
        var sending = Item("first", 0);

        SidequestRules.SendToPanel(sending, new[] { Project(SidequestTheme.Amber, 0, sending) });

        Assert.Equal(0, sending.PanelIndex);
    }

    [Fact]
    public void ReturnToSidequest_LeavesThePanel_AndKeepsDone()
    {
        var item = Item("done", 0, done: true, panelIndex: 2);

        SidequestRules.ReturnToSidequest(item);

        Assert.False(item.InPanel);
        Assert.Null(item.PanelIndex);
        Assert.True(item.Done);
    }

    // ---- 5.6: the detail's order -----------------------------------------------------

    [Fact]
    public void DetailOrder_IsCurrent_ThenPending_ThenCompleted_BySortIndexWithinEach()
    {
        var project = Project(SidequestTheme.Teal, 0,
            Item("pending late", 5),
            Item("completed", 1, done: true),
            Item("current", 4, panelIndex: 0),
            Item("pending early", 2),
            // Ticked but still in the panel: FnSidequest lists it with the completed ones.
            Item("ticked objective", 0, done: true, panelIndex: 1));

        var order = SidequestRules.DetailOrder(project).Select(i => i.Text);

        Assert.Equal(new[] { "current", "pending early", "pending late", "ticked objective", "completed" }, order);
    }

    [Fact]
    public void CanUntickInDetail_OnlyForCompletedItemsOutsideThePanel()
    {
        Assert.True(SidequestRules.CanUntickInDetail(Item("done", 0, done: true)));
        Assert.False(SidequestRules.CanUntickInDetail(Item("done in panel", 0, done: true, panelIndex: 0)));
        Assert.False(SidequestRules.CanUntickInDetail(Item("pending", 0)));
    }

    // ---- D8: reordering -----------------------------------------------------------------

    [Fact]
    public void MoveInPanel_NumbersThePanelAfresh_InTheNewOrder()
    {
        // FnMainDrag: the second objective dragged one place down. Gaps in the old
        // indexes, as deletions leave them, don't matter.
        var a = Item("Elegir", 0, panelIndex: 0);
        var b = Item("Cotizar", 0, panelIndex: 3);
        var c = Item("Terminar", 0, panelIndex: 7);
        var d = Item("Comprar", 0, done: true, panelIndex: 8);

        Assert.True(SidequestRules.MoveInPanel(new[] { a, b, c, d }, 1, 2));

        Assert.Equal(new int?[] { 0, 2, 1, 3 }, new[] { a.PanelIndex, b.PanelIndex, c.PanelIndex, d.PanelIndex });
    }

    [Fact]
    public void MoveInPanel_MovesCompletedObjectivesToo_AndStopsAtTheEnds()
    {
        var a = Item("a", 0, panelIndex: 0);
        var done = Item("done", 0, done: true, panelIndex: 1);

        Assert.True(SidequestRules.MoveInPanel(new[] { a, done }, 1, -5));
        Assert.Equal(0, done.PanelIndex);
        Assert.Equal(1, a.PanelIndex);

        Assert.False(SidequestRules.MoveInPanel(new[] { done, a }, 0, 0));
    }

    [Fact]
    public void MoveInGroup_TradesPlacesWithinTheGroup_AndLeavesTheOthersAlone()
    {
        var current = Item("current", 1, panelIndex: 0);
        var p1 = Item("p1", 2);
        var p2 = Item("p2", 4);
        var completed = Item("completed", 5, done: true);
        var p3 = Item("p3", 7);
        var project = Project(SidequestTheme.Teal, 0, current, p1, p2, completed, p3);

        Assert.True(SidequestRules.MoveInGroup(project, p3, 0));

        Assert.Equal(new[] { "current", "p3", "p1", "p2", "completed" },
            SidequestRules.DetailOrder(project).Select(i => i.Text));
        // p3 takes p1's place and the rest shift along; the others keep their indexes.
        Assert.Equal((2, 4, 7), (p3.SortIndex, p1.SortIndex, p2.SortIndex));
        Assert.Equal((1, 5), (current.SortIndex, completed.SortIndex));
    }

    [Fact]
    public void MoveInGroup_CompletedItemsStay_AndAMoveStopsAtTheGroupsEnds()
    {
        var current = Item("current", 0, panelIndex: 0);
        var pending = Item("pending", 1);
        var completed = Item("completed", 2, done: true);
        var project = Project(SidequestTheme.Teal, 0, current, pending, completed);

        Assert.False(SidequestRules.MoveInGroup(project, completed, 0));
        // Alone in its group, the pending item has nowhere to go - not into the current one.
        Assert.False(SidequestRules.MoveInGroup(project, pending, 0));
        Assert.Equal((0, 1, 2), (current.SortIndex, pending.SortIndex, completed.SortIndex));
    }

    [Fact]
    public void MoveInGroup_SpreadsEqualIndexesFirst_SoTheNewOrderHolds()
    {
        var a = Item("a", 3);
        var b = Item("b", 3);
        var c = Item("c", 3);
        var project = Project(SidequestTheme.Teal, 0, a, b, c);

        Assert.True(SidequestRules.MoveInGroup(project, a, 2));

        Assert.Equal(new[] { "b", "c", "a" }, SidequestRules.DetailOrder(project).Select(i => i.Text));
        Assert.Equal(3, project.Items.Select(i => i.SortIndex).Distinct().Count());
    }
}
