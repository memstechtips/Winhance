using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SystemSettingsDiscoveryServiceTrayIconDetectionTests
{
    private const string TrayKeyPath = @"HKEY_CURRENT_USER\Control Panel\NotifyIconSettings";

    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IPowerSettingsQueryService> _powerQuery = new();
    private readonly Mock<ISpecialDiscoveryRegistry> _discoveryRegistry = new();
    private readonly Mock<IScheduledTaskService> _scheduledTask = new();
    private readonly Mock<ISystemRestoreService> _systemRestore = new();

    private SystemSettingsDiscoveryService NewService()
    {
        _discoveryRegistry.Setup(r => r.All).Returns(Array.Empty<ISpecialSettingHandler>());
        return new SystemSettingsDiscoveryService(
            _registry.Object,
            _log.Object,
            _powerQuery.Object,
            _discoveryRegistry.Object,
            _scheduledTask.Object,
            _systemRestore.Object);
    }

    private static SettingDefinition TraySetting() => new()
    {
        Id = "taskbar-system-tray-icons-11",
        Name = "x",
        Description = "x",
        InputType = InputType.Selection,
        DetectionType = DetectionType.SystemTrayIcons,
        ComboBox = new ComboBoxMetadata
        {
            Options = new List<ComboBoxOption>
            {
                new() { DisplayName = "Show all", Script = ScriptOption.Enabled },
                new() { DisplayName = "Hide all", Script = ScriptOption.Disabled },
                new() { DisplayName = "Custom",   Script = ScriptOption.None },
            }
        },
    };

    [Fact]
    public async Task Detect_AllPromoted_ReturnsShowAllIndex()
    {
        _registry.Setup(r => r.GetSubKeyNames(TrayKeyPath)).Returns(new[] { "A", "B" });
        _registry.Setup(r => r.GetValue($@"{TrayKeyPath}\A", "IsPromoted")).Returns((object?)1);
        _registry.Setup(r => r.GetValue($@"{TrayKeyPath}\B", "IsPromoted")).Returns((object?)1);

        var service = NewService();
        var result = await service.GetSettingStatesAsync(new[] { TraySetting() });

        result["taskbar-system-tray-icons-11"].CurrentValue.Should().Be(0);
    }

    [Fact]
    public async Task Detect_NonePromoted_ReturnsHideAllIndex()
    {
        _registry.Setup(r => r.GetSubKeyNames(TrayKeyPath)).Returns(new[] { "A", "B" });
        _registry.Setup(r => r.GetValue($@"{TrayKeyPath}\A", "IsPromoted")).Returns((object?)0);
        _registry.Setup(r => r.GetValue($@"{TrayKeyPath}\B", "IsPromoted")).Returns((object?)0);

        var service = NewService();
        var result = await service.GetSettingStatesAsync(new[] { TraySetting() });

        result["taskbar-system-tray-icons-11"].CurrentValue.Should().Be(1);
    }

    [Fact]
    public async Task Detect_Mixed_ReturnsCustomIndex()
    {
        _registry.Setup(r => r.GetSubKeyNames(TrayKeyPath)).Returns(new[] { "A", "B" });
        _registry.Setup(r => r.GetValue($@"{TrayKeyPath}\A", "IsPromoted")).Returns((object?)1);
        _registry.Setup(r => r.GetValue($@"{TrayKeyPath}\B", "IsPromoted")).Returns((object?)0);

        var service = NewService();
        var result = await service.GetSettingStatesAsync(new[] { TraySetting() });

        result["taskbar-system-tray-icons-11"].CurrentValue.Should().Be(2);
    }

    [Fact]
    public async Task Detect_NoSubKeys_ReturnsCustomIndex()
    {
        _registry.Setup(r => r.GetSubKeyNames(TrayKeyPath)).Returns(Array.Empty<string>());

        var service = NewService();
        var result = await service.GetSettingStatesAsync(new[] { TraySetting() });

        result["taskbar-system-tray-icons-11"].CurrentValue.Should().Be(2);
    }
}
