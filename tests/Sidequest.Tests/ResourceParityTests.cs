using System;
using System.Linq;
using Sidequest.Models;
using Xunit;

namespace Sidequest.Tests;

/// <summary>
/// The theme and language dictionaries are swapped at runtime, and a key missing from one
/// of a pair fails silently: DynamicResource shows nothing, and LanguageService.Get hands
/// back the key itself. These catch that at build time instead of on screen.
/// </summary>
public class ResourceParityTests
{
    [Fact]
    public void BothLanguagesDefineTheSameStrings()
    {
        var english = RepoFiles.Keys("Strings", "English.xaml");
        var spanish = RepoFiles.Keys("Strings", "Spanish.xaml");

        Assert.Empty(english.Except(spanish));   // in English only
        Assert.Empty(spanish.Except(english));   // in Spanish only
    }

    [Fact]
    public void BothThemesDefineTheSameTokens()
    {
        var light = RepoFiles.Keys("Themes", "Light.xaml");
        var dark = RepoFiles.Keys("Themes", "Dark.xaml");

        Assert.Empty(light.Except(dark));
        Assert.Empty(dark.Except(light));
    }

    [Theory]
    [InlineData("English.xaml")]
    [InlineData("Spanish.xaml")]
    public void NoStringIsDefinedTwice(string file)
    {
        // A later duplicate silently wins, so an edit to the first copy does nothing.
        var keys = RepoFiles.Keys("Strings", file);
        var duplicates = keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(duplicates);
    }

    [Fact]
    public void EverySidequestThemeHasAName()
    {
        // The pickers look names up as L_Theme_{enum name}, so a new theme needs both.
        var english = RepoFiles.Keys("Strings", "English.xaml");
        var spanish = RepoFiles.Keys("Strings", "Spanish.xaml");

        foreach (var theme in Enum.GetValues<SidequestTheme>())
        {
            Assert.Contains($"L_Theme_{theme}", english);
            Assert.Contains($"L_Theme_{theme}", spanish);
        }
    }
}
