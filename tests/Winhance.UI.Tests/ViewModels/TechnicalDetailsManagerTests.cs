using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class TechnicalDetailsManagerTests : IDisposable
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<IDispatcherService> _mockDispatcherService;
    private readonly Mock<IRegeditLauncher> _mockRegeditLauncher;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly Mock<ISubscriptionToken> _mockSubscriptionToken;
    private readonly ObservableCollection<TechnicalDetailRow> _details;
    private readonly List<Action<TooltipUpdatedEvent>> _capturedHandlers;

    private string _currentSettingId = "TestSetting";
    private bool _detailsChangedCalled;
    private TechnicalDetailsManager? _manager;

    public TechnicalDetailsManagerTests()
    {
        _mockLogService = new Mock<ILogService>();
        _mockDispatcherService = new Mock<IDispatcherService>();
        _mockRegeditLauncher = new Mock<IRegeditLauncher>();
        _mockEventBus = new Mock<IEventBus>();
        _mockSubscriptionToken = new Mock<ISubscriptionToken>();
        _details = new ObservableCollection<TechnicalDetailRow>();
        _capturedHandlers = new List<Action<TooltipUpdatedEvent>>();

        // Set up dispatcher to execute actions synchronously for testing
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        // Capture the event handler when Subscribe is called
        _mockEventBus
            .Setup(e => e.Subscribe<TooltipUpdatedEvent>(It.IsAny<Action<TooltipUpdatedEvent>>()))
            .Callback<Action<TooltipUpdatedEvent>>(handler => _capturedHandlers.Add(handler))
            .Returns(_mockSubscriptionToken.Object);
    }

    private TechnicalDetailsManager CreateManager(
        IRegeditLauncher? regeditLauncher = null,
        IEventBus? eventBus = null,
        bool useDefaultEventBus = true)
    {
        _detailsChangedCalled = false;
        _manager = new TechnicalDetailsManager(
            () => _currentSettingId,
            _details,
            () => _detailsChangedCalled = true,
            _mockLogService.Object,
            _mockDispatcherService.Object,
            regeditLauncher ?? _mockRegeditLauncher.Object,
            useDefaultEventBus ? (eventBus ?? _mockEventBus.Object) : null);
        return _manager;
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    // ──────────────────────────────────────────────────
    // Constructor
    // ──────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var manager = CreateManager();

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullEventBus_DoesNotThrow()
    {
        // Act
        var action = () => CreateManager(useDefaultEventBus: false);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullRegeditLauncher_DoesNotThrow()
    {
        // Act
        var action = () => CreateManager(regeditLauncher: null);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithEventBus_SubscribesToTooltipUpdatedEvent()
    {
        // Act
        CreateManager();

        // Assert
        _mockEventBus.Verify(
            e => e.Subscribe<TooltipUpdatedEvent>(It.IsAny<Action<TooltipUpdatedEvent>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullEventBus_DoesNotSubscribe()
    {
        // Act
        CreateManager(useDefaultEventBus: false);

        // Assert
        _mockEventBus.Verify(
            e => e.Subscribe<TooltipUpdatedEvent>(It.IsAny<Action<TooltipUpdatedEvent>>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────────
    // OpenRegeditCommand
    // ──────────────────────────────────────────────────

    [Fact]
    public void OpenRegeditCommand_IsNotNull()
    {
        // Arrange
        var manager = CreateManager();

        // Act & Assert
        manager.OpenRegeditCommand.Should().NotBeNull();
    }

    [Fact]
    public void OpenRegeditCommand_WithValidPath_CallsRegeditLauncher()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        manager.OpenRegeditCommand.Execute(@"HKLM\SOFTWARE\Test");

        // Assert
        _mockRegeditLauncher.Verify(r => r.OpenAtPath(@"HKLM\SOFTWARE\Test"), Times.Once);
    }

    [Fact]
    public void OpenRegeditCommand_WithNullPath_DoesNotCallRegeditLauncher()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        manager.OpenRegeditCommand.Execute(null);

        // Assert
        _mockRegeditLauncher.Verify(r => r.OpenAtPath(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void OpenRegeditCommand_WithEmptyPath_DoesNotCallRegeditLauncher()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        manager.OpenRegeditCommand.Execute(string.Empty);

        // Assert
        _mockRegeditLauncher.Verify(r => r.OpenAtPath(It.IsAny<string>()), Times.Never);
    }

    // ──────────────────────────────────────────────────
    // TooltipUpdatedEvent handling
    // ──────────────────────────────────────────────────

    [Fact]
    public void OnTooltipUpdated_MatchingSettingId_PopulatesRegistryDetails()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        _mockRegeditLauncher
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Returns(true);

        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKLM\SOFTWARE\Test",
            ValueName = "TestValue",
            ValueType = RegistryValueKind.DWord,
            RecommendedValue = 1
        };
        var tooltipData = new SettingTooltipData
        {
            SettingId = "MySetting",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>
            {
                { regSetting, "0" }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _details.Should().HaveCount(1);
        _details[0].RowType.Should().Be(DetailRowType.Registry);
        _details[0].RegistryPath.Should().Be(@"HKLM\SOFTWARE\Test");
        _details[0].ValueName.Should().Be("TestValue");
        _details[0].ValueType.Should().Be("DWord");
        _details[0].CurrentValue.Should().Be("0");
        _details[0].RecommendedValue.Should().Be("1");
        _details[0].CanOpenRegedit.Should().BeTrue();
    }

    [Fact]
    public void OnTooltipUpdated_NonMatchingSettingId_DoesNotPopulateDetails()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        var tooltipData = new SettingTooltipData
        {
            SettingId = "OtherSetting",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>
            {
                { new RegistrySetting { KeyPath = @"HKLM\Test", ValueType = RegistryValueKind.DWord }, "0" }
            }
        };
        var evt = new TooltipUpdatedEvent("OtherSetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _details.Should().BeEmpty();
    }

    [Fact]
    public void OnTooltipUpdated_PopulatesScheduledTaskDetails()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        var tooltipData = new SettingTooltipData
        {
            SettingId = "MySetting",
            ScheduledTaskSettings = new List<ScheduledTaskSetting>
            {
                new ScheduledTaskSetting
                {
                    TaskPath = @"\Microsoft\Windows\Test",
                    RecommendedState = false
                }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _details.Should().HaveCount(1);
        _details[0].RowType.Should().Be(DetailRowType.ScheduledTask);
        _details[0].TaskPath.Should().Be(@"\Microsoft\Windows\Test");
        _details[0].RecommendedState.Should().Be("Disabled");
    }

    [Fact]
    public void OnTooltipUpdated_PopulatesScheduledTaskDetails_EnabledState()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        var tooltipData = new SettingTooltipData
        {
            SettingId = "MySetting",
            ScheduledTaskSettings = new List<ScheduledTaskSetting>
            {
                new ScheduledTaskSetting
                {
                    TaskPath = @"\Microsoft\Windows\Task",
                    RecommendedState = true
                }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _details[0].RecommendedState.Should().Be("Enabled");
    }

    [Fact]
    public void OnTooltipUpdated_PopulatesPowerConfigDetails()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        var tooltipData = new SettingTooltipData
        {
            SettingId = "MySetting",
            PowerCfgSettings = new List<PowerCfgSetting>
            {
                new PowerCfgSetting
                {
                    SubgroupGuid = "sub-guid",
                    SettingGuid = "set-guid",
                    SubgroupGUIDAlias = "SubAlias",
                    SettingGUIDAlias = "SetAlias",
                    Units = "Seconds",
                    RecommendedValueAC = 30,
                    RecommendedValueDC = 60
                }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _details.Should().HaveCount(1);
        _details[0].RowType.Should().Be(DetailRowType.PowerConfig);
        _details[0].SubgroupGuid.Should().Be("sub-guid");
        _details[0].SettingGuid.Should().Be("set-guid");
        _details[0].SubgroupAlias.Should().Be("SubAlias");
        _details[0].SettingAlias.Should().Be("SetAlias");
        _details[0].PowerUnits.Should().Be("Seconds");
        _details[0].RecommendedAC.Should().Be("30");
        _details[0].RecommendedDC.Should().Be("60");
    }

    [Fact]
    public void OnTooltipUpdated_ClearsExistingDetailsBeforePopulating()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        // Pre-populate with a dummy row
        _details.Add(new TechnicalDetailRow { RowType = DetailRowType.Registry, RegistryPath = "old" });

        var tooltipData = new SettingTooltipData
        {
            SettingId = "MySetting",
            ScheduledTaskSettings = new List<ScheduledTaskSetting>
            {
                new ScheduledTaskSetting { TaskPath = @"\New\Task", RecommendedState = true }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _details.Should().HaveCount(1);
        _details[0].TaskPath.Should().Be(@"\New\Task");
    }

    [Fact]
    public void OnTooltipUpdated_CallsOnDetailsChanged()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        var tooltipData = new SettingTooltipData { SettingId = "MySetting" };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _detailsChangedCalled.Should().BeTrue();
    }

    [Fact]
    public void OnTooltipUpdated_RegistryKeyDoesNotExist_SetsCanOpenRegeditFalse()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        _mockRegeditLauncher
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Returns(false);

        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKLM\SOFTWARE\Missing",
            ValueName = "Val",
            ValueType = RegistryValueKind.DWord
        };
        var tooltipData = new SettingTooltipData
        {
            SettingId = "MySetting",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>
            {
                { regSetting, null }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _details[0].CanOpenRegedit.Should().BeFalse();
        _details[0].CurrentValue.Should().Be("(not set)");
    }

    [Fact]
    public void OnTooltipUpdated_RegistryKeyExistsThrows_SetsCanOpenRegeditFalse()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        _mockRegeditLauncher
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Throws(new UnauthorizedAccessException("Access denied"));

        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKLM\SOFTWARE\Locked",
            ValueName = "Val",
            ValueType = RegistryValueKind.DWord
        };
        var tooltipData = new SettingTooltipData
        {
            SettingId = "MySetting",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>
            {
                { regSetting, "1" }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _details[0].CanOpenRegedit.Should().BeFalse();
        _mockLogService.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("KeyExists failed"))),
            Times.Once);
    }

    [Fact]
    public void OnTooltipUpdated_NullValueName_DefaultsToDefaultDisplay()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        _mockRegeditLauncher
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Returns(true);

        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKLM\SOFTWARE\Test",
            ValueName = null,
            ValueType = RegistryValueKind.String,
            RecommendedValue = null
        };
        var tooltipData = new SettingTooltipData
        {
            SettingId = "MySetting",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>
            {
                { regSetting, "current" }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _details[0].ValueName.Should().Be("(Default)");
        _details[0].RecommendedValue.Should().Be(string.Empty);
    }

    [Fact]
    public void OnTooltipUpdated_DispatcherRunsOnUIThread()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        var tooltipData = new SettingTooltipData { SettingId = "MySetting" };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _mockDispatcherService.Verify(d => d.RunOnUIThread(It.IsAny<Action>()), Times.Once);
    }

    // ──────────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────────

    [Fact]
    public void Dispose_DisposesSubscriptionToken()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        manager.Dispose();

        // Assert
        _mockSubscriptionToken.Verify(t => t.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var action = () =>
        {
            manager.Dispose();
            manager.Dispose();
        };

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_DisposesTokenOnlyOnce()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        manager.Dispose();
        manager.Dispose();

        // Assert - subscription set to null after first dispose, so only one call
        _mockSubscriptionToken.Verify(t => t.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_WithNullEventBus_DoesNotThrow()
    {
        // Arrange
        var manager = CreateManager(useDefaultEventBus: false);

        // Act
        var action = () => manager.Dispose();

        // Assert
        action.Should().NotThrow();
    }
}
