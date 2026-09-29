using System.IO;
using System.Windows.Media.Imaging;
using Sidequest.Models;

namespace Sidequest.Services;

/// <summary>
/// An objective's files and images (5.3, D4). Files are referenced where they are, by
/// absolute path; images are copied into media\, so the original can move or go. Nothing
/// here saves the model; callers do.
/// </summary>
public static class Attachments
{
    /// <summary>What an image cell accepts: formats WPF decodes without extra codecs.</summary>
    public static readonly IReadOnlyList<string> ImageExtensions =
        new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff" };

    public static bool IsImage(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static string MediaPath(string name) => Path.Combine(SidequestStore.MediaDirectory, name);

    // ---- Files -----------------------------------------------------------------------

    /// <summary>
    /// Links files to an objective, skipping any already linked and stopping at 5.3's
    /// five. Returns how many were added.
    /// </summary>
    public static int LinkFiles(SidequestItem item, IEnumerable<string> paths)
    {
        var added = 0;
        foreach (var path in paths)
        {
            if (item.Files.Count >= SidequestItem.MaxFiles) break;
            if (item.Files.Contains(path, StringComparer.OrdinalIgnoreCase)) continue;

            item.Files.Add(path);
            added++;
        }

        return added;
    }

    /// <summary>
    /// 5.3's "carpeta contenedora abreviada": the folder a file sits in, as "…\Mudanza".
    /// A file at a drive's root shows the root itself.
    /// </summary>
    public static string ShortFolder(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(folder)) return string.Empty;

        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? folder : "…" + Path.DirectorySeparatorChar + name;
    }

    // ---- Images ----------------------------------------------------------------------

    /// <summary>
    /// Copies an image into media\ under a new name and adds it to the objective (D4).
    /// Returns false, and changes nothing, when the objective already has five, when the
    /// file is not an image WPF can read, or when it cannot be copied.
    /// </summary>
    public static bool ImportImage(SidequestItem item, string source)
    {
        if (item.Images.Count >= SidequestItem.MaxImages) return false;
        if (!IsImage(source) || !CanDecode(source)) return false;

        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(source).ToLowerInvariant();
        try
        {
            SidequestStore.EnsureMediaDirectory();
            File.Copy(source, MediaPath(name));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        item.Images.Add(name);
        return true;
    }

    /// <summary>
    /// Takes an image off an objective and deletes its copy in media\, unless another
    /// objective somehow refers to the same copy. The original was never touched.
    /// </summary>
    public static void RemoveImage(SidequestItem item, string name, IEnumerable<SidequestProject> projects)
    {
        item.Images.Remove(name);
        DeleteUnreferenced(new[] { name }, projects);
    }

    /// <summary>
    /// Deletes the media copies of objectives that were just deleted, once they are out
    /// of <paramref name="remaining"/>. Only ever runs on a user's explicit delete: there
    /// is no sweep, because a quarantined sidequests.json loads as empty, and a sweep
    /// would then take every image with it.
    /// </summary>
    public static void ReleaseMedia(IEnumerable<SidequestItem> deleted, IEnumerable<SidequestProject> remaining) =>
        DeleteUnreferenced(deleted.SelectMany(i => i.Images).ToList(), remaining);

    private static void DeleteUnreferenced(IReadOnlyCollection<string> names, IEnumerable<SidequestProject> projects)
    {
        if (names.Count == 0) return;

        var inUse = projects.SelectMany(p => p.Items).SelectMany(i => i.Images)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            // Names come from the data file, which a person can edit: never follow one
            // out of media\.
            if (inUse.Contains(name) || Path.GetFileName(name) != name) continue;

            try { File.Delete(MediaPath(name)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A copy left behind costs disk space, not data.
            }
        }
    }

    private static bool CanDecode(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return decoder.Frames.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or NotSupportedException or FileFormatException
                                       or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
