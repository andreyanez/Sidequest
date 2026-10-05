using Sidequest.Models;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Sidequest.Services;

/// <summary>
/// Resolves a Sidequest's colour, which depends on two axes: the Sidequest's own theme
/// and whether the app is light or dark (4.2 gives a paired value for each). The tints
/// are derived by alpha rather than stored, so there is one source of truth per theme.
///
/// Brushes are frozen and cached: they are re-resolved on every theme switch, and a
/// frozen brush is both cheaper and safe to share across windows.
/// </summary>
public static class SidequestPalette
{
    /// <summary>
    /// Alpha values from 4.2, named for where each one is used. The second group is not
    /// in 4.2 at all; the artboards and 5.6 introduce them, and the artboards win.
    /// </summary>
    public static class Alpha
    {
        public const double HeaderTint = 0.14;      // banda de cabecera
        public const double ListTint = 0.09;        // botón de Sidequest en la lista
        public const double ListBorder = 0.34;      // borde de ese botón
        public const double ProgressTrack = 0.25;   // pista de progreso (banda de cabecera)
        public const double SendBorder = 0.55;      // borde de "Enviar objetivo"
        public const double SelectionFill = 0.08;   // relleno de forma seleccionada (Bitácora)

        public const double ListProgressTrack = 0.20;  // FnSidequests: la pista en la lista es 20, no 25
        public const double FormTint = 0.07;           // FnSidequestNuevo: tarjeta del formulario
        public const double FormBorder = 0.40;         // FnSidequestNuevo: su borde
        public const double NewItemFill = 0.08;        // 5.6, FnSidequestAdd: el item recién añadido
        public const double NewItemBorder = 0.50;      // 5.6, FnSidequestAdd: su borde
    }

    // 4.2's values, with two light-mode corrections.
    //
    // 4.2's contrast rule governs: where it and the colour table disagree, the table
    // loses. As written, Amber light #9A6B1F measures 4.32 on Bg and 3.64 on its own 14%
    // header band, and Olive light #5F6B2A measures 4.45 on that band. Both are darkened
    // by the smallest amount that clears 4.5:1 everywhere, preserving hue: Olive moves 3%
    // and is imperceptible, Amber moves 16% and reads as a deeper gold.
    //
    // The artboards use the spec's values, so this is the one place the build knowingly
    // departs from them. ContrastTests enforces the rule over every theme, both app
    // themes and all five surfaces a theme colour carries text on, so a colour cannot be
    // changed back without the suite saying so.
    private static readonly Dictionary<SidequestTheme, (Color Dark, Color Light)> Table = new()
    {
        [SidequestTheme.Amber] = (Hex("#D4A24A"), Hex("#815A1A")),   // spec: #9A6B1F
        [SidequestTheme.Teal] = (Hex("#4FB5A8"), Hex("#1F6F6B")),
        [SidequestTheme.Indigo] = (Hex("#8B93E6"), Hex("#4A4F9C")),
        [SidequestTheme.Coral] = (Hex("#E0795F"), Hex("#A1452F")),
        [SidequestTheme.Olive] = (Hex("#A3B357"), Hex("#5C6829")),   // spec: #5F6B2A
        [SidequestTheme.Plum] = (Hex("#C47FB5"), Hex("#7A3F6E")),
        [SidequestTheme.Steel] = (Hex("#6AA5D8"), Hex("#3D6285")),
        [SidequestTheme.Slate] = (Hex("#A0A4AD"), Hex("#55554F")),
    };

    private static readonly Dictionary<(SidequestTheme, AppTheme, int), SolidColorBrush> Cache = new();

    public static Color Resolve(SidequestTheme theme, AppTheme app)
    {
        var pair = Table[theme];
        return app == AppTheme.Dark ? pair.Dark : pair.Light;
    }

    /// <summary>
    /// The theme colour at a given opacity, as a frozen brush. Alpha is applied to the
    /// colour rather than to the element, so a tint does not fade whatever sits on it.
    /// </summary>
    public static SolidColorBrush Brush(SidequestTheme theme, AppTheme app, double alpha = 1.0)
    {
        var key = (theme, app, (int)Math.Round(alpha * 1000));
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var color = Resolve(theme, app);
        color.A = (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255);

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Cache[key] = brush;
        return brush;
    }

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;
}
