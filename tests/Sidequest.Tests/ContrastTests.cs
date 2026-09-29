using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Sidequest.Models;
using Sidequest.Services;
using Xunit;
using Color = System.Windows.Media.Color;

namespace Sidequest.Tests;

/// <summary>
/// 4.2 states that every theme colour carrying text clears 4.5:1, and that rule governs:
/// where the spec's own table conflicts with it, the table loses. Two light values are
/// already darkened in <see cref="SidequestPalette"/> for exactly that reason, so this
/// suite exists to stop them drifting back, and to cover the surfaces the original check
/// missed - the 9 % list-button tint, and Inv reading on top of a theme colour.
///
/// The surface tokens are read from the theme dictionaries rather than copied here, so
/// changing a colour in XAML is what this measures.
/// </summary>
public class ContrastTests
{
    private const double Required = 4.5;

    /// <summary>Where a theme colour meets a background, per 4.2 and 4.4.</summary>
    public static TheoryData<AppTheme, SidequestTheme> Combinations()
    {
        var data = new TheoryData<AppTheme, SidequestTheme>();
        foreach (var app in Enum.GetValues<AppTheme>())
            foreach (var theme in Enum.GetValues<SidequestTheme>())
                data.Add(app, theme);
        return data;
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void ThemeColourIsLegibleEverywhereItCarriesText(AppTheme app, SidequestTheme theme)
    {
        var tokens = Tokens(app);
        var c = SidequestPalette.Resolve(theme, app);

        var bg = tokens["Bg"];
        var surface = tokens["Surface"];
        var hover = tokens["Hover"];
        var inv = tokens["Inv"];

        // Chip text, Sidequest name, "Enviar objetivo", progress labels.
        AssertContrast(c, bg, app, theme, "on Bg");
        AssertContrast(c, surface, app, theme, "on Surface");

        // "Enviar objetivo" stays in C while its row takes the Hover fill (5.6).
        AssertContrast(c, hover, app, theme, "on Hover");

        // The tinted surfaces are the tight ones: the text sits on a wash of its own
        // colour, which narrows the gap rather than widening it.
        AssertContrast(c, Composite(c, SidequestPalette.Alpha.HeaderTint, bg), app, theme, "on its 14% header band");
        AssertContrast(c, Composite(c, SidequestPalette.Alpha.ListTint, bg), app, theme, "on its 9% list tint");
        AssertContrast(c, Composite(c, SidequestPalette.Alpha.FormTint, bg), app, theme, "on its 7% new-Sidequest form");

        // Reversed out: the "Objetivo actual" pill is Inv text on a solid theme fill.
        AssertContrast(inv, c, app, theme, "Inv on the theme fill");
    }

    private static void AssertContrast(
        Color foreground, Color background, AppTheme app, SidequestTheme theme, string where)
    {
        var ratio = Ratio(foreground, background);

        Assert.True(
            ratio >= Required,
            $"{theme} in {app}: {Hex(foreground)} {where} ({Hex(background)}) is "
            + $"{ratio.ToString("0.00", CultureInfo.InvariantCulture)}:1, below {Required}:1. "
            + "Darken the light value or lighten the dark one until it clears.");
    }

    // ---- colour maths (WCAG 2.1 relative luminance) ---------------------------------

    private static double Ratio(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(Color c) =>
        0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

    private static double Channel(byte raw)
    {
        var v = raw / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    /// <summary>Flattens a translucent fill onto an opaque background, as WPF paints it.</summary>
    private static Color Composite(Color fill, double alpha, Color background) => Color.FromRgb(
        (byte)Math.Round(alpha * fill.R + (1 - alpha) * background.R),
        (byte)Math.Round(alpha * fill.G + (1 - alpha) * background.G),
        (byte)Math.Round(alpha * fill.B + (1 - alpha) * background.B));

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    // ---- theme dictionaries ---------------------------------------------------------

    private static readonly Regex BrushPattern = new(
        """<SolidColorBrush\s+x:Key="(?<key>\w+)"\s+Color="(?<value>#[0-9A-Fa-f]{6})"\s*/>""",
        RegexOptions.Compiled);

    private static Dictionary<string, Color> Tokens(AppTheme app)
    {
        var path = Path.Combine(RepoRoot(), "Themes", app == AppTheme.Dark ? "Dark.xaml" : "Light.xaml");
        Assert.True(File.Exists(path), $"theme dictionary not found at {path}");

        var tokens = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (Match m in BrushPattern.Matches(File.ReadAllText(path)))
        {
            var value = m.Groups["value"].Value;
            tokens[m.Groups["key"].Value] = Color.FromRgb(
                Convert.ToByte(value.Substring(1, 2), 16),
                Convert.ToByte(value.Substring(3, 2), 16),
                Convert.ToByte(value.Substring(5, 2), 16));
        }

        foreach (var required in new[] { "Bg", "Surface", "Hover", "Inv" })
            Assert.True(tokens.ContainsKey(required), $"{required} missing from {Path.GetFileName(path)}");

        return tokens;
    }

    /// <summary>Walks up from the test binaries until the app's project file turns up.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sidequest.csproj")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
