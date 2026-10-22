using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
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
    public void ProtectedSubKeyRoots_ContainsEveryProtectedBranch()
    {
        WindowsRegistryService.ProtectedSubKeyRoots.Should().Contain(WindowsRegistryService.ProtectedBranches);
        WindowsRegistryService.ProtectedSubKeyRoots.Should().HaveCount(WindowsRegistryService.ProtectedBranches.Count + 3);
    }

    // Checked on the predicate, not DeleteKey: a gap here would let the test delete a live key.
    [Theory]
    [InlineData(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run")]
    [InlineData(@"SOFTWARE\Policies\Microsoft")]
    [InlineData(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion")]
    [InlineData(@"SYSTEM\CurrentControlSet\Control\Session Manager")]
    [InlineData(@"SYSTEM\CurrentControlSet\Services\WinDefend")]
    [InlineData(@"SYSTEM\ControlSet001\Services\WinDefend")]
    [InlineData(@"SYSTEM\ControlSet001")]
    public void IsProtected_KeyDirectlyUnderACriticalBranch_IsProtected(string subKeyPath)
    {
        WindowsRegistryService.IsProtected(subKeyPath).Should().BeTrue();
    }

    [Fact]
    public void IsProtected_EveryKeyTheCatalogDeletes_IsAllowed()
    {
        var deleted = SettingCatalog.All.SelectMany(s => s.Targets.OfType<RegTarget>()
                .Where(t => t.ValueName == null || t.IsGroupPolicy).SelectMany(t => t.Paths)
                .Concat(s.Effects.Concat(s.States.SelectMany(st => st.Effects)).OfType<RegistryWriteEffect>()
                    .Where(e => e.IsGroupPolicy).Select(e => e.Path)))
            .Select(path => path.Split('\\', 2)[1])
            .ToList();

        deleted.Should().NotBeEmpty().And.OnlyContain(path => !WindowsRegistryService.IsProtected(path));
    }

    [Fact]
    public void DeleteKey_NonExistentDeepPath_ReturnsTrue()
    {
        // Non-existent keys return true (nothing to delete)
        var result = _sut.DeleteKey(@"HKCU\Software\Winhance\TestKey\SubKey");
        result.Should().BeTrue();
    }

}
