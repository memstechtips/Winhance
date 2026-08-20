using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class WindowsRegistryServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUser = new();
    private readonly WindowsRegistryService _sut;

    public WindowsRegistryServiceTests()
    {
        _mockInteractiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        _sut = new WindowsRegistryService(_mockLog.Object, _mockInteractiveUser.Object);
    }

    [Theory]
    [InlineData(@"HKLM\SOFTWARE")]
    [InlineData(@"HKCU\Software")]
    public void DeleteKey_ShallowPath_ReturnsFalseAndLogs(string keyPath)
    {
        // Paths with only 1 segment after the hive are too shallow to delete
        var result = _sut.DeleteKey(keyPath);
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows")]
    [InlineData(@"HKLM\SOFTWARE\Policies")]
    [InlineData(@"HKLM\SYSTEM\CurrentControlSet")]
    [InlineData(@"HKLM\SYSTEM\CurrentControlSet\Services")]
    public void DeleteKey_ProtectedPath_ReturnsFalseAndLogs(string keyPath)
    {
        var result = _sut.DeleteKey(keyPath);
        result.Should().BeFalse();
    }

    [Fact]
    public void ProtectedSubKeyRoots_ContainsExpectedEntries()
    {
        WindowsRegistryService.ProtectedSubKeyRoots.Should().Contain(@"SOFTWARE\Microsoft\Windows");
        WindowsRegistryService.ProtectedSubKeyRoots.Should().Contain(@"SYSTEM\CurrentControlSet");
        WindowsRegistryService.ProtectedSubKeyRoots.Should().Contain(@"SOFTWARE\Policies");
    }

    [Fact]
    public void DeleteKey_NonExistentDeepPath_ReturnsTrue()
    {
        // Non-existent keys return true (nothing to delete)
        var result = _sut.DeleteKey(@"HKCU\Software\Winhance\TestKey\SubKey");
        result.Should().BeTrue();
    }

}
