namespace Winhance.UI.Helpers;

internal static class HelpLinks
{
    private const string DocsRoot = "https://winhance.net/docs/";

    public static readonly Uri BugReport =
        new("https://github.com/memstechtips/Winhance/issues/new?template=bug_report.yml");

    // Keyed by NavigationRouter tag; paths are relative to the website's docs folder.
    internal static readonly IReadOnlyDictionary<string, string> DocsPageByTag = new Dictionary<string, string>
    {
        ["SoftwareApps"] = "features/software-apps.html",
        ["Optimize"] = "features/optimize.html",
        ["Customize"] = "features/customize.html",
        ["Autounattend"] = "features/autounattend.html",
        ["WimUtil"] = "guides/wimutil.html",
        ["Settings"] = "features/settings.html",
    };

    public static Uri DocsFor(string? pageTag) =>
        new(DocsRoot + (pageTag is not null && DocsPageByTag.TryGetValue(pageTag, out var page) ? page : "index.html"));
}
