using System.Reflection;
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
}
