using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ConfigReviewServiceTests : IDisposable
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();
    private readonly Mock<ISystemSettingsDiscoveryService> _mockDiscoveryService = new();
    private readonly Mock<IComboBoxSetupService> _mockComboBoxSetupService = new();
    private readonly Mock<IComboBoxResolver> _mockComboBoxResolver = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IWindowsVersionService> _mockWindowsVersionService = new();

    private ConfigReviewService? _service;

    public ConfigReviewServiceTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

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
            _mockCompatibleSettingsRegistry.Object,
            _mockDiscoveryService.Object,
            _mockComboBoxSetupService.Object,
            _mockComboBoxResolver.Object,
            _mockLocalizationService.Object,
            _mockWindowsVersionService.Object);
        return _service;
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    // -------------------------------------------------------
    // Initial state
    // -------------------------------------------------------

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

    // -------------------------------------------------------
    // Dispose
    // -------------------------------------------------------

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

        // After dispose, raising language changed should not cause errors
        _mockLocalizationService.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        // No exception means unsubscribe worked
    }

    // -------------------------------------------------------
    // EnterReviewModeAsync
    // -------------------------------------------------------

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
        var settingDef = new SettingDefinition
        {
            Id = "privacy-setting",
            Name = "Privacy Setting",
            Description = "Test",
            InputType = InputType.Toggle
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["privacy-setting"] = new SettingStateResult { IsEnabled = false }
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
        var settingDef = new SettingDefinition
        {
            Id = "privacy-setting",
            Name = "Privacy Setting",
            Description = "Test",
            InputType = InputType.Toggle
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["privacy-setting"] = new SettingStateResult { IsEnabled = true }
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
        var settingDef = new SettingDefinition
        {
            Id = "taskbar-clean",
            Name = "Clean Taskbar",
            Description = "Test",
            InputType = InputType.Toggle
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Taskbar"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                // Current state matches config (no value diff) but it's an action setting
                ["taskbar-clean"] = new SettingStateResult { IsEnabled = true }
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

        // Action settings are always registered even when there's no value diff
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
        var settingDef = new SettingDefinition
        {
            Id = SettingIds.ThemeModeWindows,
            Name = "Choose your mode",
            Description = "Test",
            InputType = InputType.Selection
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("WindowsTheme"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [SettingIds.ThemeModeWindows] = new SettingStateResult { CurrentValue = selectedIndex == 0 ? 1 : 0 }
            });

        _mockComboBoxSetupService
            .Setup(c => c.SetupComboBoxOptionsAsync(settingDef, It.IsAny<object?>()))
            .ReturnsAsync(new ComboBoxSetupResult
            {
                Options = new System.Collections.ObjectModel.ObservableCollection<ComboBoxOption>
                {
                    new ComboBoxOption("Light", 0),
                    new ComboBoxOption("Dark", 1)
                },
                SelectedValue = selectedIndex == 0 ? 1 : 0,
                Success = true
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

        // Enter review mode once
        await service.EnterReviewModeAsync(new UnifiedConfigurationFile());
        service.IsInReviewMode.Should().BeTrue();

        // Enter again with a different config
        var newConfig = new UnifiedConfigurationFile();
        await service.EnterReviewModeAsync(newConfig);

        service.ActiveConfig.Should().BeSameAs(newConfig);
        service.TotalChanges.Should().Be(0); // Diffs cleared
    }

    // -------------------------------------------------------
    // ExitReviewMode
    // -------------------------------------------------------

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

        // Register diffs so badge queries return non-zero values
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s1", FeatureModuleId = "Privacy", InputType = InputType.Toggle });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s2", FeatureModuleId = "Privacy", InputType = InputType.Toggle });

        // Verify state exists before exit
        service.GetFeatureDiffCount("Privacy").Should().Be(2);
        service.IsFeatureInConfig(FeatureIds.WindowsApps).Should().BeTrue();

        service.ExitReviewMode();

        // After exit, all badge queries must return cleared/default values
        service.GetFeatureDiffCount("Privacy").Should().Be(0);
        service.GetFeaturePendingDiffCount("Privacy").Should().Be(0);
        service.IsFeatureInConfig(FeatureIds.WindowsApps).Should().BeFalse();
        service.IsFeatureFullyReviewed("Privacy").Should().BeFalse();
    }

    // -------------------------------------------------------
    // GetDiffForSetting
    // -------------------------------------------------------

    [Fact]
    public void GetDiffForSetting_WhenNoDiff_ReturnsNull()
    {
        var service = CreateService();
        service.GetDiffForSetting("nonexistent").Should().BeNull();
    }

    // -------------------------------------------------------
    // SetSettingApproval
    // -------------------------------------------------------

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
            ConfigValueDisplay = "On",
            InputType = InputType.Toggle
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
            FeatureModuleId = "Privacy",
            InputType = InputType.Toggle
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

    // -------------------------------------------------------
    // GetApprovedDiffs
    // -------------------------------------------------------

    [Fact]
    public void GetApprovedDiffs_ReturnsOnlyApprovedAndReviewed()
    {
        var service = CreateService();

        service.RegisterDiff(new ConfigReviewDiff
        {
            SettingId = "approved",
            FeatureModuleId = "Privacy",
            InputType = InputType.Toggle
        });
        service.RegisterDiff(new ConfigReviewDiff
        {
            SettingId = "rejected",
            FeatureModuleId = "Privacy",
            InputType = InputType.Toggle
        });
        service.RegisterDiff(new ConfigReviewDiff
        {
            SettingId = "unreviewed",
            FeatureModuleId = "Privacy",
            InputType = InputType.Toggle
        });

        service.SetSettingApproval("approved", true);
        service.SetSettingApproval("rejected", false);

        var approved = service.GetApprovedDiffs();
        approved.Should().HaveCount(1);
        approved[0].SettingId.Should().Be("approved");
    }

    // -------------------------------------------------------
    // RegisterDiff
    // -------------------------------------------------------

    [Fact]
    public void RegisterDiff_AddsDiff()
    {
        var service = CreateService();
        var diff = new ConfigReviewDiff
        {
            SettingId = "new-diff",
            FeatureModuleId = "Privacy",
            InputType = InputType.Toggle
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
            CurrentValueDisplay = "Old",
            InputType = InputType.Toggle
        });

        service.RegisterDiff(new ConfigReviewDiff
        {
            SettingId = "test",
            FeatureModuleId = "Privacy",
            CurrentValueDisplay = "New",
            InputType = InputType.Toggle
        });

        service.TotalChanges.Should().Be(1);
        service.GetDiffForSetting("test")!.CurrentValueDisplay.Should().Be("New");
    }

    // -------------------------------------------------------
    // Approval counts
    // -------------------------------------------------------

    [Fact]
    public void ApprovedChanges_ReturnsCorrectCount()
    {
        var service = CreateService();

        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s1", FeatureModuleId = "P", InputType = InputType.Toggle });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s2", FeatureModuleId = "P", InputType = InputType.Toggle });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s3", FeatureModuleId = "P", InputType = InputType.Toggle });

        service.SetSettingApproval("s1", true);
        service.SetSettingApproval("s2", false);

        service.ApprovedChanges.Should().Be(1);
        service.ReviewedChanges.Should().Be(2);
    }

    // -------------------------------------------------------
    // Badge / feature tracking
    // -------------------------------------------------------

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
        service.MarkFeatureVisited("Privacy"); // Second call should not fire again

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

        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s1", FeatureModuleId = "Privacy", InputType = InputType.Toggle });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s2", FeatureModuleId = "Privacy", InputType = InputType.Toggle });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s3", FeatureModuleId = "Power", InputType = InputType.Toggle });

        service.GetFeatureDiffCount("Privacy").Should().Be(2);
        service.GetFeatureDiffCount("Power").Should().Be(1);
        service.GetFeatureDiffCount("Nonexistent").Should().Be(0);
    }

    [Fact]
    public void GetFeaturePendingDiffCount_ExcludesReviewedDiffs()
    {
        var service = CreateService();

        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s1", FeatureModuleId = "Privacy", InputType = InputType.Toggle });
        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s2", FeatureModuleId = "Privacy", InputType = InputType.Toggle });

        service.SetSettingApproval("s1", true); // Reviewed

        service.GetFeaturePendingDiffCount("Privacy").Should().Be(1);
    }

    // -------------------------------------------------------
    // IsFeatureInConfig
    // -------------------------------------------------------

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

    // -------------------------------------------------------
    // IsSectionFullyReviewed
    // -------------------------------------------------------

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

    // -------------------------------------------------------
    // IsFeatureFullyReviewed
    // -------------------------------------------------------

    [Fact]
    public void IsFeatureFullyReviewed_WhenNotInReviewMode_ReturnsFalse()
    {
        var service = CreateService();
        service.IsFeatureFullyReviewed("Privacy").Should().BeFalse();
    }

    [Fact]
    public async Task IsFeatureFullyReviewed_WithNoDiffs_ReturnsTrue()
    {
        // A feature that is in config but has zero diffs is considered fully reviewed
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

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(Enumerable.Empty<SettingDefinition>());

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.IsFeatureFullyReviewed("Privacy").Should().BeTrue();
    }

    [Fact]
    public async Task IsFeatureFullyReviewed_WithUnreviewedDiffs_ReturnsFalse()
    {
        var settingDef = new SettingDefinition
        {
            Id = "s1",
            Name = "S1",
            Description = "Test",
            InputType = InputType.Toggle
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { IsEnabled = false }
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

        // Has unreviewed diff - not fully reviewed even without visiting
        service.IsFeatureFullyReviewed("Privacy").Should().BeFalse();

        // Review the diff
        service.SetSettingApproval("s1", true);

        // Fully reviewed once all diffs are reviewed, regardless of visited state
        service.IsFeatureFullyReviewed("Privacy").Should().BeTrue();
    }

    [Fact]
    public async Task IsFeatureFullyReviewed_AllDiffsReviewed_WithoutVisiting_ReturnsTrue()
    {
        // Reproduces Bug: entering review mode while already on a sub-page
        // means MarkFeatureVisited is never called, but reviewing all items
        // should still mark the feature as fully reviewed.
        var settingDef = new SettingDefinition
        {
            Id = "s1",
            Name = "S1",
            Description = "Test",
            InputType = InputType.Toggle
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { IsEnabled = false }
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

        // Review the diff
        service.SetSettingApproval("s1", true);

        // Should be fully reviewed even without visiting
        service.IsFeatureFullyReviewed("Privacy").Should().BeTrue();
    }

    [Fact]
    public async Task IsFeatureFullyReviewed_FiresBadgeStateChanged_WhenLastDiffReviewed()
    {
        var settingDef = new SettingDefinition
        {
            Id = "s1",
            Name = "S1",
            Description = "Test",
            InputType = InputType.Toggle
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { IsEnabled = false }
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

        // Reviewing the last diff should fire BadgeStateChanged
        service.SetSettingApproval("s1", true);

        badgeChangedCount.Should().BeGreaterThan(0);
        service.IsFeatureFullyReviewed("Privacy").Should().BeTrue();
    }

    // -------------------------------------------------------
    // NotifyBadgeStateChanged
    // -------------------------------------------------------

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

    // -------------------------------------------------------
    // Language change re-localization
    // -------------------------------------------------------

    [Fact]
    public async Task LanguageChanged_WhenInReviewMode_RelocalizesDisplayStrings()
    {
        var settingDef = new SettingDefinition
        {
            Id = "s1",
            Name = "Setting 1",
            Description = "Test",
            InputType = InputType.Toggle
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { IsEnabled = false }
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

        // Now change the localization strings
        _mockLocalizationService
            .Setup(l => l.GetString("Common_On"))
            .Returns("Ein");
        _mockLocalizationService
            .Setup(l => l.GetString("Common_Off"))
            .Returns("Aus");

        // Trigger language change
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

    // -------------------------------------------------------
    // NumericRange diff computation
    // -------------------------------------------------------

    [Fact]
    public async Task EnterReviewModeAsync_NumericRange_WithACValueDiff_RegistersDiff()
    {
        var settingDef = new SettingDefinition
        {
            Id = "numeric-setting",
            Name = "Numeric Setting",
            Description = "Test",
            InputType = InputType.NumericRange
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Power"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["numeric-setting"] = new SettingStateResult { CurrentValue = 30 }
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

    // -------------------------------------------------------
    // Selection diff computation
    // -------------------------------------------------------

    [Fact]
    public async Task EnterReviewModeAsync_Selection_WithDifferentIndex_RegistersDiff()
    {
        var settingDef = new SettingDefinition
        {
            Id = "selection-setting",
            Name = "Selection Setting",
            Description = "Test",
            InputType = InputType.Selection
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["selection-setting"] = new SettingStateResult { CurrentValue = 0 }
            });

        _mockComboBoxSetupService
            .Setup(c => c.SetupComboBoxOptionsAsync(settingDef, It.IsAny<object?>()))
            .ReturnsAsync(new ComboBoxSetupResult
            {
                Options = new System.Collections.ObjectModel.ObservableCollection<ComboBoxOption>
                {
                    new ComboBoxOption("Option A", 0),
                    new ComboBoxOption("Option B", 1)
                },
                SelectedValue = 0,
                Success = true
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

    // -------------------------------------------------------
    // Start menu version filtering
    // -------------------------------------------------------

    [Fact]
    public async Task EnterReviewModeAsync_StartMenuClean10_OnWindows11_IsSkipped()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);

        var settingDef = new SettingDefinition
        {
            Id = "start-menu-clean-10",
            Name = "Clean Start Menu (Win10)",
            Description = "Test",
            InputType = InputType.Toggle
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("StartMenu"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["start-menu-clean-10"] = new SettingStateResult { IsEnabled = false }
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

        var settingDef = new SettingDefinition
        {
            Id = "start-menu-clean-11",
            Name = "Clean Start Menu (Win11)",
            Description = "Test",
            InputType = InputType.Toggle
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("StartMenu"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["start-menu-clean-11"] = new SettingStateResult { IsEnabled = false }
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

    // -------------------------------------------------------
    // NumericRange edge cases (guards #482 rendering)
    // -------------------------------------------------------

    [Fact]
    public async Task EnterReviewModeAsync_NumericRange_SameACValue_NoDiff()
    {
        var settingDef = new SettingDefinition
        {
            Id = "nr-same",
            Name = "Numeric Same",
            Description = "Test",
            InputType = InputType.NumericRange
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Power"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["nr-same"] = new SettingStateResult { CurrentValue = 30 }
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
        var settingDef = new SettingDefinition
        {
            Id = "nr-nopower",
            Name = "No Power Settings",
            Description = "Test",
            InputType = InputType.NumericRange
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Power"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["nr-nopower"] = new SettingStateResult { CurrentValue = 50 }
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
                                PowerSettings = null // No PowerSettings dictionary
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
        var settingDef = new SettingDefinition
        {
            Id = "nr-dconly",
            Name = "DC Only NumericRange",
            Description = "Test",
            InputType = InputType.NumericRange
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Power"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["nr-dconly"] = new SettingStateResult { CurrentValue = 30 }
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
                                    ["DCValue"] = 60 // Only DCValue, no ACValue
                                }
                            }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        // Should not register a diff since ACValue is the comparison key and it's missing
        service.GetDiffForSetting("nr-dconly").Should().BeNull();
    }

    // -------------------------------------------------------
    // Multi-feature diff computation (guards #482 for all pages)
    // -------------------------------------------------------

    [Fact]
    public async Task EnterReviewModeAsync_MultipleFeatures_ComputesDiffsPerFeature()
    {
        // Privacy (Toggle)
        var privacyDef = new SettingDefinition
        {
            Id = "priv1",
            Name = "Privacy Setting",
            Description = "Test",
            InputType = InputType.Toggle
        };
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { privacyDef });

        // Power (NumericRange)
        var powerDef = new SettingDefinition
        {
            Id = "pow1",
            Name = "Power Setting",
            Description = "Test",
            InputType = InputType.NumericRange
        };
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Power"))
            .Returns(new[] { powerDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.Is<IReadOnlyList<SettingDefinition>>(
                l => l.Any(s => s.Id == "priv1"))))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["priv1"] = new SettingStateResult { IsEnabled = false }
            });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.Is<IReadOnlyList<SettingDefinition>>(
                l => l.Any(s => s.Id == "pow1"))))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["pow1"] = new SettingStateResult { CurrentValue = 30 }
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
        var settingDef = new SettingDefinition
        {
            Id = "sel-null",
            Name = "Selection Null Index",
            Description = "Test",
            InputType = InputType.Selection
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["sel-null"] = new SettingStateResult { CurrentValue = 0 }
            });

        _mockComboBoxSetupService
            .Setup(c => c.SetupComboBoxOptionsAsync(settingDef, It.IsAny<object?>()))
            .ReturnsAsync(new ComboBoxSetupResult
            {
                Options = new System.Collections.ObjectModel.ObservableCollection<ComboBoxOption>
                {
                    new ComboBoxOption("Option A", 0)
                },
                SelectedValue = 0,
                Success = true
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
                                SelectedIndex = null // Null index
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
        var settingDef = new SettingDefinition
        {
            Id = "power-plan",
            Name = "Power Plan",
            Description = "Test",
            InputType = InputType.Selection
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Power"))
            .Returns(new[] { settingDef });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["power-plan"] = new SettingStateResult
                {
                    CurrentValue = 0,
                    RawValues = new Dictionary<string, object?>
                    {
                        ["ActivePowerPlanGuid"] = "381b4222-f694-41f0-9685-ff5bb260df2e", // Balanced
                        ["ActivePowerPlan"] = "Balanced"
                    }
                }
            });

        _mockComboBoxSetupService
            .Setup(c => c.SetupComboBoxOptionsAsync(settingDef, It.IsAny<object?>()))
            .ReturnsAsync(new ComboBoxSetupResult
            {
                Options = new System.Collections.ObjectModel.ObservableCollection<ComboBoxOption>
                {
                    new ComboBoxOption("Balanced", 0),
                    new ComboBoxOption("High Performance", 1)
                },
                SelectedValue = 0,
                Success = true
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
