using System.Security.Principal;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// TryGetSidFromWmi is one of three SID-detection strategies InteractiveUserService's constructor
// tries in order (Explorer token, WMI, WTS session); it's internal so these tests can exercise it
// directly instead of depending on which strategy the constructor's own chain happens to resolve
// through on this machine.
public class InteractiveUserServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly FakeWmiApi _wmiApi = new();

    private InteractiveUserService CreateSut() =>
        new(_mockLog.Object, _mockProcessExecutor.Object, _wmiApi);

    [Fact]
    public void TryGetSidFromWmi_NoUserNameReported_ReturnsNull()
    {
        var sut = CreateSut();

        sut.TryGetSidFromWmi().Should().BeNull();
    }

    [Fact]
    public void TryGetSidFromWmi_UserNameCannotBeTranslated_ReturnsNull()
    {
        _wmiApi.For("Win32_ComputerSystem").Add(new FakeWmiInstance
        {
            ["UserName"] = @"NOTAREALDOMAIN-2026\NotARealUser-2026",
        });
        var sut = CreateSut();

        sut.TryGetSidFromWmi().Should().BeNull();
    }

    [Fact]
    public void TryGetSidFromWmi_ReturnsTheAccountsSid_WhenTranslationSucceeds()
    {
        // The current test process's own identity is guaranteed to translate on any machine the
        // gate runs on, without depending on a specific fixture account existing.
        var currentUser = WindowsIdentity.GetCurrent();
        _wmiApi.For("Win32_ComputerSystem").Add(new FakeWmiInstance
        {
            ["UserName"] = currentUser.Name,
        });
        var sut = CreateSut();

        sut.TryGetSidFromWmi().Should().Be(currentUser.User?.Value);
    }
}
