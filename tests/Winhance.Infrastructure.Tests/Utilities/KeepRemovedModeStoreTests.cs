using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Utilities;

public class KeepRemovedModeStoreTests
{
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<IFileSystemService> _files = new();

    public KeepRemovedModeStoreTests() =>
        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => Path.Combine(parts));

    [Fact]
    public void Read_StoredMode_Wins()
    {
        _registry.Setup(r => r.GetValue(KeepRemovedModeStore.Key, KeepRemovedModeStore.ModeValue)).Returns("Scripts");

        KeepRemovedModeStore.Read(_registry.Object, _files.Object).Should().Be(KeepRemovedMode.Scripts);
    }

    [Fact]
    public void Read_NumericTextOutsideTheEnum_FallsBack()
    {
        _registry.Setup(r => r.GetValue(KeepRemovedModeStore.Key, KeepRemovedModeStore.ModeValue)).Returns("7");

        KeepRemovedModeStore.Read(_registry.Object, _files.Object).Should().Be(KeepRemovedMode.Winhance);
    }

    [Fact]
    public void Read_NothingStoredAndNoScripts_IsWinhance()
    {
        KeepRemovedModeStore.Read(_registry.Object, _files.Object).Should().Be(KeepRemovedMode.Winhance);
    }

    [Fact]
    public void Read_NothingStoredAndAScriptOnDisk_IsScripts()
    {
        _files.Setup(f => f.FileExists(It.Is<string>(path => path.EndsWith("OneDriveRemoval.ps1")))).Returns(true);

        KeepRemovedModeStore.Read(_registry.Object, _files.Object).Should().Be(KeepRemovedMode.Scripts);
    }

    [Theory]
    [InlineData("Mode", true)]
    [InlineData("enabled", true)]
    [InlineData("windows-app-edge", false)]
    public void IsSetting_SeparatesTheTwoSettingsFromKeptApps(string name, bool expected)
    {
        KeepRemovedModeStore.IsSetting(name).Should().Be(expected);
    }
}
