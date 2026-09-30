using System.IO;
using Sidequest.Models;
using Sidequest.Services;
using Xunit;

namespace Sidequest.Tests;

public class StorageTests
{
    [Fact]
    public void Settings_RoundTrip()
    {
        using var temp = new TempStore();
        Storage.SaveSettings(new AppSettings
        {
            Theme = AppTheme.Light,
            FollowSystemTheme = true,
            Language = AppLanguage.Spanish,
            KeepOnTop = false,
            Collapsed = true,
            MainLeft = 120,
            MainTop = 80,
        });

        var loaded = Storage.LoadSettings();

        Assert.Equal(AppTheme.Light, loaded.Theme);
        Assert.True(loaded.FollowSystemTheme);
        Assert.Equal(AppLanguage.Spanish, loaded.Language);
        Assert.False(loaded.KeepOnTop);
        Assert.True(loaded.Collapsed);
        Assert.Equal(120, loaded.MainLeft);
        Assert.Equal(80, loaded.MainTop);
    }

    [Fact]
    public void Settings_FromAnEarlierBuild_StillLoad()
    {
        // Builds before phase 3 wrote WeeklyReminder's schedule fields alongside 3's own.
        // They are gone from the model now, and must not stop the rest from loading.
        using var temp = new TempStore();
        File.WriteAllText(Storage.SettingsPath, """
            {
              "PopupTimes": [ "09:00:00", "15:00:00" ],
              "ActiveDays": [ "Monday" ],
              "SnoozeMinutes": 15,
              "AutoCloseSeconds": 60,
              "PopupLeft": 10,
              "LastFired": "2026-09-20T09:00:00",
              "Theme": "Light",
              "KeepOnTop": false
            }
            """);

        var loaded = Storage.LoadSettings();

        Assert.Equal(AppTheme.Light, loaded.Theme);
        Assert.False(loaded.KeepOnTop);
        Assert.Empty(Directory.GetFiles(temp.StoreDirectory, "*.corrupt-*"));
    }

    [Fact]
    public void Settings_DefaultToDark_WhenThereAreNone()
    {
        using var temp = new TempStore();

        Assert.Equal(AppTheme.Dark, Storage.LoadSettings().Theme);
    }
}
