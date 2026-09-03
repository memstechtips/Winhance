using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class AppxPackageSourceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IPowerShellRunner> _mockPowerShellRunner = new();
    private readonly FakeWmiApi _wmiApi = new();
    private readonly AppxPackageSource _service;

    public AppxPackageSourceTests()
    {
        _service = new AppxPackageSource(_mockLog.Object, _mockPowerShellRunner.Object, _wmiApi);
    }

    [Fact]
    public async Task GetInstalledPackageNamesAsync_DoesNotThrow()
    {
        // Integration-style: PackageManager COM should work on real Windows;
        // if it fails, fallback paths handle it gracefully.
        var result = await _service.GetInstalledPackageNamesAsync();

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetInstalledPackageNamesAsync_ReturnsCaseInsensitiveSet()
    {
        var result = await _service.GetInstalledPackageNamesAsync();

        result.Should().NotBeNull();
        result.Comparer.Should().Be(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInstalledPackageNamesAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _service.GetInstalledPackageNamesAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // The PackageManager COM primary path is not fakeable here, so the WMI fallback
    // (GetInstalledAppxPackageNamesViaWmiAsync) is exercised directly rather than through
    // GetInstalledPackageNamesAsync's catch branches.
    [Fact]
    public async Task GetInstalledAppxPackageNamesViaWmiAsync_StripsThePublisherHashFromTheFamilyName()
    {
        _wmiApi.For("Win32_InstalledStoreProgram").Add(new FakeWmiInstance
        {
            ["Name"] = "Microsoft.BingSearch_8wekyb3d8bbwe",
        });

        var result = await _service.GetInstalledAppxPackageNamesViaWmiAsync();

        result.Should().Contain("Microsoft.BingSearch");
    }

    [Fact]
    public async Task GetInstalledAppxPackageNamesViaWmiAsync_NothingReported_ReturnsEmptySet()
    {
        var result = await _service.GetInstalledAppxPackageNamesViaWmiAsync();

        result.Should().BeEmpty();
    }
}
