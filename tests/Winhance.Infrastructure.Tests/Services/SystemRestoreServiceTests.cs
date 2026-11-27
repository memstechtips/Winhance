using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SystemRestoreServiceTests
{
    private readonly Mock<ILogService> _log = new();
    private readonly FakeWmiApi _wmiApi = new();

    [Fact]
    public void IsEnabledForC_DoesNotThrow_OnAnyEnvironment()
    {
        // Smoke: ensures the method short-circuits to false rather than propagating exceptions.
        // Full behavioural tests require an integration environment with a real C: volume and
        // registry state - the WMI half alone (C: DeviceID lookup) is covered below.
        var svc = new SystemRestoreService(_log.Object, _wmiApi);
        var act = () => svc.IsEnabledForC();
        act.Should().NotThrow();
    }

    [Fact]
    public void QueryCDeviceIdFromWmi_NoCVolumeReported_ReturnsNull()
    {
        // The WMI path is the fallback now, reached only when GetVolumeNameForVolumeMountPoint has
        // no answer (ReFS, SMB). Nothing registered in the fake, so it reports null and IsEnabledForC
        // falls through to Disabled.
        var svc = new SystemRestoreService(_log.Object, _wmiApi);

        svc.QueryCDeviceIdFromWmi().Should().BeNull();
    }

    [Fact]
    public void QueryCDeviceIdNative_ReturnsCsVolumeGuidPath()
    {
        // The trailing backslash is the part that matters: IsEnabledForC matches this string against
        // the SPP\Clients entries with StartsWith, and Win32_Volume.DeviceID carried one. Losing it
        // would not throw, it would just report System Restore as Disabled on every machine.
        var svc = new SystemRestoreService(_log.Object, _wmiApi);

        var deviceId = svc.QueryCDeviceIdNative();

        deviceId.Should().NotBeNullOrEmpty();
        deviceId.Should().StartWith(@"\\?\Volume{");
        deviceId.Should().EndWith(@"}\");
    }

    [Fact]
    public void QueryCDeviceIdNative_MountPointDoesNotExist_ReturnsNullAndFallsBack()
    {
        var svc = new SystemRestoreService(_log.Object, _wmiApi);

        svc.QueryCDeviceIdNative(@"Q:\winhance-no-such-mount-2026\").Should().BeNull();

        _log.Verify(
            l => l.Log(
                LogLevel.Info,
                It.Is<string>(m => m.Contains("falling back")),
                It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void CDeviceId_WmiFallbackAgreesWithTheNativeLookup()
    {
        // The two sources must produce the same string: IsEnabledForC cannot tell which one it is
        // matching against SPP\Clients.
        var svc = new SystemRestoreService(_log.Object, new WmiManagementApi());

        var fromWmi = svc.QueryCDeviceIdFromWmi();

        fromWmi.Should().NotBeNull();
        svc.QueryCDeviceIdNative().Should().Be(fromWmi);
    }
}
