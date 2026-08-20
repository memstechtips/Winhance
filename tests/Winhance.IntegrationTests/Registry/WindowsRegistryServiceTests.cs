using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.IntegrationTests.Registry;

[Trait("Category", "Integration")]
public class WindowsRegistryServiceTests : IDisposable
{
    private readonly string _testKeyRoot;
    private readonly WindowsRegistryService _service;

    public WindowsRegistryServiceTests()
    {
        var guid = Guid.NewGuid().ToString("N");
        _testKeyRoot = $@"Software\WinhanceIntegrationTests\{guid}";

        var logService = new Mock<ILogService>();
        var interactiveUserService = new Mock<IInteractiveUserService>();
        interactiveUserService.Setup(s => s.IsOtsElevation).Returns(false);

        _service = new WindowsRegistryService(logService.Object, interactiveUserService.Object);
    }

    private string TestPath(string subKey = "") =>
        string.IsNullOrEmpty(subKey)
            ? $@"HKCU\{_testKeyRoot}"
            : $@"HKCU\{_testKeyRoot}\{subKey}";

    [Fact]
    public void SetValue_DWord_ReadBackMatches()
    {
        var path = TestPath("DWordTest");

        var result = _service.SetValue(path, "TestDWord", 42, RegistryValueKind.DWord);
        var readBack = _service.GetValue(path, "TestDWord");

        result.Should().BeTrue();
        readBack.Should().Be(42);
    }

    [Fact]
    public void SetValue_String_ReadBackMatches()
    {
        var path = TestPath("StringTest");
        var testValue = "Hello, Winhance Integration Tests!";

        var result = _service.SetValue(path, "TestString", testValue, RegistryValueKind.String);
        var readBack = _service.GetValue(path, "TestString");

        result.Should().BeTrue();
        readBack.Should().Be(testValue);
    }

    [Fact]
    public void SetValue_Binary_ReadBackMatches()
    {
        var path = TestPath("BinaryTest");
        var testValue = new byte[] { 0x01, 0x02, 0x03, 0xFF };

        var result = _service.SetValue(path, "TestBinary", testValue, RegistryValueKind.Binary);
        var readBack = _service.GetValue(path, "TestBinary");

        result.Should().BeTrue();
        readBack.Should().BeOfType<byte[]>();
        ((byte[])readBack!).Should().BeEquivalentTo(testValue);
    }

    [Fact]
    public void KeyExists_AfterCreate_ReturnsTrue()
    {
        var path = TestPath("KeyExistsTest");
        _service.SetValue(path, "Marker", 1, RegistryValueKind.DWord);

        _service.KeyExists(path).Should().BeTrue();
    }

    [Fact]
    public void KeyExists_NonExistent_ReturnsFalse()
    {
        _service.KeyExists(TestPath("NonExistentKey_" + Guid.NewGuid().ToString("N")))
            .Should().BeFalse();
    }

    [Fact]
    public void DeleteValue_RemovesValue()
    {
        var path = TestPath("DeleteValueTest");
        _service.SetValue(path, "ToDelete", "gone", RegistryValueKind.String);
        _service.ValueExists(path, "ToDelete").Should().BeTrue();

        var result = _service.DeleteValue(path, "ToDelete");

        result.Should().BeTrue();
        _service.ValueExists(path, "ToDelete").Should().BeFalse();
    }

    [Fact]
    public void DeleteKey_RemovesSubKeyTree()
    {
        var parentPath = TestPath("DeleteKeyTest");
        var childPath = TestPath(@"DeleteKeyTest\Child");
        _service.SetValue(childPath, "Val", 1, RegistryValueKind.DWord);
        _service.KeyExists(parentPath).Should().BeTrue();

        var result = _service.DeleteKey(parentPath);

        result.Should().BeTrue();
        _service.KeyExists(parentPath).Should().BeFalse();
        _service.KeyExists(childPath).Should().BeFalse();
    }

    [Fact]
    public void GetSubKeyNames_ReturnsCreatedKeys()
    {
        var basePath = TestPath("SubKeyTest");
        _service.SetValue(TestPath(@"SubKeyTest\Alpha"), "x", 1, RegistryValueKind.DWord);
        _service.SetValue(TestPath(@"SubKeyTest\Beta"), "x", 1, RegistryValueKind.DWord);
        _service.SetValue(TestPath(@"SubKeyTest\Gamma"), "x", 1, RegistryValueKind.DWord);

        var subKeys = _service.GetSubKeyNames(basePath);

        subKeys.Should().Contain("Alpha");
        subKeys.Should().Contain("Beta");
        subKeys.Should().Contain("Gamma");
        subKeys.Should().HaveCount(3);
    }

    [Fact]
    public void ValueExists_AfterSet_ReturnsTrue()
    {
        var path = TestPath("ValueExistsTest");
        _service.SetValue(path, "Exists", "yes", RegistryValueKind.String);

        _service.ValueExists(path, "Exists").Should().BeTrue();
        _service.ValueExists(path, "DoesNotExist").Should().BeFalse();
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(
                _testKeyRoot, throwOnMissingSubKey: false);
        }
        catch
        {
            // Best effort cleanup
        }

        try
        {
            using var parent = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\WinhanceIntegrationTests", writable: false);
            if (parent?.SubKeyCount == 0)
            {
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(
                    @"Software\WinhanceIntegrationTests", throwOnMissingSubKey: false);
            }
        }
        catch
        {
            // Best effort cleanup
        }
        GC.SuppressFinalize(this);
    }
}
