using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>
/// Conformance (Step 4 of the Windows-defaults audit): each shipped per-build Default config must match the
/// catalog's WindowsDefault roles 1:1 through <see cref="DefaultConfigProjection"/> - the same projection
/// <see cref="DefaultConfigGeneratorTests"/> writes the files from, so file and gate cannot disagree.
///
/// FORWARD: every Customize/Optimize item resolves to a catalog setting that is available on the build, carries
/// a WindowsDefault for it, and stores exactly the projected value (slider system-units and powercfg AC/DC
/// index shapes included). REVERSE: every available catalog setting with a WindowsDefault for the build appears.
/// This is the WindowsDefault sibling of RecommendedConfigConformanceTests, run once per build because
/// WindowsDefault roles ARE build-scoped (which is exactly why two Default configs exist).
///
/// Run: winhance-harness DefaultConfigConformanceTests
/// </summary>
[Collection(RepoFileWritersCollection.Name)]
public class DefaultConfigConformanceTests
{
    private readonly ITestOutputHelper _output;

    public DefaultConfigConformanceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Win10_default_config_matches_catalog_windows_defaults()
        => RunBuild(DefaultConfigProjection.Targets[0].FileName, DefaultConfigProjection.Targets[0].Build);

    [Fact]
    public void Win11_default_config_matches_catalog_windows_defaults()
        => RunBuild(DefaultConfigProjection.Targets[1].FileName, DefaultConfigProjection.Targets[1].Build);

    private void RunBuild(string fileName, WinBuild build)
    {
        var config = JsonSerializer.Deserialize<UnifiedConfigurationFile>(
                File.ReadAllText(DefaultConfigProjection.ConfigPath(fileName)), ConfigFileConstants.JsonOptions)
            ?? throw new InvalidOperationException($"{fileName} deserialized to null.");

        var items = config.Customize.Features.Values
            .Concat(config.Optimize.Features.Values)
            .SelectMany(section => section.Items)
            .ToList();

        var violations = new List<string>();
        var presentIds = new HashSet<string>(StringComparer.Ordinal);
        int checkedWithDefault = 0;

        foreach (var item in items)
        {
            presentIds.Add(item.Id);

            var setting = SettingCatalog.Find(item.Id);
            if (setting is null)
            {
                violations.Add($"[dangling] config id '{item.Id}' resolves to no catalog setting.");
                continue;
            }

            var expected = DefaultConfigProjection.Project(setting, build);
            if (expected is null)
            {
                violations.Add(
                    $"[stale] '{item.Id}' ({setting.Control}) has no WindowsDefault for build {build.Build} "
                    + "(or is unavailable there) but appears in the config.");
                continue;
            }

            checkedWithDefault++;
            var mismatch = Compare(expected, item);
            if (mismatch is not null)
                violations.Add($"[value] {item.Id} ({setting.Control}): {mismatch}");
        }

        foreach (var setting in SettingCatalog.All)
        {
            if (DefaultConfigProjection.Project(setting, build) is null)
                continue;
            if (!presentIds.Contains(setting.Id))
                violations.Add($"[missing] '{setting.Id}' ({setting.Control}) has a WindowsDefault for build {build.Build} but is absent.");
        }

        Assert.True(items.Count > 250, $"{fileName}: only {items.Count} items read - scoping/deserialization bug.");
        Assert.True(checkedWithDefault > 200, $"{fileName}: only {checkedWithDefault} items had a default to check - population bug.");

        if (violations.Count > 0)
        {
            _output.WriteLine($"{fileName}: {violations.Count} Default-config conformance violation(s):");
            foreach (var v in violations.OrderBy(v => v, StringComparer.Ordinal))
                _output.WriteLine("  " + v);
        }

        Assert.True(
            violations.Count == 0,
            $"{fileName} does not match the catalog WindowsDefaults ({violations.Count} violation(s)):\n"
                + string.Join("\n", violations.OrderBy(v => v, StringComparer.Ordinal)));
    }

    private static string? Compare(ConfigurationItem expected, ConfigurationItem actual)
    {
        if (expected.InputType != actual.InputType)
            return $"projected InputType={expected.InputType}, config has {actual.InputType}.";
        if (expected.IsSelected != actual.IsSelected)
            return $"projected IsSelected={Fmt(expected.IsSelected)}, config has {Fmt(actual.IsSelected)}.";
        if (expected.SelectedIndex != actual.SelectedIndex)
            return $"projected SelectedIndex={Fmt(expected.SelectedIndex)}, config has {Fmt(actual.SelectedIndex)}.";

        foreach (var key in new[] { "ACIndex", "DCIndex", "ACValue", "DCValue", "Value" })
        {
            int? exp = AsInt(expected.PowerSettings?.GetValueOrDefault(key));
            int? act = AsInt(actual.PowerSettings?.GetValueOrDefault(key));
            if (exp != act)
                return $"projected PowerSettings[{key}]={Fmt(exp)}, config has {Fmt(act)}.";
        }
        return null;
    }

    // Config dictionaries deserialize their object values as JsonElement; normalize to int for comparison.
    private static int? AsInt(object? o) => o switch
    {
        null => null,
        int i => i,
        long l => (int)l,
        JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
        JsonElement je when je.ValueKind == JsonValueKind.String && int.TryParse(je.GetString(), out var s) => s,
        string str when int.TryParse(str, out var s) => s,
        _ => null,
    };

    private static string Fmt(int? v) => v.HasValue ? v.Value.ToString() : "<unset>";

    private static string Fmt(bool? v) => v.HasValue ? v.Value.ToString() : "<unset>";
}
