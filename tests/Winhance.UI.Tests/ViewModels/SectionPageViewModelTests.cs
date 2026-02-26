using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

/// <summary>
/// Concrete ISectionInfo implementation for testing.
/// </summary>
public class TestSectionInfo : ISectionInfo
{
    public string Key { get; }
    public string IconGlyphKey { get; }
    public string DisplayName { get; }
    public string ModuleId { get; }

    public TestSectionInfo(string key, string iconGlyphKey, string displayName, string moduleId)
    {
        Key = key;
        IconGlyphKey = iconGlyphKey;
        DisplayName = displayName;
        ModuleId = moduleId;
    }
}

/// <summary>
/// Concrete subclass of SectionPageViewModel for testing.
/// </summary>
public class TestableSectionPageViewModel : SectionPageViewModel<TestSectionInfo>
{
    public static readonly IReadOnlyList<TestSectionInfo> TestSections = new List<TestSectionInfo>
    {
        new("SectionA", "IconA", "Section A", "ModuleA"),
        new("SectionB", "IconB", "Section B", "ModuleB"),
        new("SectionC", "IconC", "Section C", "ModuleC"),
    };

    protected override string PageTitleKey => "Test_Page_Title";
    protected override string PageDescriptionKey => "Test_Page_Description";
    protected override string BreadcrumbRootFallback => "TestPage";
    protected override string LogPrefix => "TestPageViewModel";
    protected override IReadOnlyList<TestSectionInfo> SectionDefinitions => TestSections;

    public TestableSectionPageViewModel(
        ILogService logService,
        ILocalizationService localizationService,
        IEnumerable<ISettingsFeatureViewModel> featureViewModels)
        : base(logService, localizationService, featureViewModels)
    {
        InitializeSectionMappings();
    }
}

public class SectionPageViewModelTests : IDisposable
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<ILocalizationService> _mockLocalizationService;
    private readonly List<Mock<ISettingsFeatureViewModel>> _mockFeatureVms;

    public SectionPageViewModelTests()
    {
        _mockLogService = new Mock<ILogService>();
        _mockLocalizationService = new Mock<ILocalizationService>();

        // Default localization: return the key itself
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockFeatureVms = new List<Mock<ISettingsFeatureViewModel>>();
        foreach (var section in TestableSectionPageViewModel.TestSections)
        {
            var mock = new Mock<ISettingsFeatureViewModel>();
            mock.Setup(vm => vm.ModuleId).Returns(section.ModuleId);
            mock.Setup(vm => vm.DisplayName).Returns(section.DisplayName);
            mock.Setup(vm => vm.SettingsCount).Returns(0);
            mock.Setup(vm => vm.Settings).Returns(new ObservableCollection<SettingItemViewModel>());
            mock.Setup(vm => vm.HasVisibleSettings).Returns(false);
            _mockFeatureVms.Add(mock);
        }
    }

    public void Dispose()
    {
        // Intentionally empty.
    }

    private TestableSectionPageViewModel CreateViewModel()
    {
        return new TestableSectionPageViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockFeatureVms.Select(m => m.Object));
    }

    private TestableSectionPageViewModel CreateViewModel(
        IEnumerable<ISettingsFeatureViewModel> featureViewModels)
    {
        return new TestableSectionPageViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            featureViewModels);
    }

    // ── Constructor Tests ──

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        vm.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Act
        var action = () => CreateViewModel();

        // Assert
        action.Should().NotThrow();
    }

    // ── Default Property Tests ──

    [Fact]
    public void CurrentSectionKey_DefaultsToOverview()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        vm.CurrentSectionKey.Should().Be("Overview");
    }

    [Fact]
    public void IsLoading_DefaultsToTrue()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        vm.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void IsNotLoading_WhenIsLoadingIsTrue_ReturnsFalse()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        vm.IsNotLoading.Should().BeFalse();
    }

    [Fact]
    public void SearchText_DefaultsToEmptyString()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void IsInDetailPage_WhenOverview_ReturnsFalse()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        vm.IsInDetailPage.Should().BeFalse();
    }

    // ── PageTitle / PageDescription / Breadcrumb Localization ──

    [Fact]
    public void PageTitle_ReturnsLocalizedString()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Test_Page_Title"))
            .Returns("My Test Page");

        var vm = CreateViewModel();

        // Act & Assert
        vm.PageTitle.Should().Be("My Test Page");
    }

    [Fact]
    public void PageDescription_ReturnsLocalizedString()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Test_Page_Description"))
            .Returns("This is a test page");

        var vm = CreateViewModel();

        // Act & Assert
        vm.PageDescription.Should().Be("This is a test page");
    }

    [Fact]
    public void BreadcrumbRootText_ReturnsLocalizedTitle()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Test_Page_Title"))
            .Returns("My Page");

        var vm = CreateViewModel();

        // Act & Assert
        vm.BreadcrumbRootText.Should().Be("My Page");
    }

    [Fact]
    public void BreadcrumbRootText_WhenLocalizationReturnsNull_ReturnsFallback()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Test_Page_Title"))
            .Returns((string)null!);

        var vm = CreateViewModel();

        // Act & Assert
        vm.BreadcrumbRootText.Should().Be("TestPage");
    }

    [Fact]
    public void SearchPlaceholder_ReturnsLocalizedString()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Common_Search_Placeholder"))
            .Returns("Search settings...");

        var vm = CreateViewModel();

        // Act & Assert
        vm.SearchPlaceholder.Should().Be("Search settings...");
    }

    [Fact]
    public void SearchPlaceholder_WhenLocalizationReturnsNull_ReturnsFallback()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Common_Search_Placeholder"))
            .Returns((string)null!);

        var vm = CreateViewModel();

        // Act & Assert
        vm.SearchPlaceholder.Should().Be("Type here to search...");
    }

    // ── InitializeAsync ──

    [Fact]
    public async Task InitializeAsync_LoadsAllFeatureViewModels()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        foreach (var mockVm in _mockFeatureVms)
        {
            mockVm.Verify(v => v.LoadSettingsAsync(), Times.Once);
        }
    }

    [Fact]
    public async Task InitializeAsync_SetsIsLoadingFalseAfterCompletion()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.IsLoading.Should().BeFalse();
        vm.IsNotLoading.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_OnSecondCall_DoesNotReload()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();
        await vm.InitializeAsync();

        // Assert - LoadSettingsAsync should only be called once per VM
        foreach (var mockVm in _mockFeatureVms)
        {
            mockVm.Verify(v => v.LoadSettingsAsync(), Times.Once);
        }
    }

    [Fact]
    public async Task InitializeAsync_WhenFeatureVMThrows_ContinuesLoadingOthers()
    {
        // Arrange
        _mockFeatureVms[0]
            .Setup(v => v.LoadSettingsAsync())
            .ThrowsAsync(new InvalidOperationException("VM failed"));

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert - other VMs should still be loaded
        _mockFeatureVms[1].Verify(v => v.LoadSettingsAsync(), Times.Once);
        _mockFeatureVms[2].Verify(v => v.LoadSettingsAsync(), Times.Once);
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_SetsIsLoadingTrueDuringLoad()
    {
        // Arrange
        var vm = CreateViewModel();
        var wasLoadingDuringInit = false;

        _mockFeatureVms[0]
            .Setup(v => v.LoadSettingsAsync())
            .Returns(() =>
            {
                wasLoadingDuringInit = vm.IsLoading;
                return Task.CompletedTask;
            });

        // Act
        await vm.InitializeAsync();

        // Assert
        wasLoadingDuringInit.Should().BeTrue();
    }

    // ── OnNavigatedFrom ──

    [Fact]
    public void OnNavigatedFrom_ClearsSearchText()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SearchText = "test search";

        // Act
        vm.OnNavigatedFrom();

        // Assert
        vm.SearchText.Should().BeEmpty();
    }

    // ── Navigation / CurrentSectionKey ──

    [Fact]
    public void CurrentSectionKey_WhenSetToSection_UpdatesIsInDetailPage()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.CurrentSectionKey = "SectionA";

        // Assert
        vm.IsInDetailPage.Should().BeTrue();
    }

    [Fact]
    public void CurrentSectionKey_WhenSetBackToOverview_IsInDetailPageIsFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CurrentSectionKey = "SectionA";

        // Act
        vm.CurrentSectionKey = "Overview";

        // Assert
        vm.IsInDetailPage.Should().BeFalse();
    }

    [Fact]
    public void CurrentSectionKey_WhenChanged_RaisesPropertyChangedForIsInDetailPage()
    {
        // Arrange
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        vm.CurrentSectionKey = "SectionA";

        // Assert
        raisedProperties.Should().Contain(nameof(vm.IsInDetailPage));
    }

    [Fact]
    public void CurrentSectionKey_WhenChanged_RaisesPropertyChangedForCurrentSectionName()
    {
        // Arrange
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        vm.CurrentSectionKey = "SectionB";

        // Assert
        raisedProperties.Should().Contain(nameof(vm.CurrentSectionName));
    }

    [Fact]
    public void CurrentSectionKey_WhenChangedWithActiveSearch_ClearsSearchText()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CurrentSectionKey = "SectionA";
        vm.SearchText = "some search";

        // Act
        vm.CurrentSectionKey = "SectionB";

        // Assert
        vm.SearchText.Should().BeEmpty();
    }

    // ── CurrentSectionName ──

    [Fact]
    public void CurrentSectionName_WhenOverview_ReturnsOverview()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.CurrentSectionName.Should().Be("Overview");
    }

    [Fact]
    public void CurrentSectionName_WhenInSection_ReturnsDisplayNameFromViewModel()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.CurrentSectionKey = "SectionA";

        // Assert - DisplayName comes from the mock feature VM for ModuleA
        vm.CurrentSectionName.Should().Be("Section A");
    }

    // ── GetSectionViewModel ──

    [Fact]
    public void GetSectionViewModel_WithValidKey_ReturnsCorrectViewModel()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        var result = vm.GetSectionViewModel("SectionA");

        // Assert
        result.Should().NotBeNull();
        result!.ModuleId.Should().Be("ModuleA");
    }

    [Fact]
    public void GetSectionViewModel_WithInvalidKey_ReturnsNull()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        var result = vm.GetSectionViewModel("NonExistentSection");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSectionViewModel_WithOverviewKey_ReturnsNull()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        var result = vm.GetSectionViewModel("Overview");

        // Assert
        result.Should().BeNull();
    }

    // ── GetSectionDisplayName ──

    [Fact]
    public void GetSectionDisplayName_WithValidKey_ReturnsDisplayName()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        var name = vm.GetSectionDisplayName("SectionB");

        // Assert
        name.Should().Be("Section B");
    }

    [Fact]
    public void GetSectionDisplayName_WithInvalidKey_ReturnsOverview()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        var name = vm.GetSectionDisplayName("UnknownKey");

        // Assert
        name.Should().Be("Overview");
    }

    // ── SearchText Filtering ──

    [Fact]
    public void SearchText_WhenSetInDetailPage_AppliesFilterToCurrentSectionVM()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CurrentSectionKey = "SectionA";

        // Act
        vm.SearchText = "test query";

        // Assert
        _mockFeatureVms[0].Verify(v => v.ApplySearchFilter("test query"), Times.AtLeastOnce);
    }

    [Fact]
    public void SearchText_WhenSetInOverview_AppliesFilterToAllFeatureVMs()
    {
        // Arrange
        var vm = CreateViewModel();
        // CurrentSectionKey defaults to "Overview", which has no mapped VM

        // Act
        vm.SearchText = "global search";

        // Assert
        foreach (var mockVm in _mockFeatureVms)
        {
            mockVm.Verify(v => v.ApplySearchFilter("global search"), Times.Once);
        }
    }

    [Fact]
    public void SearchText_WhenCleared_AppliesEmptyFilterToVMs()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SearchText = "something";

        // Act
        vm.SearchText = string.Empty;

        // Assert
        foreach (var mockVm in _mockFeatureVms)
        {
            mockVm.Verify(v => v.ApplySearchFilter(string.Empty), Times.AtLeastOnce);
        }
    }

    // ── HasNoSearchResults ──

    [Fact]
    public void HasNoSearchResults_WhenNoSearchText_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.HasNoSearchResults.Should().BeFalse();
    }

    [Fact]
    public void HasNoSearchResults_WhenSearchTextSetAndNoVisibleSettings_ReturnsTrue()
    {
        // Arrange
        var vm = CreateViewModel();
        // All mock VMs have HasVisibleSettings = false by default

        // Act
        vm.SearchText = "xyz no matches";

        // Assert
        vm.HasNoSearchResults.Should().BeTrue();
    }

    [Fact]
    public void HasNoSearchResults_WhenSearchTextSetAndHasVisibleSettings_ReturnsFalse()
    {
        // Arrange
        _mockFeatureVms[1].Setup(v => v.HasVisibleSettings).Returns(true);
        var vm = CreateViewModel();

        // Act
        vm.SearchText = "something";

        // Assert
        vm.HasNoSearchResults.Should().BeFalse();
    }

    [Fact]
    public void HasNoSearchResults_InDetailPage_ChecksCurrentSectionOnly()
    {
        // Arrange
        _mockFeatureVms[0].Setup(v => v.HasVisibleSettings).Returns(false);
        _mockFeatureVms[1].Setup(v => v.HasVisibleSettings).Returns(true);
        var vm = CreateViewModel();
        vm.CurrentSectionKey = "SectionA";

        // Act
        vm.SearchText = "query";

        // Assert - SectionA has no visible settings, even though SectionB does
        vm.HasNoSearchResults.Should().BeTrue();
    }

    // ── Search Suggestions ──

    [Fact]
    public void SearchSuggestions_DefaultsToEmptyCollection()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        vm.SearchSuggestions.Should().NotBeNull();
        vm.SearchSuggestions.Should().BeEmpty();
    }

    // ── Language Changed ──

    [Fact]
    public void LanguageChanged_RaisesPropertyChangedForLocalizedProperties()
    {
        // Arrange
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        // Act - simulate language change
        _mockLocalizationService.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        // Assert
        raisedProperties.Should().Contain(nameof(vm.PageTitle));
        raisedProperties.Should().Contain(nameof(vm.PageDescription));
        raisedProperties.Should().Contain(nameof(vm.BreadcrumbRootText));
        raisedProperties.Should().Contain(nameof(vm.SearchPlaceholder));
    }

    // ── Dispose ──

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        var action = () => vm.Dispose();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        var action = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        // Arrange
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        vm.Dispose();
        raisedProperties.Clear();

        // Raise after dispose - should not fire property changed
        _mockLocalizationService.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        // Assert - no property changes should be raised after dispose
        raisedProperties.Should().NotContain(nameof(vm.PageTitle));
    }

    // ── IsLoading / IsNotLoading ──

    [Fact]
    public void IsNotLoading_WhenIsLoadingChanges_UpdatesAccordingly()
    {
        // Arrange
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        vm.IsLoading = false;

        // Assert
        vm.IsNotLoading.Should().BeTrue();
        raisedProperties.Should().Contain(nameof(vm.IsNotLoading));
    }

    // ── Section Navigation Clears Filter ──

    [Fact]
    public void CurrentSectionKey_WhenChangedWithNoSearchText_AppliesEmptyFilterToTargetSection()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.CurrentSectionKey = "SectionB";

        // Assert - should apply empty filter to the target section VM
        _mockFeatureVms[1].Verify(v => v.ApplySearchFilter(string.Empty), Times.AtLeastOnce);
    }

    // ── HasNoSearchResults PropertyChanged ──

    [Fact]
    public void SearchText_WhenChanged_RaisesPropertyChangedForHasNoSearchResults()
    {
        // Arrange
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        vm.SearchText = "test";

        // Assert
        raisedProperties.Should().Contain(nameof(vm.HasNoSearchResults));
    }
}
