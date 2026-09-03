using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.IntegrationTests.Helpers;
using Xunit;

namespace Winhance.IntegrationTests.Localization;

// AnswerFileReportDialog builds each rule's key from the enum name and picks the verdict key in a
// switch, neither of which the literal-key gate can see, so both contracts are pinned here.
public class AnswerFileRuleKeysTests
{
    private static readonly string EnglishFile =
        Path.Combine(TestContext.SolutionDir, "src", "Winhance.UI", "Features", "Common", "Localization", "en.json");

    [Fact]
    public void EveryRule_HasAnEnglishKey()
    {
        MissingKeys("WIMUtil_AnswerFile_Rule_", Enum.GetNames<AnswerFileRule>()).Should().BeEmpty();
    }

    [Fact]
    public void EveryVerdict_HasAnEnglishKey()
    {
        MissingKeys("WIMUtil_AnswerFile_Verdict_", Enum.GetNames<AnswerFileVerdict>()).Should().BeEmpty();
    }

    private static List<string> MissingKeys(string prefix, string[] names)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(EnglishFile));
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        return names.Select(name => prefix + name).Where(key => !keys.Contains(key)).ToList();
    }
}
