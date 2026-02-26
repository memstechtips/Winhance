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

        service.MarkFeatureVisited("Privacy");

        // Has unreviewed diff
        service.IsFeatureFullyReviewed("Privacy").Should().BeFalse();

        // Review the diff
        service.SetSettingApproval("s1", true);

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
}
