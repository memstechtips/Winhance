namespace Winhance.Infrastructure.Features.WimUtil.Services;

// Every listing call passes /English, so the state words are English; the identities are still recognised by
// their shape so a label change between Windows releases does not break the match.
internal static class DismListParser
{
    public static List<string> ValuesIn(string output) =>
        [.. output.Split('\n')
            .Select(line => line.IndexOf(':') is var colon and >= 0 ? line[(colon + 1)..].Trim() : string.Empty)
            .Where(value => value.Length > 0)];

    // Full names read Name_Version_Architecture_ResourceId_PublisherId, so the bare name is what comes before the first
    // underscore that leaves four more behind it.
    public static bool IsPackageOf(string fullName, string name) =>
        fullName.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase)
        && fullName[(name.Length + 1)..].Count(c => c == '_') >= 3;

    // The value after an identity is its state; the live script's filter is the same "Installed" one. Language
    // capabilities carry three tildes and app capabilities four, so two is the shared shape.
    public static List<string> InstalledCapabilities(string output)
    {
        var installed = new List<string>();
        string? identity = null;
        foreach (var value in ValuesIn(output))
        {
            if (value.Contains("~~", StringComparison.Ordinal))
            {
                identity = value;
                continue;
            }

            if (identity is not null && value.Equals("Installed", StringComparison.OrdinalIgnoreCase))
            {
                installed.Add(identity);
            }
            identity = null;
        }
        return installed;
    }

    public static HashSet<string> EnabledFeatures(string output)
    {
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? name = null;
        foreach (var line in output.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0)
            {
                continue;
            }

            var label = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (label.Equals("Feature Name", StringComparison.OrdinalIgnoreCase))
            {
                name = value;
            }
            else if (name is not null && label.Equals("State", StringComparison.OrdinalIgnoreCase))
            {
                if (value.StartsWith("Enable", StringComparison.OrdinalIgnoreCase))
                {
                    enabled.Add(name);
                }
                name = null;
            }
        }
        return enabled;
    }
}
