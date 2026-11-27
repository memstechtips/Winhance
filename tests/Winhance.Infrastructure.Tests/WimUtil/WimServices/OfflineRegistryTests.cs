using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class OfflineRegistryTests
{
    private const string S = @"HKEY_LOCAL_MACHINE\WH_SOFTWARE_t";
    private const string Y = @"HKEY_LOCAL_MACHINE\WH_SYSTEM_t";
    private const string U = @"HKEY_LOCAL_MACHINE\WH_NTUSER_t";

    private readonly Mock<IWindowsRegistryService> _live = new();
    private readonly OfflineRegistry _registry;

    public OfflineRegistryTests()
    {
        _registry = new OfflineRegistry(_live.Object, new FakeHiveSession(), Mock.Of<IRegistryHiveNative>(), Mock.Of<ILogService>());
    }

    [Theory]
    [InlineData(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Foo", S + @"\Policies\Foo")]
    [InlineData(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Foo", S + @"\Microsoft\Foo")]
    [InlineData(@"HKLM\SOFTWARE\Foo", S + @"\Foo")]
    [InlineData(@"HKLM:\SOFTWARE\Foo", S + @"\Foo")]
    [InlineData(@"HKEY_LOCAL_MACHINE\SOFTWARE", S)]
    [InlineData(@"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\Foo", S + @"\Classes\CLSID\Foo")]
    [InlineData(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Foo", Y + @"\ControlSet001\Services\Foo")]
    [InlineData(@"HKLM:\SYSTEM\CurrentControlSet\Control\Foo", Y + @"\ControlSet001\Control\Foo")]
    [InlineData(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\Foo", Y + @"\ControlSet001\Services\Foo")]
    [InlineData(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet", Y + @"\ControlSet001")]
    [InlineData(@"HKLM\SYSTEM\Setup\LabConfig", Y + @"\Setup\LabConfig")]
    [InlineData(@"HKEY_CURRENT_USER\Software\Microsoft\Foo", U + @"\Software\Microsoft\Foo")]
    [InlineData(@"HKEY_CURRENT_USER\Control Panel\Desktop", U + @"\Control Panel\Desktop")]
    [InlineData(@"HKCU\Software\Foo", U + @"\Software\Foo")]
    [InlineData(@"HKCU:\Control Panel\Desktop", U + @"\Control Panel\Desktop")]
    public void Map_RewritesEachRootFormOntoTheLoadedHive(string path, string expected)
    {
        _registry.Map(path).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"HKEY_CLASSES_ROOT\.txt")]
    [InlineData(@"HKEY_USERS\.DEFAULT\Control Panel\Keyboard")]
    [InlineData(@"HKU:\.DEFAULT\Foo")]
    [InlineData(@"HKEY_CURRENT_CONFIG\Foo")]
    [InlineData(@"HKEY_LOCAL_MACHINE\SAM\Foo")]
    [InlineData(@"HKLM\HARDWARE\Foo")]
    [InlineData(@"HKEY_CURRENT_USER\Software\Classes\CLSID\Foo")]
    [InlineData(@"HKCU:\Software\Classes")]
    public void Map_LeavesARootWithNoLoadedHiveUnmapped(string path)
    {
        _registry.Map(path).Should().BeNull();
    }

    [Fact]
    public void SetValue_OnAnUnmappedRoot_IsRefusedAndNeverReachesTheLiveRegistry()
    {
        var written = _registry.SetValue(@"HKEY_CLASSES_ROOT\.txt", "Foo", 1, RegistryValueKind.DWord);

        written.Should().BeFalse();
        _registry.Unmapped.Should().ContainSingle().Which.Should().Be(@"HKEY_CLASSES_ROOT\.txt");
        _registry.Refusals.Should().Be(1);
        _live.VerifyNoOtherCalls();
    }

    [Fact]
    public void SetValue_OnAMappedRoot_WritesToTheLoadedHive()
    {
        _live.Setup(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>())).Returns(true);

        _registry.SetValue(@"HKEY_CURRENT_USER\Software\Foo", "Bar", 1, RegistryValueKind.DWord).Should().BeTrue();

        _live.Verify(r => r.SetValue(U + @"\Software\Foo", "Bar", 1, RegistryValueKind.DWord), Times.Once);
        _registry.Unmapped.Should().BeEmpty();
    }

    [Fact]
    public void DeleteKey_OnAProtectedBranch_IsRefusedEvenThoughTheMountNameHidesIt()
    {
        _registry.DeleteKey(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft").Should().BeFalse();

        _live.Verify(r => r.DeleteKey(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetSubKeyNames_OnAnUnmappedRoot_IsEmpty()
    {
        _registry.GetSubKeyNames(@"HKEY_USERS\.DEFAULT\Foo").Should().BeEmpty();
    }
}
