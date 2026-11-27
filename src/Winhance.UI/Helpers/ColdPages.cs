namespace Winhance.UI.Helpers;

// A page the frame builds on every visit is cold each time; a cached one only until the frame first builds it.
// Covering a cached page the frame still holds would only flash the loader.
internal sealed class ColdPages
{
    // ColdPagesTests checks these against the NavigationCacheMode each page sets.
    internal static readonly IReadOnlySet<string> CachedTags = new HashSet<string> { "Optimize", "Customize", "SoftwareApps" };

    private readonly HashSet<string> _built = [];

    public bool IsCold(string tag) => !CachedTags.Contains(tag) || !_built.Contains(tag);

    public void Arrived(string? tag)
    {
        if (tag is not null)
            _built.Add(tag);
    }
}
