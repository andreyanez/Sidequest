using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sidequest.Views;

/// <summary>
/// The corner radius that makes 4.4's pill: half the element's height.
///
/// The artboards write pills as border-radius: 999px, and CSS scales an oversized radius
/// down evenly, which leaves a pill. WPF scales it separately along each side, so 999 on a
/// 64x26 chip gives 32 across and 13 down - an ellipse. Bound to ActualHeight, the radius
/// is right for any height, focus rings included.
/// </summary>
public sealed class PillRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double height && height > 0 ? new CornerRadius(height / 2) : new CornerRadius(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
