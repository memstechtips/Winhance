namespace Winhance.UI.Features.Common.Helpers;

// Pattern holds {0} for the description, the language's full stop right after it, and one [bracketed] phrase, so a
// translator controls the sentence join and can bracket an inflected form of the term. Most descriptions end without
// a full stop, so the pattern supplies it and any stop the description has is dropped.
public sealed record DescriptionLinkSpec(string Term, string Pattern, string ToolTip, Action? Open = null);

internal readonly record struct DescriptionLinkParts(string Before, string Link, string After);

internal static class DescriptionLink
{
    public static readonly char[] SentenceStops = ['.', '\u3002', '\uFF0E', '\u0964'];

    public static DescriptionLinkParts Locate(string? description, DescriptionLinkSpec spec)
    {
        description ??= string.Empty;
        var body = description.TrimEnd().TrimEnd(SentenceStops);
        var text = body.Length > 0
            ? spec.Pattern.Replace("{0}", body)
            : spec.Pattern.Replace("{0}", string.Empty).TrimStart().TrimStart(SentenceStops).Trim();
        var open = text.IndexOf('[');
        var close = open < 0 ? -1 : text.IndexOf(']', open + 1);
        if (close < 0)
            return new(description.Length == 0 ? string.Empty : description + " ", spec.Term, string.Empty);

        return new(text[..open], text[(open + 1)..close], text[(close + 1)..]);
    }
}
