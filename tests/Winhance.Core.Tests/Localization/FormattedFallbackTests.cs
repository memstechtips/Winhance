using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Localization;

public class FormattedFallbackTests
{
    [Fact]
    public void FormatsTheTranslation()
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(l => l.GetString("Test_Format_Key")).Returns("Failed: {0}");
        localization.MirrorTryGetString();

        localization.Object.GetStringOrDefault("Test_Format_Key", "Error: {0}", "disk full").Should().Be("Failed: disk full");
    }

    [Fact]
    public void FormatsTheFallbackOnAMiss()
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(k => $"[{k}]");
        localization.MirrorTryGetString();

        localization.Object.GetStringOrDefault("Test_Format_Key", "Error: {0}", "disk full").Should().Be("Error: disk full");
    }

    [Fact]
    public void FormatsTheFallbackWithNoService()
    {
        ILocalizationService? localization = null;

        localization.GetStringOrDefault("Test_Format_Key", "Error: {0}", "disk full").Should().Be("Error: disk full");
    }

    [Fact]
    public void AMalformedFormatComesBackUnformatted()
    {
        ILocalizationService? localization = null;

        localization.GetStringOrDefault("Test_Format_Key", "Error: {0", "disk full").Should().Be("Error: {0");
    }
}
