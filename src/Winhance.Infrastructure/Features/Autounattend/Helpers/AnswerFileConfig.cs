using System.Text.Json;
using System.Text.Json.Nodes;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.Autounattend.Helpers;

internal static class AnswerFileConfig
{
    private static readonly IReadOnlyDictionary<string, ItemDefinition> WindowsApps = WindowsAppDefinitions.GetWindowsApps().Items
        .Concat(CapabilityDefinitions.GetWindowsCapabilities().Items)
        .Concat(OptionalFeatureDefinitions.GetWindowsOptionalFeatures().Items)
        .ToDictionary(item => item.Id, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, ItemDefinition> ExternalApps = ExternalAppDefinitions.GetExternalApps().Items
        .ToDictionary(item => item.Id, StringComparer.Ordinal);
    private static readonly JsonSerializerOptions CompactOptions = new(ConfigFileConstants.JsonOptions) { WriteIndented = false };

    internal static string Serialize(WinhanceConfigFile config)
    {
        var json = JsonSerializer.SerializeToNode(config, CompactOptions)!;
        Compact(json["WindowsApps"]!["Items"]!.AsArray(), config.WindowsApps, WindowsApps, external: false);
        Compact(json["ExternalApps"]!["Items"]!.AsArray(), config.ExternalApps, ExternalApps, external: true);
        return json.ToJsonString(CompactOptions);
    }

    private static void Compact(JsonArray items, ConfigSection section, IReadOnlyDictionary<string, ItemDefinition> catalog, bool external)
    {
        for (int i = 0; i < section.Items.Count; i++)
        {
            var item = section.Items[i];
            if (!catalog.TryGetValue(item.Id, out var definition))
                continue;
            var known = FromCatalog(definition, external);
            if (!(item.AppxPackageName ?? []).SequenceEqual(known.AppxPackageName ?? [], StringComparer.Ordinal)
                || item.CapabilityName != known.CapabilityName || item.OptionalFeatureName != known.OptionalFeatureName
                || item.WinGetPackageId != known.WinGetPackageId)
                continue;
            items[i] = new JsonObject { ["Id"] = item.Id, ["IsSelected"] = JsonValue.Create(item.IsSelected) };
        }
    }

    internal static void Restore(WinhanceConfigFile config)
    {
        Restore(config.WindowsApps, WindowsApps, external: false);
        Restore(config.ExternalApps, ExternalApps, external: true);
    }

    private static void Restore(ConfigSection? section, IReadOnlyDictionary<string, ItemDefinition> catalog, bool external)
    {
        foreach (var item in section?.Items ?? [])
        {
            if (item is null || !string.IsNullOrEmpty(item.Name) || item.AppxPackageName is not null
                || item.CapabilityName is not null || item.OptionalFeatureName is not null || item.WinGetPackageId is not null
                || !catalog.TryGetValue(item.Id, out var definition))
                continue;
            var known = FromCatalog(definition, external);
            item.Name = known.Name;
            item.AppxPackageName = known.AppxPackageName;
            item.CapabilityName = known.CapabilityName;
            item.OptionalFeatureName = known.OptionalFeatureName;
            item.WinGetPackageId = known.WinGetPackageId;
        }
    }

    // Match AppSelectionSource's package choice; a saved variant that differs from this remains verbatim.
    private static ConfigurationItem FromCatalog(ItemDefinition definition, bool external) => new()
    {
        Id = definition.Id,
        Name = definition.Name,
        AppxPackageName = external ? null : definition.AppxPackageName,
        CapabilityName = external || definition.AppxPackageName is { Length: > 0 } ? null : definition.CapabilityName,
        OptionalFeatureName = external || definition.AppxPackageName is { Length: > 0 } || !string.IsNullOrEmpty(definition.CapabilityName)
            ? null : definition.OptionalFeatureName,
        WinGetPackageId = external ? definition.WinGetPackageId?.FirstOrDefault() : null,
    };
}
