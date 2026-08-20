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
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows NT")]
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
        WindowsRegistryService.ProtectedSubKeyRoots.Should().Contain(@"SOFTWARE\Microsoft\Windows NT");
        WindowsRegistryService.ProtectedSubKeyRoots.Should().Contain(@"SYSTEM\CurrentControlSet");
        WindowsRegistryService.ProtectedSubKeyRoots.Should().Contain(@"SOFTWARE\Policies");
    }

    [Theory]
    [InlineData(@"SOFTWARE\Microsoft\Windows")]
    [InlineData(@"SOFTWARE\Microsoft\Windows NT")]
    [InlineData(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")]
    [InlineData(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList")]
    [InlineData(@"SOFTWARE\WOW6432Node\Microsoft\Windows NT")]
    [InlineData(@"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion")]
    [InlineData(@"SOFTWARE\Policies")]
    [InlineData(@"SYSTEM\CurrentControlSet")]
    [InlineData(@"SYSTEM\CurrentControlSet\Services")]
    [InlineData(@"software\microsoft\windows nt\currentversion")]
    public void IsProtectedSubKeyPath_ProtectedRootAndWindowsNtDescendants_ReturnsTrue(string subKeyPath)
    {
        WindowsRegistryService.IsProtectedSubKeyPath(subKeyPath).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"SOFTWARE\Winhance")]
    [InlineData(@"SOFTWARE\Microsoft")]
    [InlineData(@"SOFTWARE\Microsoft\WindowsPowerShell")]
    [InlineData(@"SOFTWARE\Microsoft\Windows Defender")]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion")]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}")]
    [InlineData(@"SOFTWARE\Policies\Microsoft")]
    [InlineData(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection")]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection")]
    [InlineData(@"SYSTEM")]
    [InlineData(@"SYSTEM\ControlSet001")]
    [InlineData(@"SYSTEM\CurrentControlSet\Services\Winmgmt")]
    public void IsProtectedSubKeyPath_UnrelatedAndCatalogDeletePaths_ReturnsFalse(string subKeyPath)
    {
        WindowsRegistryService.IsProtectedSubKeyPath(subKeyPath).Should().BeFalse();
    }

    [Fact]
    public void DeleteKey_NonExistentDeepPath_ReturnsTrue()
    {
        // Non-existent keys return true (nothing to delete)
        var result = _sut.DeleteKey(@"HKCU\Software\Winhance\TestKey\SubKey");
        result.Should().BeTrue();
    }

    [Fact]
    public void DeleteKey_NonExistentPolicyPath_ReturnsTrue()
    {
        var result = _sut.DeleteKey(@"HKCU\SOFTWARE\Policies\Microsoft\Windows\DataCollection\WinhanceDoesNotExist");
        result.Should().BeTrue();
    }

    [Fact]
    public void DeleteKey_WindowsNtDescendant_ReturnsFalseEvenWhenMissing()
    {
        var result = _sut.DeleteKey(@"HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\WinhanceDoesNotExist");
        result.Should().BeFalse();
    }

    [Fact]
    public void DeleteKey_WindowsNtDescendant_ReturnsFalseUnderOtsRedirect()
    {
        _mockInteractiveUser.Setup(x => x.IsOtsElevation).Returns(true);
        _mockInteractiveUser.Setup(x => x.InteractiveUserSid).Returns("S-1-5-21-1-2-3-1001");

        var otsSut = new WindowsRegistryService(_mockLog.Object, _mockInteractiveUser.Object);
        var result = otsSut.DeleteKey(@"HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\WinhanceDoesNotExist");

        result.Should().BeFalse();
    }

}
