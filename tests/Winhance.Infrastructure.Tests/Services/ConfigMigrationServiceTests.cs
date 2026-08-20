using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ConfigMigrationServiceTests
{
    private readonly Mock<ILogService> _logMock;
    private readonly ConfigMigrationService _sut;

    public ConfigMigrationServiceTests()
    {
        _logMock = new Mock<ILogService>();
        _sut = new ConfigMigrationService(_logMock.Object);
    }

    private static WinhanceConfigFile CreateConfigWithCustomizeItem(ConfigurationItem item)
    {
        return new WinhanceConfigFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["TaskbarCustomizations"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem> { item },
                    },
                },
            },
        };
    }

    [Fact]
    public void MigrateConfig_NullConfig_DoesNotThrow()
    {
        var action = () => _sut.MigrateConfig(null!);

        action.Should().NotThrow();
    }

    [Fact]
    public void MigrateConfig_NoMigrateableItems_NoChanges()
    {
        var item = new ConfigurationItem
        {
            Id = "some-other-setting",
            Name = "Some Setting",
            InputType = InputType.Toggle,
            IsSelected = true,
        };

        var config = CreateConfigWithCustomizeItem(item);

        _sut.MigrateConfig(config);

        item.InputType.Should().Be(InputType.Toggle);
        item.IsSelected.Should().BeTrue();
        item.SelectedIndex.Should().BeNull();
    }

    [Fact]
    public void MigrateConfig_TaskbarTransparentToggleSelected_MigratedToSelectionIndex1()
    {
        var item = new ConfigurationItem
        {
            Id = "taskbar-transparent",
            Name = "Taskbar Transparency",
            InputType = InputType.Toggle,
            IsSelected = true,
        };

        var config = CreateConfigWithCustomizeItem(item);

        _sut.MigrateConfig(config);

        item.InputType.Should().Be(InputType.Selection);
        item.SelectedIndex.Should().Be(1);
        item.IsSelected.Should().BeNull();
    }

    [Fact]
    public void MigrateConfig_TaskbarTransparentToggleNotSelected_MigratedToSelectionIndex0()
    {
        var item = new ConfigurationItem
        {
            Id = "taskbar-transparent",
            Name = "Taskbar Transparency",
            InputType = InputType.Toggle,
            IsSelected = false,
        };

        var config = CreateConfigWithCustomizeItem(item);

        _sut.MigrateConfig(config);

        item.InputType.Should().Be(InputType.Selection);
        item.SelectedIndex.Should().Be(0);
        item.IsSelected.Should().BeNull();
    }

    [Fact]
    public void MigrateConfig_TaskbarTransparentAlreadySelection_NotMigrated()
    {
        var item = new ConfigurationItem
        {
            Id = "taskbar-transparent",
            Name = "Taskbar Transparency",
            InputType = InputType.Selection,
            SelectedIndex = 2,
        };

        var config = CreateConfigWithCustomizeItem(item);

        _sut.MigrateConfig(config);

        item.InputType.Should().Be(InputType.Selection);
        item.SelectedIndex.Should().Be(2);
    }

    [Fact]
    public void MigrateConfig_LogsMigration()
    {
        var item = new ConfigurationItem
        {
            Id = "taskbar-transparent",
            Name = "Taskbar Transparency",
            InputType = InputType.Toggle,
            IsSelected = true,
        };

        var config = CreateConfigWithCustomizeItem(item);

        _sut.MigrateConfig(config);

        _logMock.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(msg =>
                msg.Contains("taskbar-transparent") && msg.Contains("Toggle") && msg.Contains("Selection")), null),
            Times.Once);
    }

    [Fact]
    public void MigrateConfig_OptimizeSection_MigratesItems()
    {
        var item = new ConfigurationItem
        {
            Id = "taskbar-transparent",
            Name = "Taskbar Transparency",
            InputType = InputType.Toggle,
            IsSelected = true,
        };

        var config = new WinhanceConfigFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["SomeOptimization"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem> { item },
                    },
                },
            },
        };

        _sut.MigrateConfig(config);

        item.InputType.Should().Be(InputType.Selection);
        item.SelectedIndex.Should().Be(1);
    }

    [Fact]
    public void MigrateConfig_WindowsAppsSection_MigratesItems()
    {
        var item = new ConfigurationItem
        {
            Id = "taskbar-transparent",
            Name = "Taskbar Transparency",
            InputType = InputType.Toggle,
            IsSelected = false,
        };

        var config = new WinhanceConfigFile
        {
            WindowsApps = new ConfigSection
            {
                Items = new List<ConfigurationItem> { item },
            },
        };

        _sut.MigrateConfig(config);

        item.InputType.Should().Be(InputType.Selection);
        item.SelectedIndex.Should().Be(0);
    }

    [Fact]
    public void MigrateConfig_ExternalAppsSection_MigratesItems()
    {
        var item = new ConfigurationItem
        {
            Id = "taskbar-transparent",
            Name = "Taskbar Transparency",
            InputType = InputType.Toggle,
            IsSelected = true,
        };

        var config = new WinhanceConfigFile
        {
            ExternalApps = new ConfigSection
            {
                Items = new List<ConfigurationItem> { item },
            },
        };

        _sut.MigrateConfig(config);

        item.InputType.Should().Be(InputType.Selection);
        item.SelectedIndex.Should().Be(1);
    }

    [Theory]
    [InlineData("explorer-customization-shortcut-suffix")]
    [InlineData("explorer-customization-shortcut-arrow")]
    public void MigrateConfig_ShortcutToggleSelected_MigratedToSelectionIndex1(string settingId)
    {
        var item = new ConfigurationItem
        {
            Id = settingId,
            Name = "Old Name",
            InputType = InputType.Toggle,
            IsSelected = true,
        };

        var config = CreateConfigWithCustomizeItem(item);

        _sut.MigrateConfig(config);

        item.InputType.Should().Be(InputType.Selection);
        item.SelectedIndex.Should().Be(1);
        item.IsSelected.Should().BeNull();
    }

    [Theory]
    [InlineData("explorer-customization-shortcut-suffix")]
    [InlineData("explorer-customization-shortcut-arrow")]
    public void MigrateConfig_ShortcutToggleNotSelected_MigratedToSelectionIndex0(string settingId)
    {
        var item = new ConfigurationItem
        {
            Id = settingId,
            Name = "Old Name",
            InputType = InputType.Toggle,
            IsSelected = false,
        };

        var config = CreateConfigWithCustomizeItem(item);

        _sut.MigrateConfig(config);

        item.InputType.Should().Be(InputType.Selection);
        item.SelectedIndex.Should().Be(0);
        item.IsSelected.Should().BeNull();
    }

    [Theory]
    [InlineData("explorer-customization-shortcut-suffix")]
    [InlineData("explorer-customization-shortcut-arrow")]
    public void MigrateConfig_ShortcutAlreadySelection_NotMigrated(string settingId)
    {
        var item = new ConfigurationItem
        {
            Id = settingId,
            Name = "New Name",
            InputType = InputType.Selection,
            SelectedIndex = 1,
        };

        var config = CreateConfigWithCustomizeItem(item);

        _sut.MigrateConfig(config);

        item.InputType.Should().Be(InputType.Selection);
        item.SelectedIndex.Should().Be(1);
    }

    [Fact]
    public void MigrateConfig_NullSections_DoesNotThrow()
    {
        var config = new WinhanceConfigFile
        {
            Customize = new FeatureGroupSection { Features = null! },
            Optimize = new FeatureGroupSection { Features = null! },
            WindowsApps = null!,
            ExternalApps = null!,
        };

        var action = () => _sut.MigrateConfig(config);

        action.Should().NotThrow();
    }

    [Fact]
    public void MigrateConfig_ItemWithNullId_SkippedGracefully()
    {
        var item = new ConfigurationItem
        {
            Id = null!,
            Name = "No ID",
            InputType = InputType.Toggle,
            IsSelected = true,
        };

        var config = CreateConfigWithCustomizeItem(item);

        var action = () => _sut.MigrateConfig(config);

        action.Should().NotThrow();
        item.InputType.Should().Be(InputType.Toggle);
    }

    [Theory]
    [InlineData("explorer-customization-thispc-folder-desktop-win10", "explorer-customization-thispc-folder-desktop")]
    [InlineData("explorer-customization-thispc-folder-documents-win10", "explorer-customization-thispc-folder-documents")]
    [InlineData("explorer-customization-thispc-folder-downloads-win10", "explorer-customization-thispc-folder-downloads")]
    [InlineData("explorer-customization-thispc-folder-music-win10", "explorer-customization-thispc-folder-music")]
    [InlineData("explorer-customization-thispc-folder-pictures-win10", "explorer-customization-thispc-folder-pictures")]
    [InlineData("explorer-customization-thispc-folder-videos-win10", "explorer-customization-thispc-folder-videos")]
    public void MigrateConfig_RetiredWin10ThisPcId_NormalizedToCanonical(string retired, string canonical)
    {
        var item = new ConfigurationItem
        {
            Id = retired,
            Name = "This PC Folder",
            InputType = InputType.Toggle,
            IsSelected = true,
        };

        var config = CreateConfigWithCustomizeItem(item);

        _sut.MigrateConfig(config);

        item.Id.Should().Be(canonical);
    }

    [Fact]
    public void MigrateConfig_AlreadyCanonicalThisPcId_IdUnchanged()
    {
        var item = new ConfigurationItem
        {
            Id = "explorer-customization-thispc-folder-desktop",
            Name = "This PC Folder",
            InputType = InputType.Toggle,
            IsSelected = true,
        };

        var config = CreateConfigWithCustomizeItem(item);

        _sut.MigrateConfig(config);

        item.Id.Should().Be("explorer-customization-thispc-folder-desktop");
    }

    [Fact]
    public void MigrateConfig_AliasNormalization_AppliesInOptimizeSection()
    {
        // Normalization lives in MigrateSection, so it applies to every section, not just Customize.
        var item = new ConfigurationItem
        {
            Id = "explorer-customization-thispc-folder-videos-win10",
            Name = "This PC Folder",
            InputType = InputType.Toggle,
            IsSelected = false,
        };

        var config = new WinhanceConfigFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["SomeFeature"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem> { item },
                    },
                },
            },
        };

        _sut.MigrateConfig(config);

        item.Id.Should().Be("explorer-customization-thispc-folder-videos");
    }
}
