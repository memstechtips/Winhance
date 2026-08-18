namespace Winhance.Core.Features.SoftwareApps.Models;

// external-app-* -> external/<winget|choco|stripped-id>; windows-app-* -> windows/<appx-identity>;
// capability-* / feature-* -> windows/<name>. All lowercased.
public static class RepoIconKey
{
    public static string? For(ItemDefinition def)
    {
        if (def.Id.StartsWith("external-app-", System.StringComparison.Ordinal))
        {
            var key = (def.WinGetPackageId is { Length: > 0 } w && !string.IsNullOrEmpty(w[0]))
                ? w[0]
                : !string.IsNullOrEmpty(def.ChocoPackageId)
                    ? def.ChocoPackageId
                    : def.Id["external-app-".Length..];
            return $"icons/external/{key.ToLowerInvariant()}.png";
        }

        if (def.Id.StartsWith("windows-app-", System.StringComparison.Ordinal))
        {
            if (def.AppxPackageName is not { Length: > 0 } a || string.IsNullOrEmpty(a[0]))
                return null;
            return $"icons/windows/{a[0].ToLowerInvariant()}.png";
        }

        if (def.Id.StartsWith("capability-", System.StringComparison.Ordinal))
        {
            return string.IsNullOrEmpty(def.CapabilityName)
                ? null
                : $"icons/windows/{def.CapabilityName.ToLowerInvariant()}.png";
        }

        if (def.Id.StartsWith("feature-", System.StringComparison.Ordinal))
        {
            return string.IsNullOrEmpty(def.OptionalFeatureName)
                ? null
                : $"icons/windows/{def.OptionalFeatureName.ToLowerInvariant()}.png";
        }

        return null;
    }

    public static System.Collections.Generic.IEnumerable<string> WindowsCandidates(ItemDefinition def)
    {
        if (def.AppxPackageName is null) yield break;
        foreach (var a in def.AppxPackageName)
            if (!string.IsNullOrEmpty(a))
                yield return $"icons/windows/{a.ToLowerInvariant()}.png";
    }
}
