using System.IO;
using FluentAssertions;
using Microsoft.UI.Xaml;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class AppItemViewModelTests
{
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IDispatcherService> _mockDispatcher = new();
    private readonly Mock<IThemeService> _mockThemeService = new();
    private readonly ItemDefinition _defaultDefinition;

    public AppItemViewModelTests()
    {
        // Set up dispatcher to execute actions synchronously
        _mockDispatcher
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        // Default to dark theme so existing tests are unaffected.
        _mockThemeService
            .Setup(t => t.GetEffectiveTheme())
            .Returns(ElementTheme.Dark);

        _defaultDefinition = new ItemDefinition
        {
            Id = "test-app",
            Name = "Test App",
            Description = "A test application",
            GroupName = "TestGroup",
            IsInstalled = false,
            CanBeReinstalled = true,
        };
    }

    private AppItemViewModel CreateViewModel(ItemDefinition? definition = null)
    {
        return new AppItemViewModel(
            definition ?? _defaultDefinition,
            _mockLocalization.Object,
            _mockDispatcher.Object,
            _mockThemeService.Object);
    }

    // -------------------------------------------------------
    // Constructor / property passthrough
    // -------------------------------------------------------

    [Fact]
    public void Constructor_SetsPropertiesFromDefinition()
    {
        var vm = CreateViewModel();

        vm.Name.Should().Be("Test App");
        vm.Description.Should().Be("A test application");
        vm.GroupName.Should().Be("TestGroup");
        vm.Id.Should().Be("test-app");
        vm.Definition.Should().BeSameAs(_defaultDefinition);
    }

    [Fact]
    public void Constructor_SubscribesToLanguageChanged()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Raise the event and verify property changed notifications fire
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalization.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(vm.InstalledStatusText));
        changedProperties.Should().Contain(nameof(vm.ReinstallableStatusText));
    }

    [Fact]
    public void GroupName_WhenNull_ReturnsEmptyString()
    {
        var def = new ItemDefinition
        {
            Id = "no-group",
            Name = "NoGroup",
            Description = "desc",
            GroupName = null,
        };

        var vm = CreateViewModel(def);

        vm.GroupName.Should().BeEmpty();
    }

    // -------------------------------------------------------
    // IsSelected
    // -------------------------------------------------------

    [Fact]
    public void IsSelected_DefaultIsFalse()
    {
        var vm = CreateViewModel();

        vm.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void IsSelected_SetTrue_RaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsSelected))
                raised = true;
        };

        vm.IsSelected = true;

        vm.IsSelected.Should().BeTrue();
        raised.Should().BeTrue();
    }

    // -------------------------------------------------------
    // IsInstalled
    // -------------------------------------------------------

    [Fact]
    public void IsInstalled_SetToNewValue_UpdatesDefinitionAndRaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        vm.IsInstalled = true;

        vm.IsInstalled.Should().BeTrue();
        _defaultDefinition.IsInstalled.Should().BeTrue();
        changedProperties.Should().Contain(nameof(vm.InstalledStatusText));
    }

    [Fact]
    public void IsInstalled_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        _defaultDefinition.IsInstalled = false;
        var vm = CreateViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        vm.IsInstalled = false; // same value

        changedProperties.Should().BeEmpty();
    }

    [Fact]
    public void IsInstalled_SetTrue_DispatchesToUIThread()
    {
        var vm = CreateViewModel();

        vm.IsInstalled = true;

        _mockDispatcher.Verify(d => d.RunOnUIThread(It.IsAny<Action>()), Times.Once);
    }

    // -------------------------------------------------------
    // InstalledStatusText / ReinstallableStatusText
    // -------------------------------------------------------

    [Fact]
    public void InstalledStatusText_WhenInstalled_ReturnsInstalledKey()
    {
        _mockLocalization
            .Setup(l => l.GetString("Status_Installed"))
            .Returns("Installed");
        _mockLocalization
            .Setup(l => l.GetString("Status_NotInstalled"))
            .Returns("Not Installed");

        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
            IsInstalled = true,
        };
        var vm = CreateViewModel(def);

        vm.InstalledStatusText.Should().Be("Installed");
    }

    [Fact]
    public void InstalledStatusText_WhenNotInstalled_ReturnsNotInstalledKey()
    {
        _mockLocalization
            .Setup(l => l.GetString("Status_NotInstalled"))
            .Returns("Not Installed");

        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
            IsInstalled = false,
        };
        var vm = CreateViewModel(def);

        vm.InstalledStatusText.Should().Be("Not Installed");
    }

    [Fact]
    public void ReinstallableStatusText_WhenCanReinstall_ReturnsCanReinstallKey()
    {
        _mockLocalization
            .Setup(l => l.GetString("Status_CanReinstall"))
            .Returns("Can Reinstall");

        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
            CanBeReinstalled = true,
        };
        var vm = CreateViewModel(def);

        vm.ReinstallableStatusText.Should().Be("Can Reinstall");
    }

    [Fact]
    public void ReinstallableStatusText_WhenCannotReinstall_ReturnsCannotReinstallKey()
    {
        _mockLocalization
            .Setup(l => l.GetString("Status_CannotReinstall"))
            .Returns("Cannot Reinstall");

        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
            CanBeReinstalled = false,
        };
        var vm = CreateViewModel(def);

        vm.ReinstallableStatusText.Should().Be("Cannot Reinstall");
    }

    // -------------------------------------------------------
    // ItemTypeDescription
    // -------------------------------------------------------

    [Fact]
    public void ItemTypeDescription_WithCapabilityName_ReturnsLegacyCapability()
    {
        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
            CapabilityName = "SomeCapability",
        };
        var vm = CreateViewModel(def);

        vm.ItemTypeDescription.Should().Be("Legacy Capability");
    }

    [Fact]
    public void ItemTypeDescription_WithOptionalFeatureName_ReturnsOptionalFeature()
    {
        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
            OptionalFeatureName = "SomeFeature",
        };
        var vm = CreateViewModel(def);

        vm.ItemTypeDescription.Should().Be("Optional Feature");
    }

    [Fact]
    public void ItemTypeDescription_WithAppxPackageName_ReturnsAppXPackage()
    {
        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
            AppxPackageName = new[] { "SomePackage" },
        };
        var vm = CreateViewModel(def);

        vm.ItemTypeDescription.Should().Be("AppX Package");
    }

    [Fact]
    public void ItemTypeDescription_WithNoSpecialNames_ReturnsEmpty()
    {
        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
        };
        var vm = CreateViewModel(def);

        vm.ItemTypeDescription.Should().BeEmpty();
    }

    [Fact]
    public void ItemTypeDescription_CapabilityTakesPriorityOverOptionalFeature()
    {
        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
            CapabilityName = "Cap",
            OptionalFeatureName = "Feat",
            AppxPackageName = new[] { "Pkg" },
        };
        var vm = CreateViewModel(def);

        vm.ItemTypeDescription.Should().Be("Legacy Capability");
    }

    // -------------------------------------------------------
    // WebsiteUrl
    // -------------------------------------------------------

    [Fact]
    public void WebsiteUrl_ReflectsDefinition()
    {
        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
            WebsiteUrl = "https://example.com",
        };
        var vm = CreateViewModel(def);

        vm.WebsiteUrl.Should().Be("https://example.com");
    }

    [Fact]
    public void WebsiteUrl_WhenNull_ReturnsNull()
    {
        var vm = CreateViewModel();

        vm.WebsiteUrl.Should().BeNull();
    }

    // -------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        var vm = CreateViewModel();
        vm.Dispose();

        // After dispose, raising LanguageChanged should not trigger PropertyChanged
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalization.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        changedProperties.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var vm = CreateViewModel();

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }

    // -------------------------------------------------------
    // CanBeReinstalled
    // -------------------------------------------------------

    [Fact]
    public void CanBeReinstalled_ReflectsDefinition()
    {
        var def = new ItemDefinition
        {
            Id = "x",
            Name = "x",
            Description = "x",
            CanBeReinstalled = false,
        };
        var vm = CreateViewModel(def);

        vm.CanBeReinstalled.Should().BeFalse();
    }

    // -------------------------------------------------------
    // Fallback category booleans (drive XAML icon visibility)
    // -------------------------------------------------------

    [Fact]
    public void IsAppXFallback_WhenAppxAndNoIcon_IsTrue()
    {
        var def = new ItemDefinition
        {
            Id = "app1",
            Name = "App 1",
            Description = "",
            AppxPackageName = new[] { "Microsoft.App1" },
        };
        var vm = CreateViewModel(def);

        vm.IsAppXFallback.Should().BeTrue();
        vm.IsCapabilityFallback.Should().BeFalse();
        vm.IsOptionalFeatureFallback.Should().BeFalse();
    }

    [Fact]
    public void IsCapabilityFallback_WhenCapabilityAndNoIcon_IsTrue()
    {
        var def = new ItemDefinition
        {
            Id = "cap1",
            Name = "Cap 1",
            Description = "",
            CapabilityName = "App.Support.IE.Mode",
        };
        var vm = CreateViewModel(def);

        vm.IsCapabilityFallback.Should().BeTrue();
        vm.IsAppXFallback.Should().BeFalse();
        vm.IsOptionalFeatureFallback.Should().BeFalse();
    }

    [Fact]
    public void IsOptionalFeatureFallback_WhenOptionalFeatureAndNoIcon_IsTrue()
    {
        var def = new ItemDefinition
        {
            Id = "feat1",
            Name = "Feat 1",
            Description = "",
            OptionalFeatureName = "Containers-DisposableClientVM",
        };
        var vm = CreateViewModel(def);

        vm.IsOptionalFeatureFallback.Should().BeTrue();
        vm.IsAppXFallback.Should().BeFalse();
        vm.IsCapabilityFallback.Should().BeFalse();
    }

    [Fact]
    public void IsAppXFallback_DefaultsToTrueWhenNoCategoryMatches()
    {
        var def = new ItemDefinition { Id = "x", Name = "X", Description = "" };
        var vm = CreateViewModel(def);

        vm.IsAppXFallback.Should().BeTrue();
        vm.IsCapabilityFallback.Should().BeFalse();
        vm.IsOptionalFeatureFallback.Should().BeFalse();
    }

    [Fact]
    public void AllFallbacks_AreFalse_WhenIconResolved()
    {
        var def = new ItemDefinition
        {
            Id = "app1",
            Name = "App 1",
            Description = "",
            AppxPackageName = new[] { "Microsoft.App1" },
            IconPath = @"C:\Users\test\AppData\Local\Winhance\IconCache\Microsoft.App1.png",
        };
        var vm = CreateViewModel(def);

        vm.IsAppXFallback.Should().BeFalse();
        vm.IsCapabilityFallback.Should().BeFalse();
        vm.IsOptionalFeatureFallback.Should().BeFalse();
    }

    // -------------------------------------------------------
    // HasDescription / HasWebsiteUrl / HasInstabilityWarning / ShowNonReinstallableChip
    // -------------------------------------------------------

    [Fact]
    public void HasDescription_IsTrue_WhenDescriptionPresent()
    {
        var def = new ItemDefinition { Id = "a", Name = "A", Description = "An app" };
        var vm = CreateViewModel(def);
        vm.HasDescription.Should().BeTrue();
    }

    [Fact]
    public void HasDescription_IsFalse_WhenDescriptionNull()
    {
        var def = new ItemDefinition { Id = "a", Name = "A", Description = null! };
        var vm = CreateViewModel(def);
        vm.HasDescription.Should().BeFalse();
    }

    [Fact]
    public void HasDescription_IsFalse_WhenDescriptionEmpty()
    {
        var def = new ItemDefinition { Id = "a", Name = "A", Description = "" };
        var vm = CreateViewModel(def);
        vm.HasDescription.Should().BeFalse();
    }

    [Fact]
    public void WebsiteUrl_IsExposed_WhenWebsiteSet()
    {
        var def = new ItemDefinition { Id = "a", Name = "A", Description = "", WebsiteUrl = "https://example.com" };
        var vm = CreateViewModel(def);
        vm.WebsiteUrl.Should().Be("https://example.com");
    }

    [Fact]
    public void WebsiteUrl_IsNullOrEmpty_WhenWebsiteNull()
    {
        var def = new ItemDefinition { Id = "a", Name = "A", Description = "" };
        var vm = CreateViewModel(def);
        vm.WebsiteUrl.Should().BeNullOrEmpty();
    }

    [Fact]
    public void HasInstabilityWarning_IsTrue_WhenFlagSet()
    {
        var def = new ItemDefinition { Id = "a", Name = "A", Description = "", HasInstabilityWarning = true };
        var vm = CreateViewModel(def);
        vm.HasInstabilityWarning.Should().BeTrue();
    }

    [Fact]
    public void HasInstabilityWarning_IsFalse_WhenUnset()
    {
        var def = new ItemDefinition { Id = "a", Name = "A", Description = "" };
        var vm = CreateViewModel(def);
        vm.HasInstabilityWarning.Should().BeFalse();
    }

    [Fact]
    public void InstabilityWarningLabel_PullsLocalizedCardPillWarningKey()
    {
        // Localization mock in this fixture returns the key as the value, so a
        // matching key here just confirms the property reaches that key.
        var vm = CreateViewModel();
        vm.InstabilityWarningLabel.Should().Be("Card_Pill_Warning");
        _mockLocalization.Verify(l => l.GetString("Card_Pill_Warning"), Times.AtLeastOnce);
    }

    [Fact]
    public void InstabilityWarningTooltip_PullsLocalizedTooltipKey()
    {
        var vm = CreateViewModel();
        vm.InstabilityWarningTooltip.Should().Be("Card_Pill_InstabilityWarning_Tooltip");
        _mockLocalization.Verify(l => l.GetString("Card_Pill_InstabilityWarning_Tooltip"), Times.AtLeastOnce);
    }

    [Fact]
    public void InstabilityWarningProperties_FireOnLanguageChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalization.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(vm.InstabilityWarningLabel));
        changedProperties.Should().Contain(nameof(vm.InstabilityWarningTooltip));
    }

    [Fact]
    public void ShowNonReinstallableChip_IsTrue_WhenCannotBeReinstalled()
    {
        var def = new ItemDefinition { Id = "a", Name = "A", Description = "", CanBeReinstalled = false };
        var vm = CreateViewModel(def);
        vm.ShowNonReinstallableChip.Should().BeTrue();
    }

    [Fact]
    public void ShowNonReinstallableChip_IsFalse_WhenCanBeReinstalled()
    {
        var def = new ItemDefinition { Id = "a", Name = "A", Description = "", CanBeReinstalled = true };
        var vm = CreateViewModel(def);
        vm.ShowNonReinstallableChip.Should().BeFalse();
    }

    // -------------------------------------------------------
    // HasIcon
    // -------------------------------------------------------

    [Fact]
    public void HasIcon_NullIconPath_IsFalse()
    {
        var def = new ItemDefinition
        {
            Id = "app1",
            Name = "App 1",
            Description = "",
            AppxPackageName = new[] { "Microsoft.App1" },
            IconPath = null,
        };
        var vm = CreateViewModel(def);

        vm.HasIcon.Should().BeFalse();
    }

    [Fact]
    public void HasIcon_PopulatedIconPath_IsTrue()
    {
        var def = new ItemDefinition
        {
            Id = "app1",
            Name = "App 1",
            Description = "",
            AppxPackageName = new[] { "Microsoft.App1" },
            IconPath = @"C:\Users\test\AppData\Local\Winhance\IconCache\Microsoft.App1_1.0.0.png",
        };
        var vm = CreateViewModel(def);

        vm.HasIcon.Should().BeTrue();
    }

    // -------------------------------------------------------
    // Theme-aware icon path resolution
    // -------------------------------------------------------

    [Fact]
    public void IconSource_LightTheme_WithLightSibling_DecodesFromLightPath()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "WinhanceTest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var primaryPath = Path.Combine(tmpDir, "icon.f7a3.png");
            var lightPath = Path.Combine(tmpDir, "icon.f7a3.light.png");
            File.WriteAllBytes(primaryPath, MinimalPng());
            File.WriteAllBytes(lightPath, MinimalPng());

            var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = primaryPath };
            _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Light);

            var vm = CreateViewModel(def);
            var bmp = vm.IconSource;

            bmp.Should().NotBeNull();
            bmp!.UriSource.LocalPath.Should().Be(lightPath);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void IconSource_LightTheme_NoLightSibling_FallsBackToPrimary()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "WinhanceTest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var primaryPath = Path.Combine(tmpDir, "icon.b8e1.png");
            File.WriteAllBytes(primaryPath, MinimalPng());

            var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = primaryPath };
            _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Light);

            var vm = CreateViewModel(def);
            vm.IconSource!.UriSource.LocalPath.Should().Be(primaryPath);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void IconSource_DarkTheme_NoDarkSibling_UsesPrimary()
    {
        // Mono-light source (white): no .dark.png written; primary is the
        // correct dark-mode rendering.
        var tmpDir = Path.Combine(Path.GetTempPath(), "WinhanceTest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var primaryPath = Path.Combine(tmpDir, "icon.f7a3.png");
            var lightPath = Path.Combine(tmpDir, "icon.f7a3.light.png");
            File.WriteAllBytes(primaryPath, MinimalPng());
            File.WriteAllBytes(lightPath, MinimalPng());

            var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = primaryPath };
            _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Dark);

            var vm = CreateViewModel(def);
            vm.IconSource!.UriSource.LocalPath.Should().Be(primaryPath);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void IconSource_DarkTheme_WithDarkSibling_DecodesFromDarkPath()
    {
        // Mono-dark source (e.g. Xbox Game Bar #333): synthesizer wrote a
        // .dark.png; the VM must prefer it in dark mode.
        var tmpDir = Path.Combine(Path.GetTempPath(), "WinhanceTest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var primaryPath = Path.Combine(tmpDir, "icon.7c44.png");
            var lightPath = Path.Combine(tmpDir, "icon.7c44.light.png");
            var darkPath = Path.Combine(tmpDir, "icon.7c44.dark.png");
            File.WriteAllBytes(primaryPath, MinimalPng());
            File.WriteAllBytes(lightPath, MinimalPng());
            File.WriteAllBytes(darkPath, MinimalPng());

            var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = primaryPath };
            _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Dark);

            var vm = CreateViewModel(def);
            vm.IconSource!.UriSource.LocalPath.Should().Be(darkPath);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void ThemeChanged_RaisesPropertyChangedForIconSource()
    {
        var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = "C:\\fake\\icon.png" };
        _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Dark);

        var vm = CreateViewModel(def);

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _mockThemeService.Raise(t => t.ThemeChanged += null, this, WinhanceTheme.LightNative);

        raised.Should().Contain(nameof(AppItemViewModel.IconSource));
    }

    [Fact]
    public void Dispose_UnsubscribesFromThemeChanged()
    {
        var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = "C:\\fake\\icon.png" };
        _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Dark);

        var vm = CreateViewModel(def);
        vm.Dispose();

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _mockThemeService.Raise(t => t.ThemeChanged += null, this, WinhanceTheme.LightNative);

        raised.Should().NotContain(nameof(AppItemViewModel.IconSource));
    }

    /// <summary>
    /// PNG-ish junk bytes — sufficient for File.Exists checks and lazy
    /// BitmapImage URI assignment. The tests in this file only inspect
    /// `BitmapImage.UriSource.LocalPath`, so the decoder never runs over
    /// these bytes and their malformed CRC/payload doesn't matter.
    /// </summary>
    private static byte[] MinimalPng() => new byte[]
    {
        0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,
        0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
        0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,
        0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
        0x89,0x00,0x00,0x00,0x0D,0x49,0x44,0x41,
        0x54,0x78,0x9C,0x62,0x00,0x01,0x00,0x00,
        0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,
        0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
        0x42,0x60,0x82
    };
}
