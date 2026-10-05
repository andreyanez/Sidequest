using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sidequest.Models;
using Sidequest.Services;
using Xunit;

namespace Sidequest.Tests;

public class AttachmentsTests
{
    /// <summary>A real, decodable 4x4 PNG outside media\, standing in for a user's photo.</summary>
    private static string WritePng(TempStore temp, string name)
    {
        var source = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 4 * 4], 16);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        var path = Path.Combine(temp.Root, name);
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    // ---- Images (D4) ------------------------------------------------------------------

    [Fact]
    public void ImportImage_CopiesIntoMedia_UnderANewName()
    {
        using var temp = new TempStore();
        var original = WritePng(temp, "foto.PNG");
        var project = new SidequestProject();

        Assert.True(Attachments.ImportImage(project, original));

        var name = Assert.Single(project.Images);
        Assert.NotEqual("foto.PNG", name);
        Assert.EndsWith(".png", name);
        Assert.True(File.Exists(Path.Combine(SidequestStore.MediaDirectory, name)));
        Assert.True(File.Exists(original)); // copied, not moved
    }

    [Fact]
    public void ImportImage_RefusesAFileThatOnlyLooksLikeAnImage()
    {
        using var temp = new TempStore();
        var fake = Path.Combine(temp.Root, "no-soy-una-imagen.png");
        File.WriteAllText(fake, "just text");
        var project = new SidequestProject();

        Assert.False(Attachments.ImportImage(project, fake));
        Assert.Empty(project.Images);
        Assert.False(Directory.Exists(SidequestStore.MediaDirectory) && Directory.EnumerateFiles(SidequestStore.MediaDirectory).Any());
    }

    [Fact]
    public void ImportImage_RefusesOtherKindsOfFile()
    {
        using var temp = new TempStore();
        var text = Path.Combine(temp.Root, "notas.txt");
        File.WriteAllText(text, "hola");

        Assert.False(Attachments.ImportImage(new SidequestProject(), text));
    }

    [Fact]
    public void ImportImage_StopsAtFive()
    {
        using var temp = new TempStore();
        var original = WritePng(temp, "foto.png");
        var project = new SidequestProject();

        for (var i = 0; i < SidequestProject.MaxImages; i++) Assert.True(Attachments.ImportImage(project, original));

        Assert.False(Attachments.ImportImage(project, original));
        Assert.Equal(SidequestProject.MaxImages, project.Images.Count);
    }

    [Fact]
    public void RemoveImage_DeletesTheCopy_AndLeavesTheOriginal()
    {
        using var temp = new TempStore();
        var original = WritePng(temp, "foto.png");
        var project = new SidequestProject();
        Attachments.ImportImage(project, original);
        var name = project.Images[0];

        Attachments.RemoveImage(project, name, new[] { project });

        Assert.Empty(project.Images);
        Assert.False(File.Exists(Attachments.MediaPath(name)));
        Assert.True(File.Exists(original));
    }

    [Fact]
    public void RemoveImage_KeepsACopyThatAnotherSidequestStillUses()
    {
        // Imports never share a copy, but the data file can be edited by hand.
        using var temp = new TempStore();
        var owner = new SidequestProject();
        Attachments.ImportImage(owner, WritePng(temp, "foto.png"));
        var name = owner.Images[0];
        var other = new SidequestProject { Images = { name } };

        Attachments.RemoveImage(owner, name, new[] { owner, other });

        Assert.True(File.Exists(Attachments.MediaPath(name)));
    }

    [Fact]
    public void ReleaseMedia_DeletesTheCopiesOfDeletedSidequests()
    {
        using var temp = new TempStore();
        var deleted = new SidequestProject();
        Attachments.ImportImage(deleted, WritePng(temp, "a.png"));
        var kept = new SidequestProject();
        Attachments.ImportImage(kept, WritePng(temp, "b.png"));

        // As a delete does it: out of the model first, then released.
        Attachments.ReleaseMedia(new[] { deleted }, new[] { kept });

        Assert.False(File.Exists(Attachments.MediaPath(deleted.Images[0])));
        Assert.True(File.Exists(Attachments.MediaPath(kept.Images[0])));
    }

    [Fact]
    public void ReleaseMedia_NeverDeletesOutsideMedia()
    {
        using var temp = new TempStore();
        var outside = Path.Combine(temp.StoreDirectory, "sidequests-backup.json");
        File.WriteAllText(outside, "{}");
        var tampered = new SidequestProject { Images = { @"..\sidequests-backup.json" } };

        Attachments.ReleaseMedia(new[] { tampered }, Enumerable.Empty<SidequestProject>());

        Assert.True(File.Exists(outside));
    }

    // ---- Files -------------------------------------------------------------------------

    [Fact]
    public void LinkFiles_SkipsOnesAlreadyLinked_AndStopsAtFive()
    {
        var project = new SidequestProject { Files = { @"C:\docs\a.pdf" } };
        var offered = new[] { @"C:\DOCS\A.pdf", @"C:\docs\b.pdf", @"C:\docs\c.pdf", @"C:\docs\d.pdf", @"C:\docs\e.pdf", @"C:\docs\f.pdf" };

        var added = Attachments.LinkFiles(project, offered);

        Assert.Equal(4, added);
        Assert.Equal(SidequestProject.MaxFiles, project.Files.Count);
        Assert.DoesNotContain(@"C:\docs\f.pdf", project.Files);
    }

    [Theory]
    [InlineData(@"C:\Users\ana\Documentos\Mudanza\inventario-cajas.xlsx", @"…\Mudanza")]
    [InlineData(@"D:\presupuesto.pdf", @"D:\")]
    [InlineData(@"\\servidor\compartido\planos\casa.dwg", @"…\planos")]
    public void ShortFolder_ShowsOnlyTheFolderTheFileIsIn(string path, string expected)
    {
        Assert.Equal(expected, Attachments.ShortFolder(path));
    }
}
