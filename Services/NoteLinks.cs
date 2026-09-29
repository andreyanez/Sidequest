using System.Text.RegularExpressions;

namespace Sidequest.Services;

/// <summary>A run of a note's text: plain, or a web address shown as a link (5.3).</summary>
public readonly record struct NoteSegment(string Text, bool IsLink);

/// <summary>
/// Finds the web addresses in an objective's notes. Notes are plain text (3); links are
/// only a way of showing them, worked out again each time the text is shown.
/// </summary>
public static partial class NoteLinks
{
    // 5.3 detects http:// and https:// only. Anything else - file:, mailto:, a custom
    // protocol - stays plain text, since opening one would hand it to whatever
    // handler Windows has for it.
    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex Address();

    /// <summary>Punctuation that ends a sentence rather than an address.</summary>
    private const string Trailing = ".,;:!?'\"";

    /// <summary>Splits one line of a note into plain text and links, in order.</summary>
    public static IReadOnlyList<NoteSegment> Split(string line)
    {
        var segments = new List<NoteSegment>();
        var at = 0;

        foreach (Match match in Address().Matches(line))
        {
            var url = TrimTail(match.Value);
            if (!IsOpenable(url)) continue;

            if (match.Index > at) segments.Add(new NoteSegment(line[at..match.Index], false));
            segments.Add(new NoteSegment(url, true));
            at = match.Index + url.Length;
        }

        if (at < line.Length) segments.Add(new NoteSegment(line[at..], false));
        return segments;
    }

    /// <summary>True for an absolute http or https address, the only kind 5.3 opens.</summary>
    public static bool IsOpenable(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);

    /// <summary>
    /// "(see https://example.com/a)." should link "https://example.com/a": a sentence's
    /// full stop and a closing bracket with no opening one inside the address both go.
    /// </summary>
    private static string TrimTail(string url)
    {
        while (url.Length > 0)
        {
            var last = url[^1];
            if (Trailing.Contains(last))
            {
                url = url[..^1];
                continue;
            }

            if (Unbalanced(url, last, '(', ')') || Unbalanced(url, last, '[', ']') || Unbalanced(url, last, '{', '}'))
            {
                url = url[..^1];
                continue;
            }

            break;
        }

        return url;
    }

    private static bool Unbalanced(string url, char last, char open, char close) =>
        last == close && url.Count(c => c == close) > url.Count(c => c == open);
}
