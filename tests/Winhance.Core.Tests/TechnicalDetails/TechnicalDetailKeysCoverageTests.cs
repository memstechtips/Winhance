using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.Common.TechnicalDetails;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.TechnicalDetails;

// The GetString("literal") scanner in the integration tests can't see keys resolved through constants; before
// this, 26 of the panel's ~50 strings rendered English in all 29 languages with nothing to catch it.
public class TechnicalDetailKeysCoverageTests
{
    private static IEnumerable<(string Name, string Key)> AllKeys() =>
        typeof(TechnicalDetailKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!));

    private static string LocalizationDir() => RepoPaths.LocalizationDir();

    private static Dictionary<string, string> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
    }

    [Fact]
    public void EveryKeyTheBuilderAsksFor_ExistsInEnglish()
    {
        var english = Load(Path.Combine(LocalizationDir(), "en.json"));

        var missing = AllKeys().Where(k => !english.ContainsKey(k.Key))
            .Select(k => $"{k.Name} => \"{k.Key}\"").ToList();

        missing.Should().BeEmpty(
            "every TechnicalDetailKeys constant must exist in en.json, otherwise the panel renders "
            + "its hardcoded English fallback with nothing to flag it");
    }

    [Fact]
    public void EveryKeyTheBuilderAsksFor_IsTranslatedInEveryLanguage()
    {
        var dir = LocalizationDir();
        var keys = AllKeys().Select(k => k.Key).ToList();

        var gaps = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f))
        {
            var strings = Load(file);
            var absent = keys.Where(k => !strings.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v));
            gaps.AddRange(absent.Select(k => $"{Path.GetFileName(file)}: {k}"));
        }

        gaps.Should().BeEmpty("all 29 shipped languages carry the same key set");
    }

    [Fact]
    public void FormatKeys_KeepTheirPlaceholder()
    {
        // These are passed through string.Format; a translation that drops {0} silently loses the number.
        string[] formatKeys =
        [
            TechnicalDetailKeys.ChipPartOfValue,
            TechnicalDetailKeys.ChipSubKey,
            TechnicalDetailKeys.CodeWhenSetTo,
        ];

        var dir = LocalizationDir();
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f))
        {
            var strings = Load(file);
            offenders.AddRange(formatKeys
                .Where(k => strings.TryGetValue(k, out var v) && !v.Contains("{0}"))
                .Select(k => $"{Path.GetFileName(file)}: {k}"));
        }

        offenders.Should().BeEmpty("a placeholder-less translation drops the value from the sentence");
    }

    [Fact]
    public void KeyConstants_AreUnique()
    {
        var duplicates = AllKeys()
            .GroupBy(k => k.Key)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} <- {string.Join(", ", g.Select(x => x.Name))}")
            .ToList();

        // Deliberate aliases are fine only when they mean the same thing; nothing should share a key today.
        duplicates.Should().BeEmpty();
    }
}
