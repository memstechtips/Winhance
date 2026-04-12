using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.Services;

/// <summary>
/// Tests for <see cref="BulkSettingsActionService"/> Apply Recommended / Reset to Default on
/// Selection settings, plus round-trip agreement with <see cref="SettingItemViewModel"/> badge
/// state. These lock in the Phase A wiring that reads IsRecommended/IsDefault flags from
/// <c>ComboBoxMetadata.Options</c>.
/// </summary>
public class BulkSettingsActionServiceTests
{
    private const string TestSettingId = "test-selection";
    private const string TestDomain = "TestDomain";

    private readonly Mock<IDomainServiceRouter> _domainServiceRouter = new();
    private readonly Mock<IDomainService> _domainService = new();
    private readonly Mock<IWindowsVersionService> _versionService = new();
    private readonly Mock<ISettingApplicationService> _applicationService = new();
    private readonly Mock<ILogService> _logService = new();

    public BulkSettingsActionServiceTests()
    {
        _versionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22631);
        _versionService.Setup(v => v.IsWindows11()).Returns(true);

        _domainService.SetupGet(d => d.DomainName).Returns(TestDomain);
        _domainServiceRouter
            .Setup(r => r.GetDomainService(It.IsAny<string>()))
            .Returns(_domainService.Object);
    }

    private BulkSettingsActionService CreateSut(params SettingDefinition[] settings)
    {
        _domainService
            .Setup(d => d.GetSettingsAsync())
            .ReturnsAsync(settings.AsEnumerable());

        return new BulkSettingsActionService(
            _domainServiceRouter.Object,
            _versionService.Object,
            _applicationService.Object,
            _logService.Object);
    }

    private static SettingDefinition MakeSelectionSetting(
        int? recommendedIndex,
        int defaultIndex,
        bool supportsCustomState = false,
        string id = TestSettingId)
    {
        var options = new List<Winhance.Core.Features.Common.Models.ComboBoxOption>
        {
            new() { DisplayName = "A", ValueMappings = new Dictionary<string, object?> { ["V"] = 0 } },
            new() { DisplayName = "B", ValueMappings = new Dictionary<string, object?> { ["V"] = 1 } },
            new() { DisplayName = "C", ValueMappings = new Dictionary<string, object?> { ["V"] = 2 } },
        };
        options[defaultIndex] = options[defaultIndex] with { IsDefault = true };
        if (recommendedIndex is int r)
            options[r] = options[r] with { IsRecommended = true };

        return new SettingDefinition
        {
            Id = id,
            Name = "Test",
            Description = "",
            InputType = InputType.Selection,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Winhance\Test",
                    ValueName = "V",
                    RecommendedValue = null,
                    DefaultValue = null,
                    ValueType = RegistryValueKind.DWord,
                    IsPrimary = true,
                },
            },
            ComboBox = new ComboBoxMetadata
            {
                Options = options,
                SupportsCustomState = supportsCustomState,
                CustomStateDisplayName = supportsCustomState ? "Custom" : null,
            },
        };
    }

    // ── Apply / Reset: direct service behavior ──

    [Fact]
    public async Task ApplyRecommended_Selection_WritesRecommendedIndex()
    {
        var setting = MakeSelectionSetting(recommendedIndex: 1, defaultIndex: 0);
        var sut = CreateSut(setting);

        var applied = await sut.ApplyRecommendedAsync(new[] { setting.Id });

        applied.Should().Be(1);
        _applicationService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == TestSettingId &&
                r.Value != null &&
                (int)r.Value == 1 &&
                r.Enable == true &&
                r.ResetToDefault == false)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyRecommended_Selection_NoRecommendedOption_Skips()
    {
        // Informational ComboBox case: no option flagged IsRecommended.
        var setting = MakeSelectionSetting(recommendedIndex: null, defaultIndex: 0);
        var sut = CreateSut(setting);

        await sut.ApplyRecommendedAsync(new[] { setting.Id });

        _applicationService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == TestSettingId)),
            Times.Never);
    }

    [Fact]
    public async Task ResetToDefault_Selection_WritesDefaultIndex()
    {
        var setting = MakeSelectionSetting(recommendedIndex: 1, defaultIndex: 0);
        var sut = CreateSut(setting);

        var applied = await sut.ResetToDefaultsAsync(new[] { setting.Id });

        applied.Should().Be(1);
        _applicationService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == TestSettingId &&
                r.Value != null &&
                (int)r.Value == 0 &&
                r.Enable == true &&
                r.ResetToDefault == true)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyRecommended_Selection_SupportsCustomStateDoesNotFail()
    {
        var setting = MakeSelectionSetting(
            recommendedIndex: 1,
            defaultIndex: 0,
            supportsCustomState: true);
        var sut = CreateSut(setting);

        var action = () => sut.ApplyRecommendedAsync(new[] { setting.Id });
        await action.Should().NotThrowAsync();

        _applicationService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == TestSettingId &&
                r.Value != null &&
                (int)r.Value == 1)),
            Times.Once);
    }

    // ── Round-trip: apply service + SettingItemViewModel.ComputeBadgeState() agree ──

    [Fact]
    public async Task ApplyRecommended_Selection_RoundTrip_ViewModelShowsRecommendedBadge()
    {
        var setting = MakeSelectionSetting(recommendedIndex: 1, defaultIndex: 0);
        var sut = CreateSut(setting);

        // Capture the Value the apply service writes.
        object? writtenValue = null;
        _applicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .Callback<ApplySettingRequest>(r => writtenValue = r.Value)
            .ReturnsAsync(OperationResult.Succeeded());

        await sut.ApplyRecommendedAsync(new[] { setting.Id });

        writtenValue.Should().NotBeNull();
        var writtenIndex = (int)writtenValue!;

        // Simulate the UI reflecting the apply: SelectedValue on the VM equals the mapped "V"
        // of the option at the written index.
        var selectedOption = setting.ComboBox!.Options![writtenIndex];
        var vm = CreateSettingItemViewModel(setting);
        vm.SelectedValue = selectedOption.ValueMappings!["V"];
        vm.ComputeBadgeState();

        vm.BadgeState.Should().Be(SettingBadgeState.Recommended,
            because: "ApplyRecommended wrote the IsRecommended option index, and ComputeBadgeState " +
                     "must agree that the effective selection is Recommended.");
    }

    [Fact]
    public async Task ResetToDefault_Selection_RoundTrip_ViewModelShowsDefaultBadge()
    {
        var setting = MakeSelectionSetting(recommendedIndex: 1, defaultIndex: 0);
        var sut = CreateSut(setting);

        object? writtenValue = null;
        _applicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .Callback<ApplySettingRequest>(r => writtenValue = r.Value)
            .ReturnsAsync(OperationResult.Succeeded());

        await sut.ResetToDefaultsAsync(new[] { setting.Id });

        writtenValue.Should().NotBeNull();
        var writtenIndex = (int)writtenValue!;

        var selectedOption = setting.ComboBox!.Options![writtenIndex];
        var vm = CreateSettingItemViewModel(setting);
        vm.SelectedValue = selectedOption.ValueMappings!["V"];
        vm.ComputeBadgeState();

        vm.BadgeState.Should().Be(SettingBadgeState.Default,
            because: "ResetToDefault wrote the IsDefault option index, and ComputeBadgeState " +
                     "must agree that the effective selection is Default.");
    }

    // ── SettingItemViewModel construction helper (mirrors SettingItemViewModelTests) ──

    private static SettingItemViewModel CreateSettingItemViewModel(SettingDefinition definition)
    {
        var dispatcher = new Mock<IDispatcherService>();
        dispatcher.Setup(d => d.RunOnUIThread(It.IsAny<System.Action>()))
            .Callback<System.Action>(a => a());
        dispatcher.Setup(d => d.RunOnUIThreadAsync(It.IsAny<System.Func<Task>>()))
            .Returns<System.Func<Task>>(f => f());

        var localization = new Mock<ILocalizationService>();
        localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string _) => null!);

        var config = new SettingItemViewModelConfig
        {
            SettingDefinition = definition,
            SettingId = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            InputType = definition.InputType,
            IsSelected = false,
        };

        return new SettingItemViewModel(
            config,
            new Mock<ISettingApplicationService>().Object,
            new Mock<ILogService>().Object,
            dispatcher.Object,
            new Mock<IDialogService>().Object,
            localization.Object,
            new Mock<IEventBus>().Object,
            new Mock<IUserPreferencesService>().Object,
            new Mock<IRegeditLauncher>().Object);
    }
}
