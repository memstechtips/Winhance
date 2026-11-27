namespace Winhance.Core.Features.Common.Constants;

// Keeps OLD configuration files REVIEWING correctly. Applying a config already ignores the feature key
// (ConfigFileMapper.FromFile iterates Features.Values and matches each item by its own setting id), but
// ConfigReviewService counts, badges and diffs features by the key the file carries, so a file written under a
// retired feature id would apply fine and then show no badge and no reviewable cards for that section.
public static class FeatureIdAliases
{
    // Retired id -> current FeatureIds member.
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["ExplorerCustomization"] = FeatureIds.Explorer,
    };

    public static string Normalize(string id)
        => Aliases.TryGetValue(id, out var current) ? current : id;
}
