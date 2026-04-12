using System.Reflection;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
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

    // ── CompareValues (private static, tested via reflection) ──

    private static bool InvokeCompareValues(object? current, object? desired)
    {
        var method = typeof(WindowsRegistryService)
            .GetMethod("CompareValues", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, new[] { current, desired })!;
    }

    [Fact]
    public void CompareValues_IntToInt_Equal_ReturnsTrue()
    {
        InvokeCompareValues(5, 5).Should().BeTrue();
    }

    [Fact]
    public void CompareValues_IntToInt_NotEqual_ReturnsFalse()
    {
        InvokeCompareValues(5, 10).Should().BeFalse();
    }

    [Fact]
    public void CompareValues_IntToLong_Equal_ReturnsTrue()
    {
        InvokeCompareValues(5, 5L).Should().BeTrue();
    }

    [Fact]
    public void CompareValues_LongToInt_Equal_ReturnsTrue()
    {
        InvokeCompareValues(5L, 5).Should().BeTrue();
    }

    [Fact]
    public void CompareValues_LongToLong_Equal_ReturnsTrue()
    {
        InvokeCompareValues(5L, 5L).Should().BeTrue();
    }

    [Fact]
    public void CompareValues_IntToLong_NotEqual_ReturnsFalse()
    {
        InvokeCompareValues(5, 10L).Should().BeFalse();
    }

    [Fact]
    public void CompareValues_LongToInt_NotEqual_ReturnsFalse()
    {
        InvokeCompareValues(5L, 10).Should().BeFalse();
    }

    [Fact]
    public void CompareValues_StringToString_CaseInsensitive_ReturnsTrue()
    {
        InvokeCompareValues("Hello", "hello").Should().BeTrue();
    }

    [Fact]
    public void CompareValues_StringToString_NotEqual_ReturnsFalse()
    {
        InvokeCompareValues("Hello", "World").Should().BeFalse();
    }

    [Fact]
    public void CompareValues_ByteArrayToByteArray_Equal_ReturnsTrue()
    {
        InvokeCompareValues(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }).Should().BeTrue();
    }

    [Fact]
    public void CompareValues_ByteArrayToByteArray_NotEqual_ReturnsFalse()
    {
        InvokeCompareValues(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }).Should().BeFalse();
    }

    [Fact]
    public void CompareValues_NullToNull_ReturnsTrue()
    {
        InvokeCompareValues(null, null).Should().BeTrue();
    }

    [Fact]
    public void CompareValues_NullToNonNull_ReturnsFalse()
    {
        InvokeCompareValues(null, 5).Should().BeFalse();
    }

    [Fact]
    public void CompareValues_NonNullToNull_ReturnsFalse()
    {
        InvokeCompareValues(5, null).Should().BeFalse();
    }

    // ── GetHiveFromPath (private, tested via reflection) ──

    private Microsoft.Win32.RegistryKey InvokeGetHiveFromPath(string keyPath)
    {
        var method = typeof(WindowsRegistryService)
            .GetMethod("GetHiveFromPath", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Microsoft.Win32.RegistryKey)method.Invoke(_sut, new object[] { keyPath })!;
    }

    [Theory]
    [InlineData("HKEY_CURRENT_USER\\Software\\Test")]
    [InlineData("HKCU\\Software\\Test")]
    public void GetHiveFromPath_ValidHKCU_ReturnsCurrentUser(string keyPath)
    {
        var result = InvokeGetHiveFromPath(keyPath);
        result.Should().Be(Microsoft.Win32.Registry.CurrentUser);
    }

    [Theory]
    [InlineData("HKEY_LOCAL_MACHINE\\Software\\Test")]
    [InlineData("HKLM\\Software\\Test")]
    public void GetHiveFromPath_ValidHKLM_ReturnsLocalMachine(string keyPath)
    {
        var result = InvokeGetHiveFromPath(keyPath);
        result.Should().Be(Microsoft.Win32.Registry.LocalMachine);
    }

    [Theory]
    [InlineData("HKEY_CLASSES_ROOT\\Software\\Test")]
    [InlineData("HKCR\\Software\\Test")]
    public void GetHiveFromPath_ValidHKCR_ReturnsClassesRoot(string keyPath)
    {
        var result = InvokeGetHiveFromPath(keyPath);
        result.Should().Be(Microsoft.Win32.Registry.ClassesRoot);
    }

    [Theory]
    [InlineData("HKEY_USERS\\Software\\Test")]
    [InlineData("HKU\\Software\\Test")]
    public void GetHiveFromPath_ValidHKU_ReturnsUsers(string keyPath)
    {
        var result = InvokeGetHiveFromPath(keyPath);
        result.Should().Be(Microsoft.Win32.Registry.Users);
    }

    [Theory]
    [InlineData("HKEY_CURRENT_CONFIG\\Software\\Test")]
    [InlineData("HKCC\\Software\\Test")]
    public void GetHiveFromPath_ValidHKCC_ReturnsCurrentConfig(string keyPath)
    {
        var result = InvokeGetHiveFromPath(keyPath);
        result.Should().Be(Microsoft.Win32.Registry.CurrentConfig);
    }

    [Theory]
    [InlineData("INVALID_HIVE\\Software\\Test")]
    [InlineData("HKLM_TYPO\\Software\\Test")]
    [InlineData("BOGUS\\Software\\Test")]
    public void GetHiveFromPath_InvalidHive_ThrowsArgumentException(string keyPath)
    {
        var act = () => InvokeGetHiveFromPath(keyPath);

        // Reflection wraps the inner exception in TargetInvocationException
        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentException>()
            .WithMessage("*Unrecognized registry hive*");
    }

    // ── DeleteKey safeguards (BP-3) ──

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

    // ── IsRegistryValueInEnabledState ──

    private static RegistrySetting CreateTestSetting(
        object?[]? enabledValue = null,
        object?[]? disabledValue = null)
    {
        return new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "TestValue",
            ValueType = Microsoft.Win32.RegistryValueKind.DWord,
            EnabledValue = enabledValue,
            DisabledValue = disabledValue,
            RecommendedValue = null,
            DefaultValue = null
        };
    }

    [Fact]
    public void IsRegistryValueInEnabledState_NullSetting_ReturnsFalse()
    {
        _sut.IsRegistryValueInEnabledState(null!, null, false).Should().BeFalse();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_ValueNotExists_EnabledIncludesAbsence_ReturnsTrue()
    {
        // Game Mode scenario: value doesn't exist, EnabledValue=[1, null] → enabled (absence matches)
        var setting = CreateTestSetting(enabledValue: [1, null], disabledValue: [0]);
        _sut.IsRegistryValueInEnabledState(setting, null, false).Should().BeTrue();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_ValueNotExists_EnabledValueSet_ReturnsFalse()
    {
        var setting = CreateTestSetting(enabledValue: [1], disabledValue: [0]);
        _sut.IsRegistryValueInEnabledState(setting, null, false).Should().BeFalse();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_EnabledValueNull_MatchesDisabledValue_ReturnsFalse()
    {
        var setting = CreateTestSetting(enabledValue: null, disabledValue: [0]);
        _sut.IsRegistryValueInEnabledState(setting, 0, true).Should().BeFalse();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_EnabledValueNull_DoesNotMatchDisabledValue_ReturnsTrue()
    {
        var setting = CreateTestSetting(enabledValue: null, disabledValue: [0]);
        _sut.IsRegistryValueInEnabledState(setting, 1, true).Should().BeTrue();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_EnabledValueNull_BoolFalse_ReturnsFalse()
    {
        // Pre-processed BitMask where false means "bit not set"
        var setting = CreateTestSetting();
        _sut.IsRegistryValueInEnabledState(setting, false, true).Should().BeFalse();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_EnabledValueNull_BoolTrue_ReturnsTrue()
    {
        // Pre-processed BitMask where true means "bit is set"
        var setting = CreateTestSetting();
        _sut.IsRegistryValueInEnabledState(setting, true, true).Should().BeTrue();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_MatchesEnabledValue_ReturnsTrue()
    {
        var setting = CreateTestSetting(enabledValue: [1], disabledValue: [0]);
        _sut.IsRegistryValueInEnabledState(setting, 1, true).Should().BeTrue();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_MatchesDisabledValue_ReturnsFalse()
    {
        var setting = CreateTestSetting(enabledValue: [1], disabledValue: [0]);
        _sut.IsRegistryValueInEnabledState(setting, 0, true).Should().BeFalse();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_MatchesNeither_ReturnsFalse()
    {
        var setting = CreateTestSetting(enabledValue: [1], disabledValue: [0]);
        _sut.IsRegistryValueInEnabledState(setting, 99, true).Should().BeFalse();
    }

    // ── CompositeStringKey handling ──

    [Fact]
    public void IsRegistryValueInEnabledState_CompositeStringKey_MatchesEnabled_ReturnsTrue()
    {
        var setting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "DirectXUserGlobalSettings",
            CompositeStringKey = "SwapEffectUpgradeEnable",
            EnabledValue = ["1"],
            DisabledValue = ["0"],
            DefaultValue = "1",
            ValueType = Microsoft.Win32.RegistryValueKind.String,
            RecommendedValue = null
        };
        _sut.IsRegistryValueInEnabledState(setting, "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;", true)
            .Should().BeTrue();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_CompositeStringKey_MatchesDisabled_ReturnsFalse()
    {
        var setting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "DirectXUserGlobalSettings",
            CompositeStringKey = "SwapEffectUpgradeEnable",
            EnabledValue = ["1"],
            DisabledValue = ["0"],
            DefaultValue = "1",
            ValueType = Microsoft.Win32.RegistryValueKind.String,
            RecommendedValue = null
        };
        _sut.IsRegistryValueInEnabledState(setting, "SwapEffectUpgradeEnable=0;VRROptimizeEnable=1;", true)
            .Should().BeFalse();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_CompositeStringKey_SubKeyAbsent_UsesDefaultValue()
    {
        var setting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "DirectXUserGlobalSettings",
            CompositeStringKey = "SwapEffectUpgradeEnable",
            EnabledValue = ["1"],
            DisabledValue = ["0"],
            DefaultValue = "1",
            ValueType = Microsoft.Win32.RegistryValueKind.String,
            RecommendedValue = null
        };
        // Composite string exists but doesn't contain our sub-key — DefaultValue "1" == EnabledValue "1"
        _sut.IsRegistryValueInEnabledState(setting, "VRROptimizeEnable=0;", true)
            .Should().BeTrue();
    }

    [Fact]
    public void IsRegistryValueInEnabledState_CompositeStringKey_NullValue_UsesDefaultValue()
    {
        var setting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "DirectXUserGlobalSettings",
            CompositeStringKey = "SwapEffectUpgradeEnable",
            EnabledValue = ["1"],
            DisabledValue = ["0"],
            DefaultValue = "1",
            ValueType = Microsoft.Win32.RegistryValueKind.String,
            RecommendedValue = null
        };
        // Value doesn't exist (key absent) — DefaultValue "1" == EnabledValue "1"
        _sut.IsRegistryValueInEnabledState(setting, null, false)
            .Should().BeTrue();
    }
}
