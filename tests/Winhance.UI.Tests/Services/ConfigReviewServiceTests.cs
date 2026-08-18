using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.Services;

public class ConfigReviewServiceTests : IDisposable
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ICatalogSettingsRegistry> _mockCatalogSettingsRegistry = new();
    private readonly Mock<ICatalogSettingStateProvider> _mockSettingStateProvider = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IWindowsVersionService> _mockWindowsVersionService = new();

    private ConfigReviewService? _service;

    public ConfigReviewServiceTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _mockLocalizationService.MirrorTryGetString();

        _mockLocalizationService
            .Setup(l => l.GetString("Common_On"))
            .Returns("On");

        _mockLocalizationService
            .Setup(l => l.GetString("Common_Off"))
            .Returns("Off");
    }

    private ConfigReviewService CreateService()
    {
        _service = new ConfigReviewService(
            _mockLogService.Object,
            _mockCatalogSettingsRegistry.Object,
            _mockSettingStateProvider.Object,
            _mockLocalizationService.Object,
            _mockWindowsVersionService.Object);
        return _service;
    }

    public void Dispose()
    {
        _service?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_InitializesWithCorrectDefaults()
    {
        var service = CreateService();

        service.IsInReviewMode.Should().BeFalse();
        service.ActiveConfig.Should().BeNull();
        service.TotalChanges.Should().Be(0);
        service.ApprovedChanges.Should().Be(0);
        service.ReviewedChanges.Should().Be(0);
        service.TotalConfigItems.Should().Be(0);
    }

    [Fact]
    public void CurrentMode_DefaultsToNormal()
    {
        var service = CreateService();

        service.CurrentMode.Should().Be(WinhanceMode.Normal);
        service.CurrentBuilderTarget.Should().Be(BuilderTarget.Config);
        service.IsInReviewMode.Should().BeFalse();
    }

    [Fact]
    public async Task EnterReviewModeAsync_SetsCurrentModeToConfigReview()
    {
        var service = CreateService();

        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());

        service.CurrentMode.Should().Be(WinhanceMode.ConfigReview);
        service.IsInReviewMode.Should().BeTrue();
    }

    [Fact]
    public async Task ExitReviewMode_ResetsCurrentModeToNormal()
    {
        var service = CreateService();
        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());

        service.ExitReviewMode();

        service.CurrentMode.Should().Be(WinhanceMode.Normal);
        service.IsInReviewMode.Should().BeFalse();
    }

    [Fact]
    public async Task EnterReviewModeAsync_RaisesModeChanged()
    {
        var service = CreateService();
        var raised = false;
        service.ModeChanged += (_, _) => raised = true;

        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());

        raised.Should().BeTrue();
    }

    [Fact]
    public async Task ExitReviewMode_RaisesModeChanged()
    {
        var service = CreateService();
        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());
        var raised = false;
        service.ModeChanged += (_, _) => raised = true;

        service.ExitReviewMode();

        raised.Should().BeTrue();
    }

    [Fact]
    public void EnterBuilderMode_SetsBuilderModeAndTarget()
    {
        var service = CreateService();

        service.EnterBuilderMode(BuilderTarget.Autounattend);

        service.CurrentMode.Should().Be(WinhanceMode.Builder);
        service.CurrentBuilderTarget.Should().Be(BuilderTarget.Autounattend);
        service.IsInReviewMode.Should().BeFalse();
    }

    [Fact]
    public void SetBuilderTarget_WhenInBuilderMode_SwitchesTargetAndRaisesModeChanged()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);
        var raised = false;
        service.ModeChanged += (_, _) => raised = true;

        service.SetBuilderTarget(BuilderTarget.Autounattend);

        service.CurrentBuilderTarget.Should().Be(BuilderTarget.Autounattend);
        service.CurrentMode.Should().Be(WinhanceMode.Builder);
        raised.Should().BeTrue();
    }

    [Fact]
    public void SetBuilderTarget_WhenNotInBuilderMode_IsIgnored()
    {
        var service = CreateService();

        service.SetBuilderTarget(BuilderTarget.Autounattend);

        service.CurrentMode.Should().Be(WinhanceMode.Normal);
        service.CurrentBuilderTarget.Should().Be(BuilderTarget.Config);
    }

    [Fact]
    public void EnterNormalMode_FromBuilder_ResetsToNormal()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);
        var raised = false;
        service.ModeChanged += (_, _) => raised = true;

        service.EnterNormalMode();

        service.CurrentMode.Should().Be(WinhanceMode.Normal);
        raised.Should().BeTrue();
    }

    // HasBuilderChanges exists because not every authored input type produces a serializable
    // BuilderEdit — NumericRange and AC/DC power settings do not (see the BuilderEdit scope note).
    // Gating the discard prompt on GetBuilderEdits().Count therefore skipped the prompt entirely
    // for a session that had only moved sliders, and the authoring was discarded silently.

    [Fact]
    public void HasBuilderChanges_OnAFreshBuilderSession_IsFalse()
    {
        var service = CreateService();

        service.EnterBuilderMode(BuilderTarget.Config);

        service.HasBuilderChanges.Should().BeFalse();
    }

    [Fact]
    public void MarkBuilderDirty_InBuilderMode_SetsHasBuilderChangesWithoutRecordingAnEdit()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);

        service.MarkBuilderDirty();

        // This is the whole point: dirty without a serializable edit — the slider case.
        service.HasBuilderChanges.Should().BeTrue();
        service.GetBuilderEdits().Should().BeEmpty();
    }

    [Fact]
    public void MarkBuilderDirty_OutsideBuilderMode_IsIgnored()
    {
        var service = CreateService();

        service.MarkBuilderDirty();

        service.HasBuilderChanges.Should().BeFalse();
    }

    [Fact]
    public void RecordBuilderEdit_AlsoMarksTheSessionDirty()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);

        service.RecordBuilderEdit(new BuilderEdit
        {
            SettingId = "some-setting",
            InputType = InputType.Toggle,
            IsSelected = true
        });

        service.HasBuilderChanges.Should().BeTrue();
    }

    [Fact]
    public void LeavingBuilderMode_ClearsDirtyAlongsideTheEdits()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);
        service.MarkBuilderDirty();

        service.EnterNormalMode();

        service.HasBuilderChanges.Should().BeFalse();
    }

    [Fact]
    public void ReEnteringBuilderMode_StartsCleanAfterAPriorDirtySession()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);
        service.MarkBuilderDirty();
        service.EnterNormalMode();

        service.EnterBuilderMode(BuilderTarget.Config);

        service.HasBuilderChanges.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var service = CreateService();
        service.Dispose();

        var act = () => service.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        var service = CreateService();
        service.Dispose();

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        // No exception means unsubscribe worked
    }

    [Fact]
    public async Task EnterReviewModeAsync_SetsIsInReviewMode()
    {
        var config = new UnifiedConfigurationFile();

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.IsInReviewMode.Should().BeTrue();
        service.ActiveConfig.Should().BeSameAs(config);
    }

    [Fact]
    public async Task EnterReviewModeAsync_FiresReviewModeChangedEvent()
    {
        bool eventFired = false;
        var service = CreateService();
        service.ReviewModeChanged += (_, _) => eventFired = true;

        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());

        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task EnterReviewModeAsync_FiresBadgeStateChangedEvent()
    {
        bool eventFired = false;
        var service = CreateService();
        service.BadgeStateChanged += (_, _) => eventFired = true;

        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());

        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task EnterReviewModeAsync_CountsConfigItems()
    {
        var config = new UnifiedConfigurationFile
        {
            WindowsApps = new ConfigSection
            {
                IsIncluded = true,
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "app1" },
                    new ConfigurationItem { Id = "app2" }
                }
            },
            ExternalApps = new ConfigSection
            {
                IsIncluded = true,
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "ext1" }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.TotalConfigItems.Should().Be(3);
    }

    [Fact]
    public async Task EnterReviewModeAsync_ComputesDiffsForToggleSettings()
    {
        var setting = new Setting
        {
            Id = "privacy-setting",
            Display = new() { Name = "Privacy Setting", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["privacy-setting"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "privacy-setting",
                                Name = "Privacy Setting",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.TotalChanges.Should().Be(1);
        var diff = service.GetDiffForSetting("privacy-setting");
        diff.Should().NotBeNull();
        diff!.CurrentValueDisplay.Should().Be("Off");
        diff.ConfigValueDisplay.Should().Be("On");
    }

    [Fact]
    public async Task EnterReviewModeAsync_NoDiff_WhenCurrentMatchesConfig()
    {
        var setting = new Setting
        {
            Id = "privacy-setting",
            Display = new() { Name = "Privacy Setting", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["privacy-setting"] = new SettingStateResult { Success = true, IsEnabled = true }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "privacy-setting",
                                Name = "Privacy Setting",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.TotalChanges.Should().Be(0);
        service.GetDiffForSetting("privacy-setting").Should().BeNull();
    }

    [Fact]
    public async Task EnterReviewModeAsync_ActionSettings_AlwaysRegistered()
    {
        var setting = new Setting
        {
            Id = "taskbar-clean",
            Display = new() { Name = "Clean Taskbar", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Taskbar", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                // Current state matches config (no value diff) but it's an action setting
                ["taskbar-clean"] = new SettingStateResult { Success = true, IsEnabled = true }
            });

        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Taskbar"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "taskbar-clean",
                                Name = "Clean Taskbar",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.TotalChanges.Should().Be(1);
        var diff = service.GetDiffForSetting("taskbar-clean");
        diff.Should().NotBeNull();
        diff!.IsActionSetting.Should().BeTrue();
        diff.ActionConfirmationMessage.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(0, "Light")]
    [InlineData(1, "Dark")]
    public async Task EnterReviewModeAsync_ThemeWallpaperAction_IncludesThemeName(int selectedIndex, string expectedThemeName)
    {
        var setting = new Setting
        {
            Id = SettingIds.ThemeModeWindows,
            Display = new() { Name = "Choose your mode", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Light" },
                new SettingState { Label = "Dark" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("WindowsTheme", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [SettingIds.ThemeModeWindows] = new SettingStateResult { Success = true, CurrentValue = selectedIndex == 0 ? 1 : 0 }
            });

        _mockLocalizationService
            .Setup(l => l.GetString("Review_Mode_Action_ThemeWallpaper"))
            .Returns("Apply the default {0} wallpaper?");

        _mockLocalizationService
            .Setup(l => l.GetString("Theme_LightNative"))
            .Returns("Light");

        _mockLocalizationService
            .Setup(l => l.GetString("Theme_DarkNative"))
            .Returns("Dark");

        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["WindowsTheme"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = SettingIds.ThemeModeWindows,
                                Name = "Choose your mode",
                                SelectedIndex = selectedIndex,
                                InputType = InputType.Selection
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        var diff = service.GetDiffForSetting(SettingIds.ThemeModeWindows);
        diff.Should().NotBeNull();
        diff!.IsActionSetting.Should().BeTrue();
        diff.ActionConfirmationMessage.Should().Be($"Apply the default {expectedThemeName} wallpaper?");
    }

    [Fact]
    public async Task EnterReviewModeAsync_ClearsPreviousState()
    {
        var service = CreateService();

        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());
        service.IsInReviewMode.Should().BeTrue();

        var newConfig = new UnifiedConfigurationFile();
        await service.EnterReviewModeAsync(newConfig);

        service.ActiveConfig.Should().BeSameAs(newConfig);
        service.TotalChanges.Should().Be(0);
    }

    [Fact]
    public async Task ExitReviewMode_ClearsAllState()
    {
        var service = CreateService();
        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());

        service.ExitReviewMode();

        service.IsInReviewMode.Should().BeFalse();
        service.ActiveConfig.Should().BeNull();
        service.TotalChanges.Should().Be(0);
        service.TotalConfigItems.Should().Be(0);
    }

    [Fact]
    public async Task ExitReviewMode_FiresReviewModeChangedEvent()
    {
        var service = CreateService();
        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());

        bool eventFired = false;
        service.ReviewModeChanged += (_, _) => eventFired = true;

        service.ExitReviewMode();

        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task ExitReviewMode_FiresBadgeStateChangedEvent()
    {
        var service = CreateService();
        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());

        bool eventFired = false;
        service.BadgeStateChanged += (_, _) => eventFired = true;

        service.ExitReviewMode();

        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task ExitReviewMode_ClearsBadgeRelatedState()
    {
        var config = new UnifiedConfigurationFile
        {
            WindowsApps = new ConfigSection
            {
                IsIncluded = true,
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "app1" }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s1", FeatureModuleId = "Privacy" });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s2", FeatureModuleId = "Privacy" });

        service.GetFeatureDiffCount("Privacy").Should().Be(2);
        service.IsFeatureInConfig(FeatureIds.WindowsApps).Should().BeTrue();

        service.ExitReviewMode();

        service.GetFeatureDiffCount("Privacy").Should().Be(0);
        service.GetFeaturePendingDiffCount("Privacy").Should().Be(0);
        service.IsFeatureInConfig(FeatureIds.WindowsApps).Should().BeFalse();
        service.IsFeatureFullyReviewed("Privacy").Should().BeFalse();
    }

    [Fact]
    public void GetDiffForSetting_WhenNoDiff_ReturnsNull()
    {
        var service = CreateService();
        service.GetDiffForSetting("nonexistent").Should().BeNull();
    }

    [Fact]
    public void SetSettingApproval_UpdatesDiffState()
    {
        var service = CreateService();
        var diff = new ConfigReviewDiff
        {
            SettingId = "test",
            SettingName = "Test",
            FeatureModuleId = "Privacy",
            CurrentValueDisplay = "Off",
            ConfigValueDisplay = "On"
        };
        service.RegisterDiff(diff);

        service.SetSettingApproval("test", true);

        var updated = service.GetDiffForSetting("test");
        updated.Should().NotBeNull();
        updated!.IsReviewed.Should().BeTrue();
        updated.IsApproved.Should().BeTrue();
    }

    [Fact]
    public void SetSettingApproval_FiresApprovalCountChangedEvent()
    {
        var service = CreateService();
        service.RegisterDiff(new ConfigReviewDiff
        {
            SettingId = "test",
            SettingName = "Test",
            FeatureModuleId = "Privacy"
        });

        bool eventFired = false;
        service.ApprovalCountChanged += (_, _) => eventFired = true;

        service.SetSettingApproval("test", true);

        eventFired.Should().BeTrue();
    }

    [Fact]
    public void SetSettingApproval_ForNonexistentSetting_DoesNotThrow()
    {
        var service = CreateService();
        var act = () => service.SetSettingApproval("nonexistent", true);
        act.Should().NotThrow();
    }

    [Fact]
    public void GetApprovedDiffs_ReturnsOnlyApprovedAndReviewed()
    {
        var service = CreateService();

        service.RegisterDiff(new ConfigReviewDiff
        {
            SettingId = "approved",
            FeatureModuleId = "Privacy"
        });
        service.RegisterDiff(new ConfigReviewDiff
        {
            SettingId = "rejected",
            FeatureModuleId = "Privacy"
        });
        service.RegisterDiff(new ConfigReviewDiff
        {
            SettingId = "unreviewed",
            FeatureModuleId = "Privacy"
        });

        service.SetSettingApproval("approved", true);
        service.SetSettingApproval("rejected", false);

        var approved = service.GetApprovedDiffs();
        approved.Should().HaveCount(1);
        approved[0].SettingId.Should().Be("approved");
    }

    [Fact]
    public void RegisterDiff_AddsDiff()
    {
        var service = CreateService();
        var diff = new ConfigReviewDiff
        {
            SettingId = "new-diff",
            FeatureModuleId = "Privacy"
        };

        service.RegisterDiff(diff);

        service.TotalChanges.Should().Be(1);
        service.GetDiffForSetting("new-diff").Should().NotBeNull();
    }

    [Fact]
    public void RegisterDiff_ReplacesExistingDiffWithSameId()
    {
        var service = CreateService();

        service.RegisterDiff(new ConfigReviewDiff
        {
            SettingId = "test",
            FeatureModuleId = "Privacy",
            CurrentValueDisplay = "Old"
        });

        service.RegisterDiff(new ConfigReviewDiff
        {
            SettingId = "test",
            FeatureModuleId = "Privacy",
            CurrentValueDisplay = "New"
        });

        service.TotalChanges.Should().Be(1);
        service.GetDiffForSetting("test")!.CurrentValueDisplay.Should().Be("New");
    }

    [Fact]
    public void ApprovedChanges_ReturnsCorrectCount()
    {
        var service = CreateService();

        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s1", FeatureModuleId = "P" });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s2", FeatureModuleId = "P" });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s3", FeatureModuleId = "P" });

        service.SetSettingApproval("s1", true);
        service.SetSettingApproval("s2", false);

        service.ApprovedChanges.Should().Be(1);
        service.ReviewedChanges.Should().Be(2);
    }

    [Fact]
    public void MarkFeatureVisited_TracksVisitedFeatures()
    {
        var service = CreateService();

        bool badgeChanged = false;
        service.BadgeStateChanged += (_, _) => badgeChanged = true;

        service.MarkFeatureVisited("Privacy");

        badgeChanged.Should().BeTrue();
    }

    [Fact]
    public void MarkFeatureVisited_CalledTwice_OnlyFiresOnce()
    {
        var service = CreateService();
        int fireCount = 0;
        service.BadgeStateChanged += (_, _) => fireCount++;

        service.MarkFeatureVisited("Privacy");
        service.MarkFeatureVisited("Privacy");

        fireCount.Should().Be(1);
    }

    [Fact]
    public void GetNavBadgeCount_WhenNotInReviewMode_ReturnsZero()
    {
        var service = CreateService();
        service.GetNavBadgeCount("Optimize").Should().Be(0);
    }

    [Fact]
    public async Task GetNavBadgeCount_ForSoftwareApps_ReturnsConfigItemCount()
    {
        var config = new UnifiedConfigurationFile
        {
            WindowsApps = new ConfigSection
            {
                IsIncluded = true,
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "app1" },
                    new ConfigurationItem { Id = "app2" }
                }
            },
            ExternalApps = new ConfigSection
            {
                IsIncluded = true,
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "ext1" }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.GetNavBadgeCount("SoftwareApps").Should().Be(3);
    }

    [Fact]
    public void GetFeatureDiffCount_ReturnsCountForFeature()
    {
        var service = CreateService();

        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s1", FeatureModuleId = "Privacy" });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s2", FeatureModuleId = "Privacy" });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s3", FeatureModuleId = "Power" });

        service.GetFeatureDiffCount("Privacy").Should().Be(2);
        service.GetFeatureDiffCount("Power").Should().Be(1);
        service.GetFeatureDiffCount("Nonexistent").Should().Be(0);
    }

    [Fact]
    public void GetFeaturePendingDiffCount_ExcludesReviewedDiffs()
    {
        var service = CreateService();

        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s1", FeatureModuleId = "Privacy" });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s2", FeatureModuleId = "Privacy" });

        service.SetSettingApproval("s1", true);

        service.GetFeaturePendingDiffCount("Privacy").Should().Be(1);
    }

    [Fact]
    public async Task IsFeatureInConfig_ReturnsTrueForFeaturesInConfig()
    {
        var config = new UnifiedConfigurationFile
        {
            WindowsApps = new ConfigSection
            {
                IsIncluded = true,
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "app1" }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.IsFeatureInConfig(FeatureIds.WindowsApps).Should().BeTrue();
        service.IsFeatureInConfig(FeatureIds.ExternalApps).Should().BeFalse();
    }

    [Fact]
    public void IsSectionFullyReviewed_WhenNotInReviewMode_ReturnsFalse()
    {
        var service = CreateService();
        service.IsSectionFullyReviewed("Optimize").Should().BeFalse();
    }

    [Fact]
    public async Task IsSectionFullyReviewed_SoftwareApps_UsesSoftwareAppsReviewed()
    {
        var service = CreateService();
        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());

        service.IsSoftwareAppsReviewed = false;
        service.IsSectionFullyReviewed("SoftwareApps").Should().BeFalse();

        service.IsSoftwareAppsReviewed = true;
        service.IsSectionFullyReviewed("SoftwareApps").Should().BeTrue();
    }

    [Fact]
    public void IsFeatureFullyReviewed_WhenNotInReviewMode_ReturnsFalse()
    {
        var service = CreateService();
        service.IsFeatureFullyReviewed("Privacy").Should().BeFalse();
    }

    [Fact]
    public async Task IsFeatureFullyReviewed_WithNoDiffs_ReturnsTrue()
    {
        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "s1" }
                        }
                    }
                }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<bool>()))
            .Returns(Array.Empty<Setting>());

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.IsFeatureFullyReviewed("Privacy").Should().BeTrue();
    }

    [Fact]
    public async Task IsFeatureFullyReviewed_WithUnreviewedDiffs_ReturnsFalse()
    {
        var setting = new Setting
        {
            Id = "s1",
            Display = new() { Name = "S1", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "s1",
                                Name = "S1",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.IsFeatureFullyReviewed("Privacy").Should().BeFalse();

        service.SetSettingApproval("s1", true);

        service.IsFeatureFullyReviewed("Privacy").Should().BeTrue();
    }

    [Fact]
    public async Task IsFeatureFullyReviewed_AllDiffsReviewed_WithoutVisiting_ReturnsTrue()
    {
        // Reproduces Bug: entering review mode while already on a sub-page
        // means MarkFeatureVisited is never called, but reviewing all items
        // should still mark the feature as fully reviewed.
        var setting = new Setting
        {
            Id = "s1",
            Display = new() { Name = "S1", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "s1",
                                Name = "S1",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        // Do NOT call MarkFeatureVisited - simulates entering review mode
        // while already on the sub-page

        service.SetSettingApproval("s1", true);

        service.IsFeatureFullyReviewed("Privacy").Should().BeTrue();
    }

    [Fact]
    public async Task IsFeatureFullyReviewed_FiresBadgeStateChanged_WhenLastDiffReviewed()
    {
        var setting = new Setting
        {
            Id = "s1",
            Display = new() { Name = "S1", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "s1",
                                Name = "S1",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        int badgeChangedCount = 0;
        service.BadgeStateChanged += (_, _) => badgeChangedCount++;

        service.SetSettingApproval("s1", true);

        badgeChangedCount.Should().BeGreaterThan(0);
        service.IsFeatureFullyReviewed("Privacy").Should().BeTrue();
    }

    [Fact]
    public void NotifyBadgeStateChanged_FiresBothEvents()
    {
        var service = CreateService();

        bool badgeFired = false;
        bool approvalFired = false;
        service.BadgeStateChanged += (_, _) => badgeFired = true;
        service.ApprovalCountChanged += (_, _) => approvalFired = true;

        service.NotifyBadgeStateChanged();

        badgeFired.Should().BeTrue();
        approvalFired.Should().BeTrue();
    }

    [Fact]
    public async Task LanguageChanged_WhenInReviewMode_RelocalizesDisplayStrings()
    {
        var setting = new Setting
        {
            Id = "s1",
            Display = new() { Name = "Setting 1", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "s1",
                                Name = "Setting 1",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockLocalizationService
            .Setup(l => l.GetString("Common_On"))
            .Returns("Ein");
        _mockLocalizationService
            .Setup(l => l.GetString("Common_Off"))
            .Returns("Aus");

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        var diff = service.GetDiffForSetting("s1");
        diff.Should().NotBeNull();
        diff!.CurrentValueDisplay.Should().Be("Aus");
        diff.ConfigValueDisplay.Should().Be("Ein");
    }

    [Fact]
    public void LanguageChanged_WhenNotInReviewMode_DoesNothing()
    {
        var service = CreateService();

        // Should not throw
        _mockLocalizationService.Raise(l => l.LanguageChanged += null, EventArgs.Empty);
    }

    [Fact]
    public async Task EnterReviewModeAsync_NumericRange_WithACValueDiff_RegistersDiff()
    {
        var setting = new Setting
        {
            Id = "numeric-setting",
            Display = new() { Name = "Numeric Setting", Description = "Test" },
            Numeric = new() { Min = 0, Max = 3600 }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["numeric-setting"] = new SettingStateResult { Success = true, CurrentValue = 30 }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Power"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "numeric-setting",
                                Name = "Numeric Setting",
                                InputType = InputType.NumericRange,
                                PowerSettings = new Dictionary<string, object>
                                {
                                    ["ACValue"] = 60
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        var diff = service.GetDiffForSetting("numeric-setting");
        diff.Should().NotBeNull();
        diff!.CurrentValueDisplay.Should().Be("30");
        diff.ConfigValueDisplay.Should().Be("60");
    }

    [Fact]
    public async Task EnterReviewModeAsync_Selection_WithDifferentIndex_RegistersDiff()
    {
        var setting = new Setting
        {
            Id = "selection-setting",
            Display = new() { Name = "Selection Setting", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Option A" },
                new SettingState { Label = "Option B" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["selection-setting"] = new SettingStateResult { Success = true, CurrentValue = 0 }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "selection-setting",
                                Name = "Selection Setting",
                                InputType = InputType.Selection,
                                SelectedIndex = 1
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        var diff = service.GetDiffForSetting("selection-setting");
        diff.Should().NotBeNull();
    }

    [Fact]
    public async Task EnterReviewModeAsync_StartMenuClean10_OnWindows11_IsSkipped()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);

        var setting = new Setting
        {
            Id = "start-menu-clean-10",
            Display = new() { Name = "Clean Start Menu (Win10)", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("StartMenu", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["start-menu-clean-10"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["StartMenu"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "start-menu-clean-10",
                                Name = "Clean Start Menu",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.GetDiffForSetting("start-menu-clean-10").Should().BeNull();
    }

    [Fact]
    public async Task EnterReviewModeAsync_StartMenuClean11_OnWindows10_IsSkipped()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(false);

        var setting = new Setting
        {
            Id = "start-menu-clean-11",
            Display = new() { Name = "Clean Start Menu (Win11)", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("StartMenu", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["start-menu-clean-11"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["StartMenu"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "start-menu-clean-11",
                                Name = "Clean Start Menu",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.GetDiffForSetting("start-menu-clean-11").Should().BeNull();
    }

    // The NumericRange edge cases below guard #482 (review-mode rendering).

    [Fact]
    public async Task EnterReviewModeAsync_NumericRange_SameACValue_NoDiff()
    {
        var setting = new Setting
        {
            Id = "nr-same",
            Display = new() { Name = "Numeric Same", Description = "Test" },
            Numeric = new() { Min = 0, Max = 3600 }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["nr-same"] = new SettingStateResult { Success = true, CurrentValue = 30 }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Power"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "nr-same",
                                Name = "Numeric Same",
                                InputType = InputType.NumericRange,
                                PowerSettings = new Dictionary<string, object>
                                {
                                    ["ACValue"] = 30
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.GetDiffForSetting("nr-same").Should().BeNull();
    }

    [Fact]
    public async Task EnterReviewModeAsync_NumericRange_NoPowerSettings_NoDiff()
    {
        var setting = new Setting
        {
            Id = "nr-nopower",
            Display = new() { Name = "No Power Settings", Description = "Test" },
            Numeric = new() { Min = 0, Max = 3600 }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["nr-nopower"] = new SettingStateResult { Success = true, CurrentValue = 50 }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Power"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "nr-nopower",
                                Name = "No Power Settings",
                                InputType = InputType.NumericRange,
                                PowerSettings = null
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.GetDiffForSetting("nr-nopower").Should().BeNull();
    }

    [Fact]
    public async Task EnterReviewModeAsync_NumericRange_WithDCValueOnly_UsesACValueForComparison()
    {
        var setting = new Setting
        {
            Id = "nr-dconly",
            Display = new() { Name = "DC Only NumericRange", Description = "Test" },
            Numeric = new() { Min = 0, Max = 3600 }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["nr-dconly"] = new SettingStateResult { Success = true, CurrentValue = 30 }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Power"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "nr-dconly",
                                Name = "DC Only NumericRange",
                                InputType = InputType.NumericRange,
                                PowerSettings = new Dictionary<string, object>
                                {
                                    ["DCValue"] = 60
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.GetDiffForSetting("nr-dconly").Should().BeNull();
    }

    // The multi-feature diff test below guards #482 for all pages.

    [Fact]
    public async Task EnterReviewModeAsync_MultipleFeatures_ComputesDiffsPerFeature()
    {
        var privacySetting = new Setting
        {
            Id = "priv1",
            Display = new() { Name = "Privacy Setting", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" }
            }
        };
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<bool>()))
            .Returns(new[] { privacySetting });

        var powerSetting = new Setting
        {
            Id = "pow1",
            Display = new() { Name = "Power Setting", Description = "Test" },
            Numeric = new() { Min = 0, Max = 3600 }
        };
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<bool>()))
            .Returns(new[] { powerSetting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.Is<IReadOnlyList<Setting>>(
                l => l.Any(s => s.Id == "priv1"))))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["priv1"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.Is<IReadOnlyList<Setting>>(
                l => l.Any(s => s.Id == "pow1"))))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["pow1"] = new SettingStateResult { Success = true, CurrentValue = 30 }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "priv1",
                                Name = "Privacy Setting",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    },
                    ["Power"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "pow1",
                                Name = "Power Setting",
                                InputType = InputType.NumericRange,
                                PowerSettings = new Dictionary<string, object>
                                {
                                    ["ACValue"] = 60
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.TotalChanges.Should().Be(2);
        service.GetFeatureDiffCount("Privacy").Should().Be(1);
        service.GetFeatureDiffCount("Power").Should().Be(1);

        var privDiff = service.GetDiffForSetting("priv1");
        privDiff.Should().NotBeNull();
        privDiff!.FeatureModuleId.Should().Be("Privacy");

        var powDiff = service.GetDiffForSetting("pow1");
        powDiff.Should().NotBeNull();
        powDiff!.FeatureModuleId.Should().Be("Power");
    }

    [Fact]
    public async Task EnterReviewModeAsync_Selection_WithNullSelectedIndex_NoDiff()
    {
        var setting = new Setting
        {
            Id = "sel-null",
            Display = new() { Name = "Selection Null Index", Description = "Test" },
            States = new[]
            {
                new SettingState { Label = "Option A" },
                new SettingState { Label = "Option B" }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["sel-null"] = new SettingStateResult { Success = true, CurrentValue = 0 }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "sel-null",
                                Name = "Selection Null Index",
                                InputType = InputType.Selection,
                                SelectedIndex = null
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.GetDiffForSetting("sel-null").Should().BeNull();
    }

    [Fact]
    public async Task EnterReviewModeAsync_Selection_WithPowerPlanGuid_DifferentPlan_RegistersDiff()
    {
        // The shipped power-plan setting is OptionSource-driven (0 static States -> ControlKind.PowerPlan);
        // the PowerPlanGuid branch never reads static options, so a mock source suffices.
        var setting = new Setting
        {
            Id = "power-plan",
            Display = new() { Name = "Power Plan", Description = "Test" },
            OptionSource = new Mock<IDynamicOptionSource>().Object
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<bool>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["power-plan"] = new SettingStateResult
                {
                    Success = true,
                    CurrentValue = 0,
                    // The service reads the active plan from the typed DynamicSelection (scheme GUID) +
                    // DynamicSelectionName (raw OS name), so drive those.
                    DynamicSelection = "381b4222-f694-41f0-9685-ff5bb260df2e", // Balanced
                    DynamicSelectionName = "Balanced"
                }
            });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Power"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "power-plan",
                                Name = "Power Plan",
                                InputType = InputType.Selection,
                                PowerPlanGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // High Performance
                                PowerPlanName = "High Performance"
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        var diff = service.GetDiffForSetting("power-plan");
        diff.Should().NotBeNull();
    }
}
