using System.IO;
using Sidequest.Models;
using Sidequest.Services;
using Xunit;

namespace Sidequest.Tests;

public class SidequestStoreTests
{
    private static SidequestProject SampleProject() => new()
    {
        Name = "Mudanza",
        Theme = SidequestTheme.Teal,
        SortIndex = 3,
        Items =
        {
            new SidequestItem { Text = "Pedir cajas", Done = true, SortIndex = 0 },
            new SidequestItem
            {
                Text = "Llamar al transportista",
                InPanel = true,
                PanelIndex = 2,
                SortIndex = 1,
                Notes = "https://example.com/presupuesto",
                Files = { @"C:\docs\contrato.pdf" },
                Images = { "a1b2.png" },
            },
        },
    };

    [Fact]
    public void Load_ReturnsEmpty_WhenNothingSavedYet()
    {
        using var temp = new TempStore();

        Assert.False(SidequestStore.Exists);
        Assert.Empty(SidequestStore.Load());
    }

    [Fact]
    public void RoundTrip_PreservesEveryFieldThatMatters()
    {
        using var temp = new TempStore();
        SidequestStore.Save(new[] { SampleProject() });

        var loaded = Assert.Single(SidequestStore.Load());
        Assert.Equal("Mudanza", loaded.Name);
        Assert.Equal(SidequestTheme.Teal, loaded.Theme);
        Assert.Equal(3, loaded.SortIndex);
        Assert.Equal(2, loaded.Items.Count);

        var first = loaded.Items[0];
        Assert.Equal("Pedir cajas", first.Text);
        Assert.True(first.Done);
        Assert.False(first.InPanel);
        Assert.Null(first.PanelIndex);

        var second = loaded.Items[1];
        Assert.True(second.InPanel);
        Assert.Equal(2, second.PanelIndex);
        Assert.Equal("https://example.com/presupuesto", second.Notes);
        Assert.Equal(@"C:\docs\contrato.pdf", Assert.Single(second.Files));
        Assert.Equal("a1b2.png", Assert.Single(second.Images));
    }

    [Fact]
    public void Save_WritesEnumsAsNames_SoTheFileStaysReadable()
    {
        using var temp = new TempStore();
        SidequestStore.Save(new[] { SampleProject() });

        var json = File.ReadAllText(SidequestStore.SidequestsPath);
        Assert.Contains("\"Teal\"", json);
        Assert.DoesNotContain("\"Theme\": 1", json);
    }

    [Fact]
    public void Save_OmitsComputedProperties()
    {
        // A stored count goes stale the moment an item is ticked. WeeklyReminder shipped
        // exactly this bug, writing a computed RangeLabel into its data file.
        using var temp = new TempStore();
        SidequestStore.Save(new[] { SampleProject() });

        var json = File.ReadAllText(SidequestStore.SidequestsPath);
        Assert.DoesNotContain("DoneCount", json);
        Assert.DoesNotContain("TotalCount", json);
        Assert.DoesNotContain("Progress", json);
    }

    [Fact]
    public void Load_QuarantinesAnUnreadableFile_RatherThanLosingIt()
    {
        using var temp = new TempStore();
        File.WriteAllText(SidequestStore.SidequestsPath, "{ this is not json");

        Assert.Empty(SidequestStore.Load());

        var quarantined = Directory.GetFiles(temp.StoreDirectory, "sidequests.json.corrupt-*");
        Assert.Single(quarantined);
        Assert.Contains("not json", File.ReadAllText(quarantined[0]));
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        using var temp = new TempStore();
        SidequestStore.Save(new[] { SampleProject() });

        Assert.Empty(Directory.GetFiles(temp.StoreDirectory, "*.tmp"));
    }
}
