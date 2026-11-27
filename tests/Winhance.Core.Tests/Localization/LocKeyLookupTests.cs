using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Localization;

public class LocKeyLookupTests
{
    private static readonly LocKey Key = TestKeys.Of("Test_Lookup_Key");

    [Fact]
    public void ReturnsTheTranslation()
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(l => l.GetString("Test_Lookup_Key")).Returns("Translated");
        localization.MirrorTryGetString();

        localization.Object.GetString(Key).Should().Be("Translated");
    }

    [Fact]
    public void FallsBackToTheKeyOnAMiss()
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(k => $"[{k}]");
        localization.MirrorTryGetString();

        localization.Object.GetString(Key).Should().Be("Test_Lookup_Key");
    }

    [Fact]
    public void FallsBackToTheKeyWithNoService()
    {
        ILocalizationService? localization = null;

        localization.GetString(Key).Should().Be("Test_Lookup_Key");
    }
}
