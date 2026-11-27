using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.SoftwareApps;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class AppOperationConfirmationTests
{
    private static readonly string[] PlainNames = ["A", "B"];

    private readonly Mock<ILocalizationService> _localization = new();

    public AppOperationConfirmationTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
    }

    private static ItemDefinition Item(string name, bool unstable = false, bool reinstallable = true) =>
        new() { Id = name, Name = name, Description = "", HasInstabilityWarning = unstable, CanBeReinstalled = reinstallable };

    [Fact]
    public void Remove_MarksEachItemByItsOwnFlags()
    {
        var request = AppOperationConfirmation.Build(
            AppOperation.Remove,
            new[] { Item("Plain"), Item("Edge", unstable: true), Item("Flagged", reinstallable: false), Item("Both", true, false) },
            null,
            _localization.Object);

        request.ItemDetails.Should().Equal(
            new ConfirmationItem("Plain"),
            new ConfirmationItem("Edge", HasInstabilityWarning: true),
            new ConfirmationItem("Flagged", CannotBeReinstalled: true),
            new ConfirmationItem("Both", true, true));
        request.Items.Should().Equal("Plain", "Edge", "Flagged", "Both");
    }

    [Fact]
    public void Remove_ShowsBothLegends_WhenBothIconsArePresent()
    {
        var request = AppOperationConfirmation.Build(
            AppOperation.Remove,
            new[] { Item("Edge", unstable: true), Item("Flagged", reinstallable: false) },
            null,
            _localization.Object);

        request.InstabilityLegend.Should().Be("Dialog_RemovalInstabilityLegend");
        request.NoReinstallLegend.Should().Be("Dialog_RemovalNoReinstallLegend");
    }

    [Fact]
    public void Remove_ShowsOnlyTheLegendOfAnIconThatIsPresent()
    {
        var request = AppOperationConfirmation.Build(
            AppOperation.Remove, new[] { Item("Edge", unstable: true), Item("Plain") }, null, _localization.Object);

        request.InstabilityLegend.Should().NotBeNull();
        request.NoReinstallLegend.Should().BeNull();
    }

    [Fact]
    public void Remove_ShowsNoLegend_WhenNoItemHasAnIcon()
    {
        var request = AppOperationConfirmation.Build(
            AppOperation.Remove, PlainNames, null, _localization.Object);

        request.InstabilityLegend.Should().BeNull();
        request.NoReinstallLegend.Should().BeNull();
    }

    [Fact]
    public void Install_CarriesNoIconsOrLegends()
    {
        var request = AppOperationConfirmation.Build(
            AppOperation.Install, new[] { Item("Edge", true, false) }, null, _localization.Object);

        request.ItemDetails.Should().Equal(new ConfirmationItem("Edge"));
        request.InstabilityLegend.Should().BeNull();
        request.NoReinstallLegend.Should().BeNull();
    }
}
