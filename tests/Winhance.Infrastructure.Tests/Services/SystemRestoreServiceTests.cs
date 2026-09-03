using FluentAssertions;
using Moq;
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
    public void CDeviceId_WmiFallbackAgreesWithTheNativeLookup_WhenWmiCanAnswer()
    {
        var svc = new SystemRestoreService(_log.Object, new WmiManagementApi());

        var fromWmi = svc.QueryCDeviceIdFromWmi();

        // Win32_Volume returns nothing under the gate's service account, so this guards rather than
        // asserts. It is not a privilege rule: probed on a real desktop 2026-09-02, the same query
        // answers fine to a normal unelevated user, and returns a string identical to the native
        // one. The path Winhance actually takes is covered unconditionally above. When WMI does
        // answer, the two must match - IsEnabledForC cannot tell which source produced the string it
        // matches against SPP\Clients.
        if (fromWmi is not null)
        {
            svc.QueryCDeviceIdNative().Should().Be(fromWmi);
        }
    }
}
