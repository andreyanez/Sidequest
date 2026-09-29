using System.Windows;
using System.Windows.Media;

namespace Sidequest.Views;

/// <summary>Finds controls inside generated list items, to put keyboard focus back on them.</summary>
internal static class VisualTree
{
    public static T? FindDescendant<T>(DependencyObject root, Func<T, bool>? match = null) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found && (match is null || match(found))) return found;

            var deeper = FindDescendant(child, match);
            if (deeper is not null) return deeper;
        }

        return null;
    }
}
