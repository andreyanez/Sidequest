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
        Notes = "https://example.com/presupuesto",
        Files = { @"C:\docs\contrato.pdf" },
        Images = { "a1b2.png" },
        Items =
        {
            new SidequestItem { Text = "Pedir cajas", Done = true, SortIndex = 0 },
            new SidequestItem { Text = "Llamar al transportista", InPanel = true, PanelIndex = 2, SortIndex = 1 },
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
        Assert.Equal("https://example.com/presupuesto", loaded.Notes);
        Assert.Equal(@"C:\docs\contrato.pdf", Assert.Single(loaded.Files));
        Assert.Equal("a1b2.png", Assert.Single(loaded.Images));
        Assert.Equal(2, loaded.Items.Count);

        var first = loaded.Items[0];
        Assert.Equal("Pedir cajas", first.Text);
        Assert.True(first.Done);
        Assert.False(first.InPanel);
        Assert.Null(first.PanelIndex);

        var second = loaded.Items[1];
        Assert.True(second.InPanel);
        Assert.Equal(2, second.PanelIndex);
    }

    [Fact]
    public void Load_LeavesOutWhatItemsCarriedIn010_AndKeepsTheRest()
    {
        // D10: a 0.1.0 file kept notes, files and images on items. They are not merged into
        // the Sidequest, and the next save drops them; everything else about the item stays.
        using var temp = new TempStore();
        File.WriteAllText(SidequestStore.SidequestsPath, """
            [ { "Name": "Mudanza", "Theme": "Teal", "SortIndex": 0, "Items": [
                { "Text": "Llamar al transportista", "Done": false, "InPanel": true,
                  "SortIndex": 1, "PanelIndex": 2,
                  "Notes": "https://example.com/presupuesto",
                  "Files": [ "C:\\docs\\contrato.pdf" ], "Images": [ "a1b2.png" ] } ] } ]
            """);

        var loaded = Assert.Single(SidequestStore.Load());
        var item = Assert.Single(loaded.Items);
        Assert.Equal("Llamar al transportista", item.Text);
        Assert.True(item.InPanel);
        Assert.Equal(1, item.SortIndex);
        Assert.Equal(2, item.PanelIndex);
        Assert.Equal("", loaded.Notes);
        Assert.Empty(loaded.Files);
        Assert.Empty(loaded.Images);

        SidequestStore.Save(new[] { loaded });
        var json = File.ReadAllText(SidequestStore.SidequestsPath);
        Assert.DoesNotContain("presupuesto", json);
        Assert.DoesNotContain("contrato.pdf", json);
        Assert.DoesNotContain("a1b2.png", json);
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
