namespace Winhance.Core.Features.SoftwareApps.Models;\r
\r
/// <summary>\r
/// Maps an ItemDefinition to its icon path inside the package-icons repo, or null\r
/// when the app has no hosted icon. external-app-* → external/&lt;winget|choco|stripped-id&gt;;\r
/// windows-app-* → windows/&lt;appx-identity&gt;. All lowercased. Capabilities / optional\r
/// features have no hosted icon (system-DLL extraction).\r
/// </summary>\r
public static class RepoIconKey\r
{\r
    public static string? For(ItemDefinition def)\r
    {\r
        if (def.Id.StartsWith("external-app-", System.StringComparison.Ordinal))\r
        {\r
            var key = (def.WinGetPackageId is { Length: > 0 } w && !string.IsNullOrEmpty(w[0]))\r
                ? w[0]\r
                : !string.IsNullOrEmpty(def.ChocoPackageId)\r
                    ? def.ChocoPackageId\r
                    : def.Id["external-app-".Length..];\r
            return $"icons/external/{key.ToLowerInvariant()}.png";\r
        }\r
\r
        if (def.Id.StartsWith("windows-app-", System.StringComparison.Ordinal))\r
        {\r
            if (def.AppxPackageName is not { Length: > 0 } a || string.IsNullOrEmpty(a[0]))\r
                return null;\r
            return $"icons/windows/{a[0].ToLowerInvariant()}.png";\r
        }\r
\r
        return null;\r
    }\r
\r
    /// <summary>All AppX-identity candidate paths for a windows app, in order (handles\r
    /// defs that declare multiple package names, e.g. GamingApp + XboxApp).</summary>\r
    public static System.Collections.Generic.IEnumerable<string> WindowsCandidates(ItemDefinition def)\r
    {\r
        if (def.AppxPackageName is null) yield break;\r
        foreach (var a in def.AppxPackageName)\r
            if (!string.IsNullOrEmpty(a))\r
                yield return $"icons/windows/{a.ToLowerInvariant()}.png";\r
    }\r
}\r
