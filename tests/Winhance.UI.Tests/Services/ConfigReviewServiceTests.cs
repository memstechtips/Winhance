using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Optimize.Services;
using Winhance.UI.Features.Common.Services;
using Xunit;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
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
        _mockCatalogSettingsRegistry.ResolveIdsFromFeatures();

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

    private ConfigReviewService CreateService(ICatalogSettingsRegistry? registry = null)
    {
        _service = new ConfigReviewService(
            _mockLogService.Object,
            registry ?? _mockCatalogSettingsRegistry.Object,
            _mockSettingStateProvider.Object,
            _mockLocalizationService.Object,
            _mockWindowsVersionService.Object,
            new FakeOptionProviderRegistry(new PowerService(
                Mock.Of<ILogService>(), Mock.Of<IPowerSettingsQueryService>(), Mock.Of<IPowerSchemeOperations>())));
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

        await service.EnterReviewModeAsync(new WinhanceConfigFile());

        service.CurrentMode.Should().Be(WinhanceMode.ConfigReview);
        service.IsInReviewMode.Should().BeTrue();
    }

    [Fact]
    public async Task EnterReviewModeAsync_KeepsTheSetAsideNamesUntilExit()
    {
        var service = CreateService();
        var names = new[] { "Timeline Suggestions (Privacy)" };

        await service.EnterReviewModeAsync(new WinhanceConfigFile(), false, names);
        service.SetAside.Should().Equal(names);

        service.ExitReviewMode();
        service.SetAside.Should().BeEmpty();
    }

    [Fact]
    public async Task ExitReviewMode_ResetsCurrentModeToNormal()
    {
        var service = CreateService();
        await service.EnterReviewModeAsync(new WinhanceConfigFile());

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

        await service.EnterReviewModeAsync(new WinhanceConfigFile());

        raised.Should().BeTrue();
    }

    [Fact]
    public async Task ExitReviewMode_RaisesModeChanged()
    {
        var service = CreateService();
        await service.EnterReviewModeAsync(new WinhanceConfigFile());
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

    // HasBuilderChanges exists because not every authored input shape produces a serializable
    // ChoiceValue. Gating the discard prompt on GetBuilderEdits().Count therefore skipped the prompt
    // entirely for a session whose authoring recorded nothing, and that work was discarded silently.

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
        var seeded = service.GetBuilderEdits().Count;

        service.MarkBuilderDirty();

        // This is the whole point: dirty without any recorded edit.
        service.HasBuilderChanges.Should().BeTrue();
        service.GetBuilderEdits().Should().HaveCount(seeded);
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

        service.RecordBuilderEdit(new SettingChoice("some-setting", new ChoiceValue.Toggle(true)));

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
        var config = new WinhanceConfigFile();

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

        await service.EnterReviewModeAsync(new WinhanceConfigFile());

        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task EnterReviewModeAsync_FiresBadgeStateChangedEvent()
    {
        bool eventFired = false;
        var service = CreateService();
        service.BadgeStateChanged += (_, _) => eventFired = true;

        await service.EnterReviewModeAsync(new WinhanceConfigFile());

        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task EnterReviewModeAsync_CountsConfigItems()
    {
        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("Privacy Setting"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["privacy-setting"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("Privacy Setting"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["privacy-setting"] = new SettingStateResult { Success = true, IsEnabled = true }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("Clean Taskbar"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Taskbar", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                // Current state matches config (no value diff) but it's an action setting
                ["taskbar-clean"] = new SettingStateResult { Success = true, IsEnabled = true }
            });

        var config = new WinhanceConfigFile
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

    private static Setting AlbumSetting() => new()
    {
        Id = "album-setting",
        Display = new() { Name = TestKeys.Of("Album"), Description = TestKeys.Of("Test") },
        TextBox = new(new TextRule("^.{1,}$", UpperCase: false, TestKeys.Of("Choose a folder."))),
    };

    private WinhanceConfigFile AlbumConfig(string folder, string seeded)
    {
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("WindowsTheme", It.IsAny<CatalogScope>()))
            .Returns(new[] { AlbumSetting() });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["album-setting"] = new SettingStateResult { Success = true, CurrentValue = seeded },
            });

        return new WinhanceConfigFile
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
                                Id = "album-setting",
                                Name = "Album",
                                Text = folder,
                                InputType = InputType.TextBox,
                            },
                        },
                    },
                },
            },
        };
    }

    [Fact]
    public async Task EnterReviewModeAsync_ComputesDiffsForTextBoxSettings()
    {
        var config = AlbumConfig(@"D:\Wallpapers", @"C:\Users\Public\Pictures");

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.TotalChanges.Should().Be(1);
        var diff = service.GetDiffForSetting("album-setting");
        diff.Should().NotBeNull();
        diff!.CurrentValueDisplay.Should().Be(@"C:\Users\Public\Pictures");
        diff.ConfigValueDisplay.Should().Be(@"D:\Wallpapers");
    }

    [Fact]
    public async Task EnterReviewModeAsync_NoDiff_WhenTheBoxAlreadyHoldsTheConfigsText()
    {
        var config = AlbumConfig(@"D:\Wallpapers", @"D:\Wallpapers");

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.TotalChanges.Should().Be(0);
        service.GetDiffForSetting("album-setting").Should().BeNull();
    }

    [Fact]
    public async Task EnterReviewModeAsync_ClearsPreviousState()
    {
        var service = CreateService();

        await service.EnterReviewModeAsync(new WinhanceConfigFile());
        service.IsInReviewMode.Should().BeTrue();

        var newConfig = new WinhanceConfigFile();
        await service.EnterReviewModeAsync(newConfig);

        service.ActiveConfig.Should().BeSameAs(newConfig);
        service.TotalChanges.Should().Be(0);
    }

    [Fact]
    public async Task ExitReviewMode_ClearsAllState()
    {
        var service = CreateService();
        await service.EnterReviewModeAsync(new WinhanceConfigFile());

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
        await service.EnterReviewModeAsync(new WinhanceConfigFile());

        bool eventFired = false;
        service.ReviewModeChanged += (_, _) => eventFired = true;

        service.ExitReviewMode();

        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task ExitReviewMode_FiresBadgeStateChangedEvent()
    {
        var service = CreateService();
        await service.EnterReviewModeAsync(new WinhanceConfigFile());

        bool eventFired = false;
        service.BadgeStateChanged += (_, _) => eventFired = true;

        service.ExitReviewMode();

        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task ExitReviewMode_ClearsBadgeRelatedState()
    {
        var config = new WinhanceConfigFile
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
    public void RegisterDiff_FilesTheDiffUnderTheSettingsOwnFeature()
    {
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { ToggleSetting("s1") });
        var service = CreateService();

        service.RegisterDiff(new ConfigReviewDiff { SettingId = "s1", FeatureModuleId = "Taskbar" });

        service.GetDiffForSetting("s1")!.FeatureModuleId.Should().Be("Privacy");
        service.GetFeatureDiffCount("Taskbar").Should().Be(0);
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
        var config = new WinhanceConfigFile
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
        var config = new WinhanceConfigFile
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
        await service.EnterReviewModeAsync(new WinhanceConfigFile());

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
        var config = new WinhanceConfigFile
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
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
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
            Display = new() { Name = TestKeys.Of("S1"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("S1"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("S1"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("Setting 1"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["s1"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("Numeric Setting"), Description = TestKeys.Of("Test") },
            Numeric = new() { Min = 0, Max = 3600 }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["numeric-setting"] = new SettingStateResult { Success = true, CurrentValue = 30 }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("Selection Setting"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = TestKeys.Of("Option A") },
                new SettingState { Label = TestKeys.Of("Option B") }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["selection-setting"] = new SettingStateResult { Success = true, CurrentValue = 0 }
            });

        var config = new WinhanceConfigFile
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
    public async Task EnterReviewModeAsync_ThemeMode_IsAnOrdinarySelectionDiff()
    {
        var setting = new Setting
        {
            Id = "theme-mode-windows",
            Display = new() { Name = TestKeys.Of("Theme"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Setting.ThemeModeWindows.Option0 },
                new SettingState { Label = LocKey.Setting.ThemeModeWindows.Option1 }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("WindowsTheme", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["theme-mode-windows"] = new SettingStateResult { Success = true, CurrentValue = 0 }
            });

        var config = new WinhanceConfigFile
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
                                Id = "theme-mode-windows",
                                Name = "Theme",
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

        var diff = service.GetDiffForSetting("theme-mode-windows");
        diff.Should().NotBeNull();
        diff!.IsActionSetting.Should().BeFalse();
        diff.ActionConfirmationMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task EnterReviewModeAsync_StartMenuClean10_OnWindows11_IsSkipped()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);

        var setting = new Setting
        {
            Id = "start-menu-clean-10",
            Display = new() { Name = TestKeys.Of("Clean Start Menu (Win10)"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("StartMenu", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["start-menu-clean-10"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("Clean Start Menu (Win11)"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("StartMenu", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["start-menu-clean-11"] = new SettingStateResult { Success = true, IsEnabled = false }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("Numeric Same"), Description = TestKeys.Of("Test") },
            Numeric = new() { Min = 0, Max = 3600 }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["nr-same"] = new SettingStateResult { Success = true, CurrentValue = 30 }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("No Power Settings"), Description = TestKeys.Of("Test") },
            Numeric = new() { Min = 0, Max = 3600 }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["nr-nopower"] = new SettingStateResult { Success = true, CurrentValue = 50 }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("DC Only NumericRange"), Description = TestKeys.Of("Test") },
            Numeric = new() { Min = 0, Max = 3600 }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["nr-dconly"] = new SettingStateResult { Success = true, CurrentValue = 30 }
            });

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("Privacy Setting"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled }
            }
        };
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { privacySetting });

        var powerSetting = new Setting
        {
            Id = "pow1",
            Display = new() { Name = TestKeys.Of("Power Setting"), Description = TestKeys.Of("Test") },
            Numeric = new() { Min = 0, Max = 3600 }
        };
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<CatalogScope>()))
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

        var config = new WinhanceConfigFile
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
            Display = new() { Name = TestKeys.Of("Selection Null Index"), Description = TestKeys.Of("Test") },
            States = new[]
            {
                new SettingState { Label = TestKeys.Of("Option A") },
                new SettingState { Label = TestKeys.Of("Option B") }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["sel-null"] = new SettingStateResult { Success = true, CurrentValue = 0 }
            });

        var config = new WinhanceConfigFile
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

    private static Setting ToggleSetting(string id) => new()
    {
        Id = id,
        Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of("Test") },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Enabled },
            new SettingState { Label = LocKey.Common.Disabled }
        }
    };

    private static ConfigurationItem ToggleItem(string id, bool on) =>
        new() { Id = id, Name = id, IsSelected = on, InputType = InputType.Toggle };

    private static ConfigSection Included(params ConfigurationItem[] items) => new() { IsIncluded = true, Items = items };

    private void ArrangePrivacyToggleThatIsOff(string id)
    {
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Privacy", It.IsAny<CatalogScope>()))
            .Returns(new[] { ToggleSetting(id) });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [id] = new SettingStateResult { Success = true, IsEnabled = false }
            });
    }

    private async Task<ICatalogSettingsRegistry> ShippedCatalogOnWindows11Async()
    {
        _mockWindowsVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(26100);
        var existence = new Mock<ICatalogPowerExistenceFilter>();
        existence
            .Setup(e => e.FilterAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync((IReadOnlyList<Setting> settings) => settings);

        var registry = new CatalogSettingsRegistry(
            _mockWindowsVersionService.Object, Mock.Of<IHardwareDetectionService>(), existence.Object);
        await registry.InitializeAsync();
        return registry;
    }

    [Theory]
    [InlineData("explorer-customization-short-date", "Short Date Format")]
    [InlineData("explorer-customization-first-day-of-week", "First Day of Week")]
    [InlineData("explorer-customization-number-decimal", "Number Decimal Symbol")]
    [InlineData("explorer-customization-list-separator", "List Separator")]
    [InlineData("explorer-customization-measurement-system", "Measurement System")]
    [InlineData("explorer-customization-currency-decimal", "Currency Decimal Symbol")]
    public async Task EnterReviewModeAsync_AFileFromTheReleasedApp_DiffsAMovedSettingUnderTheFeatureItLivesInNow(
        string settingId, string releasedName)
    {
        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [settingId] = new SettingStateResult { Success = true, CurrentValue = 1 }
            });

        // Winhance 26.06.12 writes these ids under the Explorer group.
        var item = new ConfigurationItem { Id = settingId, Name = releasedName, InputType = InputType.Selection, SelectedIndex = 0 };
        var config = new WinhanceConfigFile
        {
            Customize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection> { [FeatureIds.ExplorerCustomization] = Included(item) }
            }
        };

        var service = CreateService(await ShippedCatalogOnWindows11Async());
        await service.EnterReviewModeAsync(config);

        service.GetFeatureDiffCount(FeatureIds.TimeRegionLanguage).Should().Be(1);
        service.GetFeatureDiffCount(FeatureIds.ExplorerCustomization).Should().Be(0);
        service.IsFeatureInConfig(FeatureIds.TimeRegionLanguage).Should().BeTrue();
        service.GetDiffForSetting(settingId)!.FeatureModuleId.Should().Be(FeatureIds.TimeRegionLanguage);

        service.SetSettingApproval(settingId, true);

        service.GetApprovedDiffs().Should().ContainSingle().Which.ConfigItem.Should().BeSameAs(item);
    }

    [Fact]
    public async Task EnterReviewModeAsync_ARetiredWin10Id_IsReviewedAsTheSettingItWasMergedInto()
    {
        const string mergedId = "explorer-customization-thispc-folder-desktop";
        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [mergedId] = new SettingStateResult { Success = true, IsEnabled = false }
            });
        var item = ToggleItem(mergedId + "-win10", on: true);
        var config = new WinhanceConfigFile
        {
            Customize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection> { [FeatureIds.ExplorerCustomization] = Included(item) }
            }
        };

        var service = CreateService(await ShippedCatalogOnWindows11Async());
        await service.EnterReviewModeAsync(config);

        service.GetDiffForSetting(mergedId)!.ConfigItem.Should().BeSameAs(item);
    }

    [Fact]
    public async Task EnterReviewModeAsync_AnIdTheCatalogDoesNotKnow_IsSkippedWithoutDisturbingItsNeighbours()
    {
        ArrangePrivacyToggleThatIsOff("s1");
        var config = new WinhanceConfigFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = Included(ToggleItem("retired-setting", on: true), ToggleItem("s1", on: true))
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.TotalChanges.Should().Be(1);
        service.GetDiffForSetting("retired-setting").Should().BeNull();
        service.GetDiffForSetting("s1").Should().NotBeNull();
    }

    [Fact]
    public async Task EnterReviewModeAsync_ASectionTheUserLeftOut_IsNotReviewed()
    {
        ArrangePrivacyToggleThatIsOff("s1");
        var config = new WinhanceConfigFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new() { IsIncluded = false, Items = new[] { ToggleItem("s1", on: true) } }
                }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.TotalChanges.Should().Be(0);
        service.GetDiffForSetting("s1").Should().BeNull();
    }

    [Fact]
    public async Task EnterReviewModeAsync_AnIdListedInTwoGroups_IsReviewedOnceFromTheFirst()
    {
        ArrangePrivacyToggleThatIsOff("s1");
        var first = ToggleItem("s1", on: true);
        var config = new WinhanceConfigFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection> { ["Privacy"] = Included(first) }
            },
            Customize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection> { ["Taskbar"] = Included(ToggleItem("s1", on: false)) }
            }
        };

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        service.TotalChanges.Should().Be(1);
        service.GetDiffForSetting("s1")!.ConfigItem.Should().BeSameAs(first);
        _mockLogService.Verify(
            l => l.Log(LogLevel.Debug, It.Is<string>(m => m.Contains("'s1' again")), null, It.IsAny<string>()),
            Times.Once);
    }

    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    private void ArrangePowerPlanCard(SettingStateResult state)
    {
        var setting = new Setting
        {
            Id = "power-plan",
            Display = new() { Name = TestKeys.Of("Power Plan"), Description = TestKeys.Of("Test") },
            Options = new(OptionSource.PowerPlans)
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("Power", It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult> { ["power-plan"] = state });
    }

    private static WinhanceConfigFile PowerPlanConfig(ConfigurationItem item) => new()
    {
        Optimize = new FeatureGroupSection
        {
            IsIncluded = true,
            Features = new Dictionary<string, ConfigSection>
            {
                ["Power"] = new ConfigSection
                {
                    IsIncluded = true,
                    Items = new List<ConfigurationItem> { item }
                }
            }
        }
    };

    [Fact]
    public async Task EnterReviewModeAsync_KeyedSelection_LegacyPowerPlanGuid_RegistersDiff()
    {
        // A file written before 26.09.10 spells the plan as PowerPlanGuid/PowerPlanName.
        ArrangePowerPlanCard(new SettingStateResult
        {
            Success = true,
            CurrentValue = 0,
            DynamicOptions =
            [
                new DynamicOption("Balanced", BalancedGuid),
                new DynamicOption("High performance", HighPerformanceGuid),
            ],
            DynamicSelection = BalancedGuid,
        });

        var service = CreateService();
        await service.EnterReviewModeAsync(PowerPlanConfig(new ConfigurationItem
        {
            Id = "power-plan",
            Name = "Power Plan",
            InputType = InputType.Selection,
            PowerPlanGuid = HighPerformanceGuid,
            PowerPlanName = "High Performance"
        }));

        var diff = service.GetDiffForSetting("power-plan");
        diff.Should().NotBeNull();
        diff!.CurrentValueDisplay.Should().Be("Balanced");
        diff.ConfigValueDisplay.Should().Be("High performance");
    }

    [Fact]
    public async Task EnterReviewModeAsync_KeyedSelection_MachineReadsNoKey_RelocalizesTheUnknownText()
    {
        // Both sides have to be non-empty or the card counts a change nobody can review.
        _mockLocalizationService.PresentKey("ConfigReview_UnknownValue", "Unknown");
        ArrangePowerPlanCard(new SettingStateResult { Success = true, CurrentValue = 0 });

        var service = CreateService();
        await service.EnterReviewModeAsync(PowerPlanConfig(new ConfigurationItem
        {
            Id = "power-plan",
            Name = "Power Plan",
            InputType = InputType.Selection,
            SelectedKey = HighPerformanceGuid,
            SelectedKeyLabel = "High Performance"
        }));

        var diff = service.GetDiffForSetting("power-plan");
        diff.Should().NotBeNull();
        diff!.CurrentValueDisplay.Should().Be("Unknown");
        diff.ConfigValueDisplay.Should().Be("High Performance");

        _mockLocalizationService.PresentKey("ConfigReview_UnknownValue", "Unbekannt");
        _mockLocalizationService.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        service.GetDiffForSetting("power-plan")!.CurrentValueDisplay.Should().Be("Unbekannt");
    }

    [Fact]
    public async Task EnterReviewModeAsync_KeyedSelection_TheWinhancePlanUnderAGuidWindowsAssigned_IsNotADiff()
    {
        const string assignedGuid = "9f3c1a52-7b1e-4c55-8f0a-2d6e4b7a9c11";
        ArrangePowerPlanCard(new SettingStateResult
        {
            Success = true,
            CurrentValue = 0,
            DynamicOptions =
            [
                new DynamicOption("Balanced", BalancedGuid),
                new DynamicOption("Winhance Power Plan", assignedGuid),
            ],
            DynamicSelection = assignedGuid,
        });

        var service = CreateService();
        await service.EnterReviewModeAsync(PowerPlanConfig(new ConfigurationItem
        {
            Id = "power-plan",
            Name = "Power Plan",
            InputType = InputType.Selection,
            SelectedKey = PowerPlanCatalog.WinhancePowerPlanGuid,
            SelectedKeyLabel = "Winhance Power Plan"
        }));

        service.GetDiffForSetting("power-plan").Should().BeNull();
    }

    [Fact]
    public void An_answer_file_setting_cannot_be_excluded()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Autounattend);

        service.SetIncluded("autounattend-hardware-checks", false);

        service.IsIncluded("autounattend-hardware-checks").Should().BeTrue();
        service.HasBuilderChanges.Should().BeFalse();
    }

    [Fact]
    public void A_setting_nobody_touched_is_in_the_file()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);

        service.IsIncluded("privacy-advertising-id").Should().BeTrue();
    }

    [Fact]
    public void Excluding_a_setting_takes_it_out_and_marks_the_session_dirty()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);

        service.SetIncluded("privacy-advertising-id", false);

        service.IsIncluded("privacy-advertising-id").Should().BeFalse();
        service.IsIncluded("security-remote-assistance").Should().BeTrue();
        service.HasBuilderChanges.Should().BeTrue();
    }

    [Fact]
    public void Including_a_setting_that_was_never_excluded_leaves_the_session_clean()
    {
        // Every card writes its own state back on a rebuild, so an equal write arrives constantly.
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);

        service.SetIncluded("privacy-advertising-id", true);

        service.HasBuilderChanges.Should().BeFalse();
    }

    [Fact]
    public void Leaving_Builder_puts_every_excluded_setting_back()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);
        service.SetIncluded("privacy-advertising-id", false);

        service.EnterNormalMode();

        service.IsIncluded("privacy-advertising-id").Should().BeTrue();
        service.HasBuilderChanges.Should().BeFalse();
    }

    [Fact]
    public void A_second_Builder_session_starts_with_nothing_excluded()
    {
        var service = CreateService();
        service.EnterBuilderMode(BuilderTarget.Config);
        service.SetIncluded("privacy-advertising-id", false);
        service.EnterNormalMode();

        service.EnterBuilderMode(BuilderTarget.Config);

        service.IsIncluded("privacy-advertising-id").Should().BeTrue();
    }

    [Fact]
    public void Outside_an_authoring_mode_an_exclusion_is_ignored()
    {
        var service = CreateService();

        service.SetIncluded("privacy-advertising-id", false);

        service.IsIncluded("privacy-advertising-id").Should().BeTrue();
        service.HasBuilderChanges.Should().BeFalse();
    }
}
