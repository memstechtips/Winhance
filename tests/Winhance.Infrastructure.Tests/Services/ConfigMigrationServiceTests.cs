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

    private static UnifiedConfigurationFile CreateConfigWithCustomizeItem(ConfigurationItem item)
    {
        return new UnifiedConfigurationFile
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

        // Already a Selection, should not be changed
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

        var config = new UnifiedConfigurationFile
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

        var config = new UnifiedConfigurationFile
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

        var config = new UnifiedConfigurationFile
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

    [Fact]
    public void MigrateConfig_NullSections_DoesNotThrow()
    {
        var config = new UnifiedConfigurationFile
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
}
