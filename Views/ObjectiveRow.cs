using Sidequest.Models;
using Sidequest.Services;
using Brush = System.Windows.Media.Brush;

namespace Sidequest.Views;

/// <summary>
/// One row of the main panel: the item, plus the Sidequest it came from, which the card
/// needs for the chip's name and the checkbox's fill.
///
/// This wraps the very same SidequestItem rather than copying it, so ticking a box here
/// ticks it in its Sidequest too - the spec is explicit that an objective is not a second
/// entity.
/// </summary>
public sealed class ObjectiveRow : EditableRow
{
    public ObjectiveRow(SidequestItem item, SidequestProject project) : base(item)
    {
        Project = project;
    }

    public SidequestProject Project { get; }

    public string ProjectName => Project.Name;

    /// <summary>
    /// Resolved from two axes, so it cannot live in a theme dictionary. The panel rebuilds
    /// its rows on a theme switch, which re-reads this.
    /// </summary>
    public Brush ThemeBrush => SidequestPalette.Brush(Project.Theme, ThemeService.Current);

    /// <summary>
    /// What a screen reader reads for the row. Without this the generated ItemsControl
    /// container falls back to the type name and announces "Sidequest.Views.ObjectiveRow".
    /// </summary>
    public override string ToString() => $"{Item.Text} - {Project.Name}";
}
