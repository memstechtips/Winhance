using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class CatalogScopeProviderTests
{
    private readonly Mock<IWindowsVersionFilterService> _versionFilter = new();
    private readonly Mock<IHardwareFilterService> _hardwareFilter = new();

    // A filter being ON means the matching scope flag is OFF; the inversion is the whole job of this type.
    [Theory]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, true, true)]
    public void Current_InvertsEachFilterIndependently(
        bool versionFilterOn,
        bool hardwareFilterOn,
        bool expectOtherOsVersions,
        bool expectOtherHardware)
    {
        _versionFilter.Setup(f => f.IsFilterEnabled).Returns(versionFilterOn);
        _hardwareFilter.Setup(f => f.IsFilterEnabled).Returns(hardwareFilterOn);

        var sut = new CatalogScopeProvider(_versionFilter.Object, _hardwareFilter.Object);

        sut.Current.Should().Be(new CatalogScope(expectOtherOsVersions, expectOtherHardware));
    }

    [Fact]
    public void Current_BothFiltersOn_IsTheCurrentMachineScope()
    {
        _versionFilter.Setup(f => f.IsFilterEnabled).Returns(true);
        _hardwareFilter.Setup(f => f.IsFilterEnabled).Returns(true);

        var sut = new CatalogScopeProvider(_versionFilter.Object, _hardwareFilter.Object);

        sut.Current.Should().Be(CatalogScope.CurrentMachine);
    }
}
