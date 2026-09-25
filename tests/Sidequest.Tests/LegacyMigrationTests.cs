using System.IO;
using System.Linq;
using Sidequest.Models;
using Sidequest.Services;
using Xunit;

namespace Sidequest.Tests;

public class LegacyMigrationTests
{
    private const string LegacyWeek = """
        {
          "WeekStart": "2026-08-31",
          "Items": [
            { "Text": "TSP: Reponder 4 preguntas", "Done": false, "RepeatsWeekly": true },
            { "Text": "Ir a gym Makabe",           "Done": true,  "RepeatsWeekly": true },
            { "Text": "Investigacion de Carrera",  "Done": false, "RepeatsWeekly": true }
          ],
          "RangeLabel": "lun. 31 ago. - dom. 6 sept."
        }
        """;

    private const string LegacySettings = """
        {
          "PopupTimes": [ "09:00:00", "15:00:00" ],
          "SnoozeMinutes": 15,
          "KeepOnTop": false,
          "StartWithWindows": true,
          "Theme": "Dark",
          "Language": "Spanish"
        }
        """;

    private static void WriteLegacy(TempStore temp, string? week = LegacyWeek, string? settings = LegacySettings)
    {
        if (week is not null) File.WriteAllText(Path.Combine(temp.LegacyDirectory, "week.json"), week);
        if (settings is not null) File.WriteAllText(Path.Combine(temp.LegacyDirectory, "settings.json"), settings);
    }

    [Fact]
    public void Run_ImportsItemsInOrderAndKeepsDone()
    {
        using var temp = new TempStore();
        WriteLegacy(temp);

        var project = LegacyMigration.Run(new AppSettings(), "Importados");

        Assert.NotNull(project);
        Assert.Equal("Importados", project!.Name);
        Assert.Equal(SidequestTheme.Slate, project.Theme);

        Assert.Collection(project.Items,
            i => Assert.Equal("TSP: Reponder 4 preguntas", i.Text),
            i => Assert.Equal("Ir a gym Makabe", i.Text),
            i => Assert.Equal("Investigacion de Carrera", i.Text));

        Assert.Equal(new[] { 0, 1, 2 }, project.Items.Select(i => i.SortIndex));
        Assert.Equal(new[] { false, true, false }, project.Items.Select(i => i.Done));
    }

    [Fact]
    public void Run_PutsNothingInThePanel()
    {
        // Objectives are chosen deliberately; an import must not fill the panel with 19 of them.
        using var temp = new TempStore();
        WriteLegacy(temp);

        var project = LegacyMigration.Run(new AppSettings(), "Importados");

        Assert.All(project!.Items, i => Assert.False(i.InPanel));
        Assert.All(project.Items, i => Assert.Null(i.PanelIndex));
    }

    [Fact]
    public void Run_PersistsWhatItImported()
    {
        using var temp = new TempStore();
        WriteLegacy(temp);

        LegacyMigration.Run(new AppSettings(), "Importados");

        Assert.True(SidequestStore.Exists);
        var reloaded = Assert.Single(SidequestStore.Load());
        Assert.Equal(3, reloaded.Items.Count);
    }

    [Fact]
    public void Run_RenamesTheOldWeekRatherThanDeletingIt()
    {
        using var temp = new TempStore();
        WriteLegacy(temp);

        LegacyMigration.Run(new AppSettings(), "Importados");

        Assert.False(File.Exists(Path.Combine(temp.LegacyDirectory, "week.json")));
        var kept = Path.Combine(temp.LegacyDirectory, "week.json.migrated");
        Assert.True(File.Exists(kept));
        Assert.Contains("Ir a gym Makabe", File.ReadAllText(kept));
    }

    [Fact]
    public void Run_CarriesOverOnlyTheSettingsThatStillExist()
    {
        using var temp = new TempStore();
        WriteLegacy(temp);

        var settings = new AppSettings { Theme = AppTheme.Light, KeepOnTop = true, StartWithWindows = false };
        LegacyMigration.Run(settings, "Importados");

        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(AppLanguage.Spanish, settings.Language);
        Assert.False(settings.KeepOnTop);
        Assert.True(settings.StartWithWindows);
    }

    [Fact]
    public void Run_DoesNothing_WhenSidequestDataAlreadyExists()
    {
        using var temp = new TempStore();
        WriteLegacy(temp);
        SidequestStore.Save(new[] { new SidequestProject { Name = "Ya existe" } });

        Assert.Null(LegacyMigration.Run(new AppSettings(), "Importados"));

        // The old week must survive untouched, so a mistaken run cannot destroy it.
        Assert.True(File.Exists(Path.Combine(temp.LegacyDirectory, "week.json")));
        Assert.Equal("Ya existe", Assert.Single(SidequestStore.Load()).Name);
    }

    [Fact]
    public void Run_DoesNothing_WhenThereIsNoLegacyData()
    {
        using var temp = new TempStore();

        Assert.Null(LegacyMigration.Run(new AppSettings(), "Importados"));
        Assert.False(SidequestStore.Exists);
    }

    [Fact]
    public void Run_SurvivesAnUnreadableLegacyWeek()
    {
        using var temp = new TempStore();
        WriteLegacy(temp, week: "{ broken");

        Assert.Null(LegacyMigration.Run(new AppSettings(), "Importados"));
        Assert.False(SidequestStore.Exists);
    }

    [Fact]
    public void Run_ImportsEvenWithoutOldSettings()
    {
        using var temp = new TempStore();
        WriteLegacy(temp, settings: null);

        var project = LegacyMigration.Run(new AppSettings(), "Importados");

        Assert.NotNull(project);
        Assert.Equal(3, project!.Items.Count);
    }
}
