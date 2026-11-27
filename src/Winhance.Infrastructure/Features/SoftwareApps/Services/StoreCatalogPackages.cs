using System.Text.Json;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

// Reads the Display Catalog's Products[].DisplaySkuAvailabilities[].Sku.Properties.Packages[] and keeps, per package
// family, the package the Store would install here. The highest Version is not that package: a publisher that once
// shipped a bundle with a large legacy major keeps winning on Version, so PackageRank decides and Version breaks ties.
internal static class StoreCatalogPackages
{
    private static readonly string[] InstallableFormats = ["Appx", "AppxBundle", "Msix", "MsixBundle"];
    private static readonly string[] DesktopPlatforms = ["Windows.Universal", "Windows.Desktop"];

    internal static Dictionary<string, ulong> PickVersionByFamily(Stream catalogJson, string osArchitecture, ulong osVersion)
    {
        var best = new Dictionary<string, (long Rank, ulong Version)>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(catalogJson);
        foreach (var product in Items(document.RootElement, "Products"))
        {
            foreach (var availability in Items(product, "DisplaySkuAvailabilities"))
            {
                if (!availability.TryGetProperty("Sku", out var sku)
                    || !sku.TryGetProperty("Properties", out var properties))
                    continue;

                foreach (var package in Items(properties, "Packages"))
                {
                    if (!package.TryGetProperty("PackageFamilyName", out var family) || family.GetString() is not { Length: > 0 } familyName
                        || !package.TryGetProperty("Version", out var versionElement) || !TryReadNumber(versionElement, out var version)
                        || !IsInstallableHere(package, osArchitecture, osVersion))
                        continue;

                    var rank = package.TryGetProperty("PackageRank", out var rankElement) && TryReadNumber(rankElement, out var r) ? (long)r : 0;
                    if (!best.TryGetValue(familyName, out var current) || (rank, version).CompareTo(current) > 0)
                        best[familyName] = (rank, version);
                }
            }
        }

        return best.ToDictionary(pair => pair.Key, pair => pair.Value.Version, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsInstallableHere(JsonElement package, string osArchitecture, ulong osVersion) =>
        package.TryGetProperty("PackageFormat", out var format) && format.GetString() is { } formatName
        && InstallableFormats.Contains(formatName, StringComparer.OrdinalIgnoreCase)
        && Items(package, "Architectures").Any(a => a.GetString() is { } architecture
            && (architecture.Equals(osArchitecture, StringComparison.OrdinalIgnoreCase) || architecture.Equals("neutral", StringComparison.OrdinalIgnoreCase)))
        && Items(package, "PlatformDependencies").Any(d =>
            d.TryGetProperty("PlatformName", out var name) && name.GetString() is { } platform
            && DesktopPlatforms.Contains(platform, StringComparer.OrdinalIgnoreCase)
            && d.TryGetProperty("MinVersion", out var min) && TryReadNumber(min, out var minVersion) && minVersion <= osVersion);

    private static IEnumerable<JsonElement> Items(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    // The catalog sends the same field as a JSON number in one place and as a decimal string in another.
    private static bool TryReadNumber(JsonElement element, out ulong value)
    {
        value = 0;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetUInt64(out value),
            JsonValueKind.String => PackedVersion.TryParse(element.GetString(), out value),
            _ => false,
        };
    }
}
