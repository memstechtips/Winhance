using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private IReadOnlyList<TechnicalDetailSection> _sections = Array.Empty<TechnicalDetailSection>();
    private readonly List<Action<TooltipUpdatedEvent>> _capturedHandlers;

    private string _currentSettingId = "TestSetting";
    private TechnicalDetailsManager? _manager;

    public TechnicalDetailsManagerTests()
    {
        _mockLogService = new Mock<ILogService>();
        _mockDispatcherService = new Mock<IDispatcherService>();
        _mockRegeditLauncher = new Mock<IRegeditLauncher>();
        _mockEventBus = new Mock<IEventBus>();
        _mockSubscriptionToken = new Mock<ISubscriptionToken>();
        _capturedHandlers = new List<Action<TooltipUpdatedEvent>>();

        // Set up dispatcher to execute actions synchronously for testing
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Microsoft.UI.Dispatching.DispatcherQueuePriority>(), It.IsAny<Action>()))
            .Callback<Microsoft.UI.Dispatching.DispatcherQueuePriority, Action>((_, action) => action());

        // Capture the event handler when Subscribe is called
        _mockEventBus
            .Setup(e => e.Subscribe<TooltipUpdatedEvent>(It.IsAny<Action<TooltipUpdatedEvent>>()))
            .Callback<Action<TooltipUpdatedEvent>>(handler => _capturedHandlers.Add(handler))
            .Returns(_mockSubscriptionToken.Object);

        // Default echo: return the key as-is when no specific setup is provided
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(key => key);
    }

    private static readonly TechnicalDetailLabels TestLabels = new()
    {
        Path = "Path",
        Value = "Value",
        Current = "Current",
        Recommended = "Recommended",
        Default = "Default",
        ValueNotExist = "doesn't exist",
        On = "On",
        Off = "Off",
        ScriptOnEnable = "On Enable",
        ScriptOnDisable = "On Disable",
        RegContentOnEnable = "On Enable",
        RegContentOnDisable = "On Disable",
        DependencyEquals = "=",
        DependencyNotEquals = "≠"
    };

    private TechnicalDetailsManager CreateManager(
        IRegeditLauncher? regeditLauncher = null,
        IEventBus? eventBus = null,
        bool useDefaultEventBus = true)
    {
        _manager = new TechnicalDetailsManager(
            () => _currentSettingId,
            newSections => _sections = newSections,
            _mockLogService.Object,
            _mockDispatcherService.Object,
            regeditLauncher ?? _mockRegeditLauncher.Object,
            useDefaultEventBus ? (eventBus ?? _mockEventBus.Object) : null,
            _mockLocalizationService.Object,
            TestLabels);
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
            RecommendedValue = 1,
            DefaultValue = null
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
        var rows = _sections.SelectMany(s => s.Rows).ToList();
        rows.Should().HaveCount(1);
        rows[0].RowType.Should().Be(DetailRowType.Registry);
        rows[0].RegistryPath.Should().Be(@"HKLM\SOFTWARE\Test");
        rows[0].ValueName.Should().Be("TestValue");
        rows[0].ValueType.Should().Be("DWord");
        rows[0].CurrentValue.Should().Be("0");
        rows[0].RecommendedValue.Should().Be("1");
        rows[0].DefaultValue.Should().Be(TestLabels.ValueNotExist);
        rows[0].CanOpenRegedit.Should().BeTrue();
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
                { new RegistrySetting { KeyPath = @"HKLM\Test", ValueType = RegistryValueKind.DWord, RecommendedValue = null, DefaultValue = null }, "0" }
            }
        };
        var evt = new TooltipUpdatedEvent("OtherSetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _sections.SelectMany(s => s.Rows).Should().BeEmpty();
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
                    RecommendedState = false,
                    DefaultState = null
                }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        var rows = _sections.SelectMany(s => s.Rows).ToList();
        rows.Should().HaveCount(1);
        rows[0].RowType.Should().Be(DetailRowType.ScheduledTask);
        rows[0].TaskPath.Should().Be(@"\Microsoft\Windows\Test");
        rows[0].RecommendedState.Should().Be(TestLabels.Off);
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
                    RecommendedState = true,
                    DefaultState = null
                }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        _sections.SelectMany(s => s.Rows).First().RecommendedState.Should().Be(TestLabels.On);
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
                    RecommendedValueDC = 60,
                    DefaultValueAC = null,
                    DefaultValueDC = null
                }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        var rows = _sections.SelectMany(s => s.Rows).ToList();
        rows.Should().HaveCount(1);
        rows[0].RowType.Should().Be(DetailRowType.PowerConfig);
        rows[0].SubgroupGuid.Should().Be("sub-guid");
        rows[0].SettingGuid.Should().Be("set-guid");
        rows[0].SubgroupAlias.Should().Be("SubAlias");
        rows[0].SettingAlias.Should().Be("SetAlias");
        rows[0].PowerUnits.Should().Be("Seconds");
        rows[0].RecommendedAC.Should().Be("30");
        rows[0].RecommendedDC.Should().Be("60");
    }

    [Fact]
    public void OnTooltipUpdated_ReplacesExistingDetails()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        // Trigger a first update to populate sections
        var firstData = new SettingTooltipData
        {
            SettingId = "MySetting",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>
            {
                { new RegistrySetting { KeyPath = @"HKLM\Old", ValueType = RegistryValueKind.DWord, RecommendedValue = null, DefaultValue = null }, "0" }
            }
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("MySetting", firstData));

        var tooltipData = new SettingTooltipData
        {
            SettingId = "MySetting",
            ScheduledTaskSettings = new List<ScheduledTaskSetting>
            {
                new ScheduledTaskSetting { TaskPath = @"\New\Task", RecommendedState = true, DefaultState = null }
            }
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert — new sections contain only the new data
        var rows = _sections.SelectMany(s => s.Rows).ToList();
        rows.Should().HaveCount(1);
        rows[0].TaskPath.Should().Be(@"\New\Task");
    }

    [Fact]
    public void OnTooltipUpdated_SwapsCollection()
    {
        // Arrange
        _currentSettingId = "MySetting";
        var manager = CreateManager();
        var originalSections = _sections;

        var tooltipData = new SettingTooltipData { SettingId = "MySetting" };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert — the setter was called with a new list
        _sections.Should().NotBeNull();
        _sections.Should().NotBeSameAs(originalSections);
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
            ValueType = RegistryValueKind.DWord,
            RecommendedValue = null,
            DefaultValue = null
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
        var row = _sections.SelectMany(s => s.Rows).First();
        row.CanOpenRegedit.Should().BeFalse();
        row.CurrentValue.Should().Be(TestLabels.ValueNotExist);
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
            ValueType = RegistryValueKind.DWord,
            RecommendedValue = null,
            DefaultValue = null
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
        _sections.SelectMany(s => s.Rows).First().CanOpenRegedit.Should().BeFalse();
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
            RecommendedValue = null,
            DefaultValue = null
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
        // RecommendedValue = null with no EnabledValue/DisabledValue: no recommendation can be
        // resolved, so the column shows the ValueNotExist label (updated from "" after
        // DefaultValue/FormatNotExist were introduced in an earlier refactor).
        var row = _sections.SelectMany(s => s.Rows).First();
        row.ValueName.Should().Be("(Default)");
        row.RecommendedValue.Should().Be(TestLabels.ValueNotExist);
        row.DefaultValue.Should().Be(TestLabels.ValueNotExist);
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
        _mockDispatcherService.Verify(
            d => d.RunOnUIThread(It.IsAny<Microsoft.UI.Dispatching.DispatcherQueuePriority>(), It.IsAny<Action>()),
            Times.Once);
    }

    [Fact]
    public void OnTooltipUpdated_RegistryWithExplicitDefaultValue_SetsDefaultValueToString()
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
            RecommendedValue = 0,
            DefaultValue = 1
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
        _sections.SelectMany(s => s.Rows).First().DefaultValue.Should().Be("1");
    }

    [Fact]
    public void OnTooltipUpdated_RegistryWithNullDefaultValue_UsesNotExistLabel()
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
            RecommendedValue = null,
            DefaultValue = null
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
        _sections.SelectMany(s => s.Rows).First().DefaultValue.Should().Be(TestLabels.ValueNotExist);
    }

    [Fact]
    public void OnTooltipUpdated_NullCurrentValue_UsesNotExistLabel()
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
            RecommendedValue = null,
            DefaultValue = null
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
        _sections.SelectMany(s => s.Rows).First().CurrentValue.Should().Be(TestLabels.ValueNotExist);
    }

    [Fact]
    public void OnTooltipUpdated_NullValue_EnabledIncludesNull_ShowsNotExistOn()
    {
        // Arrange — Game Mode scenario: EnabledValue = [1, null], value doesn't exist
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        _mockRegeditLauncher
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Returns(true);

        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKLM\SOFTWARE\Test",
            ValueName = "GameMode",
            ValueType = RegistryValueKind.DWord,
            EnabledValue = [1, null],
            DisabledValue = [0],
            RecommendedValue = null,
            DefaultValue = null
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
        var row = _sections.SelectMany(s => s.Rows).First();
        row.CurrentValue.Should().Be("doesn't exist (On)");
        row.DefaultValue.Should().Be("doesn't exist (On)");
    }

    [Fact]
    public void OnTooltipUpdated_NullValue_DisabledIncludesNull_ShowsNotExistOff()
    {
        // Arrange — setting where absence means disabled
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        _mockRegeditLauncher
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Returns(true);

        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKLM\SOFTWARE\Test",
            ValueName = "SomeValue",
            ValueType = RegistryValueKind.DWord,
            EnabledValue = [1],
            DisabledValue = [0, null],
            RecommendedValue = null,
            DefaultValue = null
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
        var row = _sections.SelectMany(s => s.Rows).First();
        row.CurrentValue.Should().Be("doesn't exist (Off)");
        row.DefaultValue.Should().Be("doesn't exist (Off)");
    }

    [Fact]
    public void OnTooltipUpdated_NullValue_NoNullInEnabledOrDisabled_ShowsPlainNotExist()
    {
        // Arrange — neither EnabledValue nor DisabledValue contains null
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        _mockRegeditLauncher
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Returns(true);

        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKLM\SOFTWARE\Test",
            ValueName = "SomeValue",
            ValueType = RegistryValueKind.DWord,
            EnabledValue = [1],
            DisabledValue = [0],
            RecommendedValue = null,
            DefaultValue = null
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
        var row = _sections.SelectMany(s => s.Rows).First();
        row.CurrentValue.Should().Be("doesn't exist");
        row.DefaultValue.Should().Be("doesn't exist");
    }

    // ──────────────────────────────────────────────────
    // Inverted-policy Recommended column (Task 3)
    // ──────────────────────────────────────────────────

    [Fact]
    public void OnTooltipUpdated_InvertedPolicy_RecommendedColumn_UsesToggleStateValue()
    {
        // Inverted-policy shape like security-workplace-join-messages.
        // RecommendedToggleState=false => recommend the "blocking" state,
        // which maps to DisabledValue=[1], so Recommended must render "1 (Off)".
        _currentSettingId = "security-workplace-join-messages";
        var manager = CreateManager();
        _mockRegeditLauncher.Setup(r => r.KeyExists(It.IsAny<string>())).Returns(false);

        var reg = new RegistrySetting
        {
            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin",
            ValueName = "BlockAADWorkplaceJoin",
            ValueType = RegistryValueKind.DWord,
            EnabledValue = new object?[] { null },
            DisabledValue = new object?[] { 1 },
            RecommendedValue = null,
            DefaultValue = null,
            IsGroupPolicy = true,
        };
        var setting = new SettingDefinition
        {
            Id = "security-workplace-join-messages",
            Name = "Workplace Join Message Prompts",
            Description = "",
            InputType = InputType.Toggle,
            RecommendedToggleState = false,
            RegistrySettings = new[] { reg },
        };
        var tooltipData = new SettingTooltipData
        {
            SettingId = "security-workplace-join-messages",
            SettingDefinition = setting,
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>
            {
                { reg, "1" }
            }
        };
        var evt = new TooltipUpdatedEvent("security-workplace-join-messages", tooltipData);

        _capturedHandlers[0](evt);

        var rows = _sections.SelectMany(s => s.Rows).ToList();
        rows.Should().HaveCount(1);
        rows[0].CurrentValue.Should().Be("1");
        rows[0].RecommendedValue.Should().Be($"1 ({TestLabels.Off})",
            because: "RecommendedToggleState=false maps to DisabledValue=[1] => '1 (Off)'");
        rows[0].DefaultValue.Should().Be($"{TestLabels.ValueNotExist} ({TestLabels.On})",
            because: "DefaultValue=null with EnabledValue=[null] keeps the null-sentinel 'doesn't exist (On)'");
    }

    [Fact]
    public void OnTooltipUpdated_InvertedPolicy_RecommendedToggleOn_RendersNotExistOn()
    {
        // RecommendedToggleState=true maps to EnabledValue=[null] => null sentinel.
        _currentSettingId = "inverted-rec-on";
        var manager = CreateManager();

        var reg = new RegistrySetting
        {
            KeyPath = @"HKLM\Test",
            ValueName = "V",
            ValueType = RegistryValueKind.DWord,
            EnabledValue = new object?[] { null },
            DisabledValue = new object?[] { 1 },
            RecommendedValue = null,
            DefaultValue = null,
            IsGroupPolicy = true,
        };
        var setting = new SettingDefinition
        {
            Id = "inverted-rec-on",
            Name = "N",
            Description = "",
            InputType = InputType.Toggle,
            RecommendedToggleState = true,
            RegistrySettings = new[] { reg },
        };
        var tooltipData = new SettingTooltipData
        {
            SettingId = "inverted-rec-on",
            SettingDefinition = setting,
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?> { { reg, null } }
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("inverted-rec-on", tooltipData));

        _sections.SelectMany(s => s.Rows).First().RecommendedValue.Should().Be($"{TestLabels.ValueNotExist} ({TestLabels.On})");
    }

    [Fact]
    public void OnTooltipUpdated_ToggleWithBothRecommendedValueAndToggleState_UsesToggleStateLabel()
    {
        // When both reg.RecommendedValue and SettingDefinition.RecommendedToggleState
        // are set, RecommendedToggleState wins and the display includes the "(On)/(Off)"
        // label, matching the badge logic's priority.
        _currentSettingId = "both-rec-set";
        var manager = CreateManager();

        var reg = new RegistrySetting
        {
            KeyPath = @"HKLM\Test",
            ValueName = "V",
            ValueType = RegistryValueKind.DWord,
            EnabledValue = new object?[] { null },
            DisabledValue = new object?[] { 1 },
            RecommendedValue = 1,
            DefaultValue = null,
            IsGroupPolicy = true,
        };
        var setting = new SettingDefinition
        {
            Id = "both-rec-set",
            Name = "N",
            Description = "",
            InputType = InputType.Toggle,
            RecommendedToggleState = false,
            RegistrySettings = new[] { reg },
        };
        var tooltipData = new SettingTooltipData
        {
            SettingId = "both-rec-set",
            SettingDefinition = setting,
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?> { { reg, "1" } }
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("both-rec-set", tooltipData));

        _sections.SelectMany(s => s.Rows).First().RecommendedValue.Should().Be($"1 ({TestLabels.Off})");
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

    // ──────────────────────────────────────────────────
    // Selection Recommended/Default column resolution (Task A9)
    // ──────────────────────────────────────────────────

    // ── Section output ──

    [Fact]
    public void UpdateTechnicalDetails_RegistryOnly_ProducesSingleRegistrySection()
    {
        var manager = CreateManager();
        var regKey = new RegistrySetting
        {
            KeyPath = @"HKLM\Software\Test",
            ValueName = "Test",
            ValueType = RegistryValueKind.DWord,
            RecommendedValue = 1,
            DefaultValue = null
        };
        var tooltip = new SettingTooltipData
        {
            SettingId = "TestSetting",
            DisplayValue = "Enabled",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?> { [regKey] = "1" },
            ScheduledTaskSettings = Array.Empty<ScheduledTaskSetting>(),
            PowerCfgSettings = Array.Empty<PowerCfgSetting>(),
            SettingDefinition = null
        };

        _capturedHandlers[0](new TooltipUpdatedEvent("TestSetting", tooltip));

        _sections.Should().HaveCount(1);
        _sections[0].Type.Should().Be(DetailRowType.Registry);
        _sections[0].StartsExpanded.Should().BeTrue();
        _sections[0].Rows.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateTechnicalDetails_ScheduledTaskWithDefaultState_PopulatesDefaultColumn()
    {
        var manager = CreateManager();
        var task = new ScheduledTaskSetting
        {
            TaskPath = @"\Microsoft\Windows\SomeTask",
            RecommendedState = false,
            DefaultState = true
        };
        var tooltip = new SettingTooltipData
        {
            SettingId = "TestSetting",
            DisplayValue = "Disabled",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>(),
            ScheduledTaskSettings = new[] { task },
            PowerCfgSettings = Array.Empty<PowerCfgSetting>()
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("TestSetting", tooltip));

        var taskSection = _sections.Single(s => s.Type == DetailRowType.ScheduledTask);
        var row = taskSection.Rows.Single();
        row.DefaultState.Should().Be(TestLabels.On);
        row.RecommendedState.Should().Be(TestLabels.Off);
    }

    [Fact]
    public void UpdateTechnicalDetails_ScheduledTaskWithNullDefaultState_LeavesDefaultBlank()
    {
        var manager = CreateManager();
        var task = new ScheduledTaskSetting
        {
            TaskPath = @"\Microsoft\Test",
            RecommendedState = true,
            DefaultState = null
        };
        var tooltip = new SettingTooltipData
        {
            SettingId = "TestSetting",
            DisplayValue = "",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>(),
            ScheduledTaskSettings = new[] { task },
            PowerCfgSettings = Array.Empty<PowerCfgSetting>()
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("TestSetting", tooltip));

        _sections.Single(s => s.Type == DetailRowType.ScheduledTask).Rows.Single().DefaultState.Should().BeEmpty();
    }

    [Fact]
    public void UpdateTechnicalDetails_NothingDeclared_ProducesZeroSections()
    {
        var manager = CreateManager();
        var tooltip = new SettingTooltipData
        {
            SettingId = "TestSetting",
            DisplayValue = "",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>(),
            ScheduledTaskSettings = Array.Empty<ScheduledTaskSetting>(),
            PowerCfgSettings = Array.Empty<PowerCfgSetting>()
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("TestSetting", tooltip));

        _sections.Should().BeEmpty();
    }

    [Fact]
    public void UpdateTechnicalDetails_PowerCfgWithCurrentAndDefault_PopulatesAllAcDcColumns()
    {
        var manager = CreateManager();
        var pcs = new PowerCfgSetting
        {
            SubgroupGuid = Guid.NewGuid().ToString(),
            SettingGuid = Guid.NewGuid().ToString(),
            SubgroupGUIDAlias = "SUB_SLEEP",
            SettingGUIDAlias = "STANDBYIDLE",
            Units = "seconds",
            RecommendedValueAC = 0,
            RecommendedValueDC = 900,
            DefaultValueAC = 1800,
            DefaultValueDC = 600
        };
        var tooltip = new SettingTooltipData
        {
            SettingId = "TestSetting",
            DisplayValue = "Never",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>(),
            ScheduledTaskSettings = Array.Empty<ScheduledTaskSetting>(),
            PowerCfgSettings = new[] { pcs },
            CurrentPowerValues = new Dictionary<PowerCfgSetting, (int? AC, int? DC)> { [pcs] = (1200, 600) }
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("TestSetting", tooltip));

        var row = _sections.Single(s => s.Type == DetailRowType.PowerConfig).Rows.Single();
        row.CurrentAC.Should().Contain("1200");
        row.CurrentDC.Should().Contain("600");
        row.RecommendedAC.Should().Contain("0");
        row.RecommendedDC.Should().Contain("900");
        row.DefaultAC.Should().Contain("1800");
        row.DefaultDC.Should().Contain("600");
    }

    [Fact]
    public void UpdateTechnicalDetails_PowerShellBothScripts_ProducesTwoRowsInOneSection()
    {
        var manager = CreateManager();
        var script = new PowerShellScriptSetting
        {
            EnabledScript  = "Stop-Service wuauserv",
            DisabledScript = "Start-Service wuauserv"
        };
        var tooltip = new SettingTooltipData
        {
            SettingId = "TestSetting",
            DisplayValue = "",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>(),
            ScheduledTaskSettings = Array.Empty<ScheduledTaskSetting>(),
            PowerCfgSettings = Array.Empty<PowerCfgSetting>(),
            PowerShellScripts = new[] { script }
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("TestSetting", tooltip));

        var section = _sections.Single(s => s.Type == DetailRowType.PowerShellScript);
        section.Rows.Should().HaveCount(2);
        section.Rows[0].ScriptLabel.Should().Be(TestLabels.ScriptOnEnable);
        section.Rows[0].ScriptBody.Should().Be("Stop-Service wuauserv");
        section.Rows[1].ScriptLabel.Should().Be(TestLabels.ScriptOnDisable);
        section.Rows[1].ScriptBody.Should().Be("Start-Service wuauserv");
    }

    [Fact]
    public void UpdateTechnicalDetails_PowerShellEnabledOnly_ProducesOneRow()
    {
        var manager = CreateManager();
        var script = new PowerShellScriptSetting { EnabledScript = "Write-Host 'hi'", DisabledScript = null };
        var tooltip = new SettingTooltipData
        {
            SettingId = "TestSetting",
            DisplayValue = "",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>(),
            ScheduledTaskSettings = Array.Empty<ScheduledTaskSetting>(),
            PowerCfgSettings = Array.Empty<PowerCfgSetting>(),
            PowerShellScripts = new[] { script }
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("TestSetting", tooltip));

        _sections.Single(s => s.Type == DetailRowType.PowerShellScript).Rows.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateTechnicalDetails_RegContentBoth_ProducesTwoRows()
    {
        var manager = CreateManager();
        var reg = new RegContentSetting
        {
            EnabledContent  = "Windows Registry Editor Version 5.00\r\n[HKLM\\Software\\Test]\r\n\"V\"=dword:00000001",
            DisabledContent = "Windows Registry Editor Version 5.00\r\n[HKLM\\Software\\Test]\r\n\"V\"=dword:00000000"
        };
        var tooltip = new SettingTooltipData
        {
            SettingId = "TestSetting",
            DisplayValue = "",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>(),
            ScheduledTaskSettings = Array.Empty<ScheduledTaskSetting>(),
            PowerCfgSettings = Array.Empty<PowerCfgSetting>(),
            RegContents = new[] { reg }
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("TestSetting", tooltip));

        var section = _sections.Single(s => s.Type == DetailRowType.RegContent);
        section.Rows.Should().HaveCount(2);
        section.Rows[0].ContentLabel.Should().Be(TestLabels.RegContentOnEnable);
        section.Rows[0].ContentBody.Should().Contain("dword:00000001");
        section.Rows[1].ContentLabel.Should().Be(TestLabels.RegContentOnDisable);
        section.Rows[1].ContentBody.Should().Contain("dword:00000000");
    }

    [Fact]
    public void UpdateTechnicalDetails_RequiresEnabledDependency_ResolvesLocalizedName()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Setting_disable-telemetry_Name"))
            .Returns("Disable Telemetry");

        _currentSettingId = "current-setting";
        var manager = CreateManager();
        var dep = new SettingDependency
        {
            DependencyType     = SettingDependencyType.RequiresEnabled,
            DependentSettingId = "current-setting",
            RequiredSettingId  = "disable-telemetry"
        };
        var tooltip = new SettingTooltipData
        {
            SettingId = "current-setting",
            DisplayValue = "",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>(),
            ScheduledTaskSettings = Array.Empty<ScheduledTaskSetting>(),
            PowerCfgSettings = Array.Empty<PowerCfgSetting>(),
            Dependencies = new[] { dep }
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("current-setting", tooltip));

        var row = _sections.Single(s => s.Type == DetailRowType.Dependency).Rows.Single();
        row.DependencyLabel.Should().Be("Disable Telemetry");
        row.DependencyRelation.Should().Be($"{TestLabels.DependencyEquals} {TestLabels.On}");
    }

    [Fact]
    public void UpdateTechnicalDetails_RequiresDisabledDependency_UsesOffRelation()
    {
        _mockLocalizationService.Setup(l => l.GetString("Setting_other_Name")).Returns("Other Setting");
        _currentSettingId = "current-setting";
        var manager = CreateManager();
        var dep = new SettingDependency
        {
            DependencyType     = SettingDependencyType.RequiresDisabled,
            DependentSettingId = "current-setting",
            RequiredSettingId  = "other"
        };
        var tooltip = new SettingTooltipData
        {
            SettingId = "current-setting",
            DisplayValue = "",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>(),
            ScheduledTaskSettings = Array.Empty<ScheduledTaskSetting>(),
            PowerCfgSettings = Array.Empty<PowerCfgSetting>(),
            Dependencies = new[] { dep }
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("current-setting", tooltip));

        _sections.Single(s => s.Type == DetailRowType.Dependency).Rows.Single()
            .DependencyRelation.Should().Be($"{TestLabels.DependencyEquals} {TestLabels.Off}");
    }

    [Fact]
    public void OnTooltipUpdated_SelectionSetting_ResolvesColumnsFromComboBoxOptions()
    {
        // Arrange
        // Selection setting with two options:
        //   Option 0 (IsDefault):     ValueMappings { "X" = 0, "Y" = null }
        //   Option 1 (IsRecommended): ValueMappings { "X" = 1 }   // "Y" absent
        // Two RegistrySettings — one for "X" and one for "Y" — both with
        // RecommendedValue/DefaultValue = null (the new Selection state model).
        _currentSettingId = "MySetting";
        var manager = CreateManager();

        _mockRegeditLauncher
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Returns(true);

        var regX = new RegistrySetting
        {
            KeyPath = @"HKLM\SOFTWARE\Test",
            ValueName = "X",
            ValueType = RegistryValueKind.DWord,
            RecommendedValue = null,
            DefaultValue = null
        };
        var regY = new RegistrySetting
        {
            KeyPath = @"HKLM\SOFTWARE\Test",
            ValueName = "Y",
            ValueType = RegistryValueKind.DWord,
            RecommendedValue = null,
            DefaultValue = null
        };

        var settingDef = new SettingDefinition
        {
            Id = "MySetting",
            Name = "My Setting",
            Description = "test",
            InputType = InputType.Selection,
            ComboBox = new ComboBoxMetadata
            {
                Options = new List<Winhance.Core.Features.Common.Models.ComboBoxOption>
                {
                    new Winhance.Core.Features.Common.Models.ComboBoxOption
                    {
                        DisplayName = "Default option",
                        IsDefault = true,
                        ValueMappings = new Dictionary<string, object?>
                        {
                            { "X", 0 },
                            { "Y", null }   // explicit null: key would be deleted
                        }
                    },
                    new Winhance.Core.Features.Common.Models.ComboBoxOption
                    {
                        DisplayName = "Recommended option",
                        IsRecommended = true,
                        ValueMappings = new Dictionary<string, object?>
                        {
                            { "X", 1 }
                            // "Y" absent from mapping: entry is unchanged under this option
                        }
                    }
                }
            }
        };

        var tooltipData = new SettingTooltipData
        {
            SettingId = "MySetting",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?>
            {
                { regX, "0" },
                { regY, "0" }
            },
            SettingDefinition = settingDef
        };
        var evt = new TooltipUpdatedEvent("MySetting", tooltipData);

        // Act
        _capturedHandlers[0](evt);

        // Assert
        var allRows = _sections.SelectMany(s => s.Rows).ToList();
        allRows.Should().HaveCount(2);

        // Row X: both options have a value for "X".
        var rowX = allRows.First(r => r.ValueName == "X");
        rowX.RecommendedValue.Should().Be("1");
        rowX.DefaultValue.Should().Be("0");

        // Row Y: absent from Recommended option's mapping, explicitly null in Default's mapping.
        // Current TechnicalDetailLabels collapses both cases to ValueNotExist — assert both
        // columns use that label (and critically, do NOT show "0" or "1").
        var rowY = allRows.First(r => r.ValueName == "Y");
        rowY.RecommendedValue.Should().Be(TestLabels.ValueNotExist);
        rowY.DefaultValue.Should().Be(TestLabels.ValueNotExist);
    }

    [Fact]
    public void UpdateTechnicalDetails_SelectionSettingWithNullPerEntryValues_StillProducesRows()
    {
        _currentSettingId = "updates-policy-mode";
        var manager = CreateManager();
        var reg = new RegistrySetting
        {
            KeyPath = @"HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU",
            ValueName = "AUOptions",
            ValueType = RegistryValueKind.DWord,
            RecommendedValue = null,
            DefaultValue = null
        };
        var definition = new SettingDefinition
        {
            Id = "updates-policy-mode",
            Name = "Windows Update Policy Mode",
            Description = "",
            InputType = InputType.Selection
        };
        var tooltip = new SettingTooltipData
        {
            SettingId = "updates-policy-mode",
            DisplayValue = "Notify for download and auto install",
            IndividualRegistryValues = new Dictionary<RegistrySetting, string?> { [reg] = "2" },
            ScheduledTaskSettings = Array.Empty<ScheduledTaskSetting>(),
            PowerCfgSettings = Array.Empty<PowerCfgSetting>(),
            SettingDefinition = definition
        };
        _capturedHandlers[0](new TooltipUpdatedEvent("updates-policy-mode", tooltip));

        _sections.Should().NotBeEmpty();
        var regSection = _sections.Single(s => s.Type == DetailRowType.Registry);
        regSection.Rows.Should().HaveCount(1);
        regSection.Rows[0].CurrentValue.Should().Be("2");
        // Recommended / Default may be blank — that's fine; banner visibility is driven by section count.
    }
}
