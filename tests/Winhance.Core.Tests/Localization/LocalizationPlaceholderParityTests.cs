using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Localization;

// LocalizationService.GetString(key, args) catches a FormatException and returns the raw string, so a
// translation that names a placeholder the caller never fills reaches the user as a literal "{1}".
public partial class LocalizationPlaceholderParityTests
{
    [GeneratedRegex(@"\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex Placeholder();

    private static HashSet<string> PlaceholdersIn(string value) =>
        Placeholder().Matches(value).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
    }

    [Fact]
    public void EveryLocale_UsesTheSamePlaceholdersAsEnglish()
    {
        var dir = RepoPaths.LocalizationDir();
        var english = Load(Path.Combine(dir, "en.json"));

        var mismatches = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.json").Order(StringComparer.Ordinal))
        {
            var locale = Path.GetFileNameWithoutExtension(file);
            if (locale == "en") continue;

            foreach (var (key, value) in Load(file))
            {
                if (!english.TryGetValue(key, out var source)) continue;
                if (!PlaceholdersIn(value).SetEquals(PlaceholdersIn(source)))
                    mismatches.Add($"{locale}: {key}");
            }
        }

        mismatches.Should().BeEmpty();
    }
}
