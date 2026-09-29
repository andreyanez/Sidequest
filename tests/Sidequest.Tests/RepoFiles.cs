using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Sidequest.Tests;

/// <summary>
/// Reads the app's own XAML dictionaries from the source tree, so a test measures what is
/// actually there rather than a copy of it.
/// </summary>
internal static class RepoFiles
{
    // No closing quote in the pattern: [^"]+ already stops at it, and a raw string whose
    // content ended in a quote would run into its own """ delimiter.
    private static readonly Regex KeyPattern = new("""x:Key="(?<key>[^"]+)""", RegexOptions.Compiled);

    /// <summary>Walks up from the test binaries until the app's project file turns up.</summary>
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sidequest.csproj")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    public static string Read(params string[] relative)
    {
        var path = Path.Combine(Root(), Path.Combine(relative));
        Assert.True(File.Exists(path), $"not found: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>Every x:Key in a dictionary, in order, duplicates included.</summary>
    public static List<string> Keys(params string[] relative)
    {
        var keys = new List<string>();
        foreach (Match m in KeyPattern.Matches(Read(relative)))
            keys.Add(m.Groups["key"].Value);
        return keys;
    }
}
