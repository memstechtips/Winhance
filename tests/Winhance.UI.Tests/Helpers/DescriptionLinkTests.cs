using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.TestSupport;
using Winhance.UI.Features.Common.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class DescriptionLinkTests
{
    private static readonly DescriptionLinkSpec Spec = new("Group Policy", "{0}. Uses [Group Policy].", "tip");

    [Fact]
    public void Locate_AppendsEvenWhenTheDescriptionNamesTheTerm()
    {
        DescriptionLink.Locate("Set by group policy on this PC.", Spec)
            .Should().Be(new DescriptionLinkParts("Set by group policy on this PC. Uses ", "Group Policy", "."));
    }

    [Fact]
    public void GroupPolicySettings_LeavePolicyToTheLink()
    {
        var en = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.LocalizationDir(), "en.json"))).RootElement;
        SettingCatalog.All.Where(s => s.WritesGroupPolicy)
            .Where(s => en.TryGetProperty($"Setting_{s.Id}_Description", out var d)
                && d.GetString()!.Contains("polic", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Id)
            .Should().BeEmpty("every Group Policy card ends on the same link, so its description does not mention policy itself");
    }

    [Fact]
    public void Locate_AppendsThePatternSentenceOtherwise()
    {
        DescriptionLink.Locate("Stops apps running in the background.", Spec)
            .Should().Be(new DescriptionLinkParts("Stops apps running in the background. Uses ", "Group Policy", "."));
    }

    [Fact]
    public void Locate_AddsTheFullStopADescriptionLacks()
    {
        DescriptionLink.Locate("Frees disk space automatically", Spec)
            .Should().Be(new DescriptionLinkParts("Frees disk space automatically. Uses ", "Group Policy", "."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Locate_ShowsThePatternAloneWithoutADescription(string? description)
    {
        DescriptionLink.Locate(description, Spec).Should().Be(new DescriptionLinkParts("Uses ", "Group Policy", "."));
    }

    [Fact]
    public void Locate_FallsBackToTheTermWhenThePatternLostItsBrackets()
    {
        DescriptionLink.Locate("Text.", Spec with { Pattern = "{0} Uses Group Policy." })
            .Should().Be(new DescriptionLinkParts("Text. ", "Group Policy", ""));
    }

    [Fact]
    public void EveryLocalePattern_HasOneDescriptionSlotAndOneLink()
    {
        var bad = Directory.GetFiles(RepoPaths.LocalizationDir(), "*.json")
            .Select(f => (File: Path.GetFileName(f),
                Pattern: JsonDocument.Parse(File.ReadAllText(f)).RootElement.GetProperty("GroupPolicy_Link_Pattern").GetString()!))
            .Where(p => Occurrences(p.Pattern, "{0}") != 1 || Occurrences(p.Pattern, "[") != 1 || Occurrences(p.Pattern, "]") != 1
                || p.Pattern.IndexOf('[') > p.Pattern.IndexOf(']')
                || !EndsASentenceAfterTheSlot(p.Pattern))
            .Select(p => p.File);

        bad.Should().BeEmpty("the card draws the bracketed phrase as the link and puts the description at {0}, followed by a full stop");
    }

    private static bool EndsASentenceAfterTheSlot(string pattern)
    {
        var after = pattern.IndexOf("{0}", StringComparison.Ordinal) + 3;
        return after < pattern.Length && DescriptionLink.SentenceStops.Contains(pattern[after]);
    }

    private static int Occurrences(string text, string part) => text.Split(part).Length - 1;
}
