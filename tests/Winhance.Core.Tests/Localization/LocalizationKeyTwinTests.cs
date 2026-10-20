using System.Text.Json;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Localization;

// Two keys that differ only in underscores or case are one string spelled twice. The code can name only one of them,
// so the other is dead in all 29 locales and a translator fixing one leaves the other stale.
public class LocalizationKeyTwinTests
{
    [Fact]
    public void NoTwoKeys_DifferOnlyByUnderscoresOrCase()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.LocalizationDir(), "en.json")));
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        keys.Should().HaveCountGreaterThan(500, "en.json is the reference and must have loaded");
        keys.GroupBy(key => key.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" / ", g))
            .Should().BeEmpty("each string needs one key; keep the one the code names and delete its twin from every locale");
    }
}
