using FluentAssertions;
using Moq;
using Windows.Foundation;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// Smoke tests against the real PackageManager; they run on the Windows runner.
public class AppxIconSourceTests
{
    [Fact]
    public async Task GetInstalledPackageMapAsync_ReturnsNonEmptyOnStockWindows()
    {
        var sut = new AppxIconSource(Mock.Of<ILogService>());

        var map = await sut.GetInstalledPackageMapAsync();

        map.Should().NotBeEmpty("a default Windows install has many AppX packages");
    }

    [Fact]
    public async Task GetLogoStreamAsync_ForFirstEnumeratedPackage_DoesNotThrow()
    {
        var sut = new AppxIconSource(Mock.Of<ILogService>());
        var map = await sut.GetInstalledPackageMapAsync();
        map.Should().NotBeEmpty();

        var firstFullName = map.Values.First();

        // Some framework packages have no app list entry → null is acceptable.
        var act = async () => await sut.GetLogoStreamAsync(firstFullName, new Size(48, 48));
        await act.Should().NotThrowAsync();
    }
}
