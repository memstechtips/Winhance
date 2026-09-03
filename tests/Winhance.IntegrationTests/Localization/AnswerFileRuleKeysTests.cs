using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.IntegrationTests.Helpers;
using Xunit;

namespace Winhance.IntegrationTests.Localization;

// AnswerFileReportDialog builds each rule's key from the enum name, which the literal-key gate
// cannot see, so the one-key-per-rule contract is pinned here.
public class AnswerFileRuleKeysTests
{
    private static readonly string EnglishFile =
        Path.Combine(TestContext.SolutionDir, "src", "Winhance.UI", "Features", "Common", "Localization", "en.json");

    [Fact]
    public void EveryRule_HasAnEnglishKey()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(EnglishFile));
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        var missing = Enum.GetNames<AnswerFileRule>()
            .Select(rule => "WIMUtil_AnswerFile_Rule_" + rule)
            .Where(key => !keys.Contains(key))
            .ToList();

        missing.Should().BeEmpty();
    }
}
