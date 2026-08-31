using FluentAssertions;
using Winhance.Core.Features.Common.Localization;
using Xunit;

namespace Winhance.Core.Tests.Localization;

// The status line concatenates two localized sentences. A literal ASCII space is right after a Latin
// full stop and wrong after the fullwidth one zh-Hans, zh-Hant and ja end their sentences with. The
// characters are written as escapes so a diff cannot confuse them with their ASCII lookalikes.
public class LocalizedTextTests
{
    private const string FullwidthStop = "\u3002";
    private const string HalfwidthKatakanaA = "\uFF71";

    [Fact]
    public void JoinSentences_LatinSentences_AreSeparatedByASpace()
    {
        var joined = LocalizedText.JoinSentences(
            ["Installed 2 of 3 items.", "Enable started in a separate window."]);

        joined.Should().Be("Installed 2 of 3 items. Enable started in a separate window.");
    }

    [Fact]
    public void JoinSentences_AfterAFullwidthStop_AddsNoSpace()
    {
        var joined = LocalizedText.JoinSentences(
            ["installed" + FullwidthStop, "enabling" + FullwidthStop]);

        joined.Should().Be("installed" + FullwidthStop + "enabling" + FullwidthStop);
    }

    // Halfwidth katakana starts at U+FF61, just past the fullwidth forms, and is half-width, so it still
    // needs the space. Without this the range could be widened to the end of the block unnoticed.
    [Fact]
    public void JoinSentences_AfterHalfwidthKatakana_StillAddsASpace()
    {
        var joined = LocalizedText.JoinSentences([HalfwidthKatakanaA, HalfwidthKatakanaA]);

        joined.Should().Be(HalfwidthKatakanaA + " " + HalfwidthKatakanaA);
    }

    [Fact]
    public void JoinSentences_SkipsBlankEntries()
    {
        var joined = LocalizedText.JoinSentences(["One.", "", "   ", "Two."]);

        joined.Should().Be("One. Two.");
    }

    [Fact]
    public void JoinSentences_SingleSentence_IsReturnedUnchanged()
    {
        LocalizedText.JoinSentences(["Only one."]).Should().Be("Only one.");
    }

    [Fact]
    public void JoinSentences_NothingToJoin_IsEmpty()
    {
        LocalizedText.JoinSentences([]).Should().BeEmpty();
    }
}
