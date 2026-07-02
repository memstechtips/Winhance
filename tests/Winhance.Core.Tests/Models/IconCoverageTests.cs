using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

/// <summary>
/// Invariant: every catalog item that should have a hosted icon in the
/// memstechtips/package-icons repo actually has one. Guards against adding a new
/// app / capability / optional-feature without hosting its icon - the repo is the
/// only icon source for external apps, and the not-installed fallback for Windows
/// apps.
///
/// Checked against a committed snapshot of package-icons/manifest.json
/// (Assets/package-icons-manifest.json, copied to the test output) because tests
/// cannot hit the network. Refresh that snapshot whenever package-icons changes -
/// its manifest is the source of truth.
/// </summary>
public class IconCoverageTests
{
    // Defs that deliberately have NO hosted icon and fall back to a colored Fluent
    // glyph in-app. Keep in sync with package-icons (design spec 2026-06-05, the
    // "Colored Fluent fallback" cases): OpenSSH x2, Hyper-V Platform/Tools, .NET 3.5,
    // and the AI / Copilot Windows components.
    private static readonly HashSet<string> IntentionallyNoHostedIcon = new(StringComparer.Ordinal)
    {
        "capability-openssh-client",
        "capability-openssh-server",
        "feature-hyperv-platform",
        "feature-hyperv-tools",
        "feature-dotnet35",
        "windows-app-client-aix",
        "windows-app-client-copilot",
        "windows-app-office-actions-server",
        "windows-app-ai-manager",
        "windows-app-writing-assistant",
        "windows-app-ai-workloads",
        "windows-app-copilot-plus-pc",
    };

    private static HashSet<string> LoadManifestIconKeys()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "package-icons-manifest.json");
        File.Exists(path).Should().BeTrue(
            $"the package-icons manifest snapshot must be copied to the test output at {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("icons").EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<ItemDefinition> AllCatalogItems() =>
        ExternalAppDefinitions.GetExternalApps().Items
            .Concat(WindowsAppDefinitions.GetWindowsApps().Items)
            .Concat(CapabilityDefinitions.GetWindowsCapabilities().Items)
            .Concat(OptionalFeatureDefinitions.GetWindowsOptionalFeatures().Items);

    // Manifest keys are paths under icons/ (e.g. "external/x.png"); RepoIconKey
    // returns the full "icons/external/x.png", so strip the prefix to compare.
    private static IEnumerable<string> ManifestCandidateKeys(ItemDefinition def)
    {
        IEnumerable<string> full = def.Id.StartsWith("windows-app-", StringComparison.Ordinal)
            ? RepoIconKey.WindowsCandidates(def)
            : RepoIconKey.For(def) is { } k ? new[] { k } : Array.Empty<string>();

        return full.Select(p =>
            p.StartsWith("icons/", StringComparison.Ordinal) ? p["icons/".Length..] : p);
    }

    [Fact]
    public void EveryCatalogItem_MapsToHostedIcon_OrIsIntentionallyExempt()
    {
        var manifest = LoadManifestIconKeys();

        var missing = new List<string>();
        foreach (var def in AllCatalogItems())
        {
            if (IntentionallyNoHostedIcon.Contains(def.Id))
                continue;

            var keys = ManifestCandidateKeys(def).ToList();
            if (keys.Count == 0)
                continue; // no computable key -> resolves via extraction / colored fallback

            if (!keys.Any(manifest.Contains))
                missing.Add($"{def.Id} ({def.Name}) -> {string.Join(" | ", keys)}");
        }

        missing.Should().BeEmpty(
            "every catalog item that should have a hosted icon must exist in package-icons/manifest.json. " +
            "Fix by adding the icon to the package-icons repo and refreshing " +
            "Assets/package-icons-manifest.json, or - if the item deliberately uses the colored " +
            "Fluent fallback - add its def Id to IntentionallyNoHostedIcon. Missing:\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void IntentionallyExempt_DefIds_AllStillExistInCatalog()
    {
        // Stops the exempt list from rotting: every exempted Id must still be a live def.
        var allIds = AllCatalogItems().Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var stale = IntentionallyNoHostedIcon.Where(id => !allIds.Contains(id)).ToList();

        stale.Should().BeEmpty(
            "exempt def Ids must reference live catalog items; remove stale entries from IntentionallyNoHostedIcon");
    }
}
