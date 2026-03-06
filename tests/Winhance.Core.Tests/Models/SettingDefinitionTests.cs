using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class SettingDefinitionTests
{
    private static SettingDefinition CreateMinimal(string id = "test-id") => new()
    {
        Id = id,
        Name = "Test Setting",
        Description = "A test setting",
    };

    [Fact]
    public void Construction_WithRequiredProperties_Succeeds()
    {
        var setting = CreateMinimal();

        setting.Id.Should().Be("test-id");
        setting.Name.Should().Be("Test Setting");
        setting.Description.Should().Be("A test setting");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var setting = CreateMinimal();

        setting.InputType.Should().Be(InputType.Toggle);
        setting.RequiresConfirmation.Should().BeFalse();
        setting.ActionCommand.Should().BeNull();
        setting.SupportedBuildRanges.Should().BeEmpty();
        setting.ScheduledTaskSettings.Should().BeEmpty();
        setting.PowerShellScripts.Should().BeEmpty();
        setting.RegContents.Should().BeEmpty();
        setting.PowerCfgSettings.Should().BeNull();
        setting.NativePowerApiSettings.Should().BeEmpty();
        setting.Dependencies.Should().BeEmpty();
        setting.AutoEnableSettingIds.Should().BeNull();
        setting.RequiresBattery.Should().BeFalse();
        setting.RequiresLid.Should().BeFalse();
        setting.RequiresDesktop.Should().BeFalse();
        setting.RequiresBrightnessSupport.Should().BeFalse();
        setting.RequiresHybridSleepCapable.Should().BeFalse();
        setting.ValidateExistence.Should().BeTrue();
        setting.ParentSettingId.Should().BeNull();
        setting.RequiresAdvancedUnlock.Should().BeFalse();
        setting.IsWindows11Only.Should().BeFalse();
        setting.IsWindows10Only.Should().BeFalse();
        setting.MinimumBuildNumber.Should().BeNull();
        setting.MaximumBuildNumber.Should().BeNull();
        setting.GroupName.Should().BeNull();
        setting.Icon.Should().BeNull();
        setting.IconPack.Should().Be("Material");
        setting.RequiresRestart.Should().BeFalse();
        setting.RestartProcess.Should().BeNull();
        setting.RestartService.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var setting1 = CreateMinimal("same-id");
        var setting2 = CreateMinimal("same-id");

        setting1.Should().Be(setting2);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var setting1 = CreateMinimal("id-1");
        var setting2 = CreateMinimal("id-2");

        setting1.Should().NotBe(setting2);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        var original = CreateMinimal();
        var modified = original with { RequiresBattery = true, RequiresConfirmation = true };

        modified.Id.Should().Be(original.Id);
        modified.RequiresBattery.Should().BeTrue();
        modified.RequiresConfirmation.Should().BeTrue();
        original.RequiresBattery.Should().BeFalse();
        original.RequiresConfirmation.Should().BeFalse();
    }

    [Fact]
    public void RegistrySettings_DefaultsToEmpty()
    {
        var setting = CreateMinimal();

        setting.RegistrySettings.Should().BeEmpty();
    }

    [Fact]
    public void ImplementsISettingItem()
    {
        var setting = CreateMinimal();

        setting.Should().BeAssignableTo<Winhance.Core.Features.Common.Interfaces.ISettingItem>();
    }
}
