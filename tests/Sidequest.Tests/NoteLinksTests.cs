using System.Linq;
using Sidequest.Services;
using Xunit;

namespace Sidequest.Tests;

public class NoteLinksTests
{
    private static string Links(string line) =>
        string.Join(" | ", NoteLinks.Split(line).Where(s => s.IsLink).Select(s => s.Text));

    [Theory]
    [InlineData("Pedir cotización a 3 empresas. Confirmar embalaje y seguro.")]
    [InlineData("https://transportes.example/cotizar")]
    [InlineData("Ver (https://example.com/a_(b)), luego https://x.example/y.")]
    [InlineData("")]
    public void Segments_PutTheLineBackTogether(string line)
    {
        // Links only change how text is shown; nothing may be lost or reordered.
        Assert.Equal(line, string.Concat(NoteLinks.Split(line).Select(s => s.Text)));
    }

    [Fact]
    public void FindsTheArtboardsAddresses()
    {
        Assert.Equal("https://transportes.example/cotizar", Links("https://transportes.example/cotizar"));
        Assert.Equal("", Links("Pedir cotización a 3 empresas. Confirmar embalaje y seguro."));
    }

    [Fact]
    public void FindsSeveralOnOneLine()
    {
        Assert.Equal("http://a.example | https://b.example/c", Links("uno http://a.example y https://b.example/c"));
    }

    [Theory]
    [InlineData("Ver https://example.com/a.", "https://example.com/a")]
    [InlineData("¿https://example.com/a?", "https://example.com/a")]
    [InlineData("(https://example.com/a)", "https://example.com/a")]
    [InlineData("https://en.wikipedia.org/wiki/Foo_(bar)", "https://en.wikipedia.org/wiki/Foo_(bar)")]
    [InlineData("\"https://example.com/q?x=1\",", "https://example.com/q?x=1")]
    public void LeavesASentencesPunctuationOut(string line, string expected)
    {
        Assert.Equal(expected, Links(line));
    }

    [Theory]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("mailto:alguien@example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/archivo")]
    [InlineData("https://")]
    public void OnlyWebAddressesBecomeLinks(string line)
    {
        // 5.3 detects http and https; opening anything else would hand it to whatever
        // program Windows has registered for it.
        Assert.Equal("", Links(line));
    }

    [Fact]
    public void IgnoresTheSchemesCase()
    {
        Assert.Equal("HTTPS://Example.com/Ruta", Links("HTTPS://Example.com/Ruta"));
    }
}
