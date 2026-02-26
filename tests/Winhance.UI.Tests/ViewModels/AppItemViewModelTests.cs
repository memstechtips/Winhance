using FluentAssertions;
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
            _mockDispatcher.Object);
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
            AppxPackageName = "SomePackage",
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
            AppxPackageName = "Pkg",
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
}
