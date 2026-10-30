using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class RestartTargetTests
{
    [Theory]
    [InlineData("Explorer", true)]
    [InlineData("explorer", true)]
    [InlineData("intl", false)]
    [InlineData("StartMenuExperienceHost", false)]
    public void IsExplorer_RestartProcess_MatchesTheNameIgnoringCase(string name, bool expected) =>
        RestartTarget.IsExplorer(new RestartProcess(name)).Should().Be(expected);

    [Fact]
    public void IsExplorer_ServiceOrNothing_IsFalse()
    {
        RestartTarget.IsExplorer(new RestartService("Explorer")).Should().BeFalse();
        RestartTarget.IsExplorer(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("intl", true)]
    [InlineData("INTL", true)]
    [InlineData("Explorer", false)]
    public void IsRegional_RestartProcess_MatchesIntlIgnoringCase(string name, bool expected) =>
        RestartTarget.IsRegional(new RestartProcess(name)).Should().Be(expected);

    [Fact]
    public void IsRegional_ServiceOrNothing_IsFalse()
    {
        RestartTarget.IsRegional(new RestartService("intl")).Should().BeFalse();
        RestartTarget.IsRegional(null).Should().BeFalse();
    }
}
