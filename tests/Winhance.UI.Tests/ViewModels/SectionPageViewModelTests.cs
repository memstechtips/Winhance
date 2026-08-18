using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

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
        IEnumerable<ISettingsFeatureViewModel> featureViewModels,
        IConfigReviewBadgeService badgeService,
        IConfigReviewModeService reviewModeService)
        : base(logService, localizationService, featureViewModels, badgeService, reviewModeService)
    {
        InitializeSectionMappings();
    }
}

public class SectionPageViewModelTests
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<ILocalizationService> _mockLocalizationService;
    private readonly Mock<IConfigReviewBadgeService> _mockBadgeService = new();
    private readonly Mock<IConfigReviewModeService> _mockReviewModeService = new();
    private readonly List<Mock<ISettingsFeatureViewModel>> _mockFeatureVms;

    public SectionPageViewModelTests()
    {
        _mockLogService = new Mock<ILogService>();
        _mockLocalizationService = new Mock<ILocalizationService>();

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockLocalizationService.MirrorTryGetString();

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

    private TestableSectionPageViewModel CreateViewModel()
    {
        return new TestableSectionPageViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockFeatureVms.Select(m => m.Object),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        var vm = CreateViewModel();

        vm.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        var action = () => CreateViewModel();

        action.Should().NotThrow();
    }

    [Fact]
    public void CurrentSectionKey_DefaultsToOverview()
    {
        var vm = CreateViewModel();

        vm.CurrentSectionKey.Should().Be("Overview");
    }

    [Fact]
    public void IsLoading_DefaultsToTrue()
    {
        var vm = CreateViewModel();

        vm.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void IsNotLoading_WhenIsLoadingIsTrue_ReturnsFalse()
    {
        var vm = CreateViewModel();

        vm.IsNotLoading.Should().BeFalse();
    }

    [Fact]
    public void SearchText_DefaultsToEmptyString()
    {
        var vm = CreateViewModel();

        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void IsInDetailPage_WhenOverview_ReturnsFalse()
    {
        var vm = CreateViewModel();

        vm.IsInDetailPage.Should().BeFalse();
    }

    [Fact]
    public void PageTitle_ReturnsLocalizedString()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Test_Page_Title"))
            .Returns("My Test Page");

        var vm = CreateViewModel();

        vm.PageTitle.Should().Be("My Test Page");
    }

    [Fact]
    public void PageDescription_ReturnsLocalizedString()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Test_Page_Description"))
            .Returns("This is a test page");

        var vm = CreateViewModel();

        vm.PageDescription.Should().Be("This is a test page");
    }

    [Fact]
    public void BreadcrumbRootText_ReturnsLocalizedTitle()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Test_Page_Title"))
            .Returns("My Page");

        var vm = CreateViewModel();

        vm.BreadcrumbRootText.Should().Be("My Page");
    }

    [Fact]
    public void BreadcrumbRootText_WhenLocalizationReturnsNull_ReturnsFallback()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Test_Page_Title"))
            .Returns((string)null!);

        var vm = CreateViewModel();

        vm.BreadcrumbRootText.Should().Be("TestPage");
    }

    [Fact]
    public void SearchPlaceholder_ReturnsLocalizedString()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Common_Search_Placeholder"))
            .Returns("Search settings...");

        var vm = CreateViewModel();

        vm.SearchPlaceholder.Should().Be("Search settings...");
    }

    [Fact]
    public void SearchPlaceholder_WhenLocalizationReturnsNull_ReturnsFallback()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Common_Search_Placeholder"))
            .Returns((string)null!);

        var vm = CreateViewModel();

        vm.SearchPlaceholder.Should().Be("Type here to search...");
    }

    [Fact]
    public async Task InitializeAsync_LoadsAllFeatureViewModels()
    {
        var vm = CreateViewModel();

        await vm.InitializeAsync();

        foreach (var mockVm in _mockFeatureVms)
        {
            mockVm.Verify(v => v.LoadSettingsAsync(), Times.Once);
        }
    }

    [Fact]
    public async Task InitializeAsync_SetsIsLoadingFalseAfterCompletion()
    {
        var vm = CreateViewModel();

        await vm.InitializeAsync();

        vm.IsLoading.Should().BeFalse();
        vm.IsNotLoading.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_OnSecondCall_DoesNotReload()
    {
        var vm = CreateViewModel();

        await vm.InitializeAsync();
        await vm.InitializeAsync();

        foreach (var mockVm in _mockFeatureVms)
        {
            mockVm.Verify(v => v.LoadSettingsAsync(), Times.Once);
        }
    }

    [Fact]
    public async Task InitializeAsync_WhenFeatureVMThrows_ContinuesLoadingOthers()
    {
        _mockFeatureVms[0]
            .Setup(v => v.LoadSettingsAsync())
            .ThrowsAsync(new InvalidOperationException("VM failed"));

        var vm = CreateViewModel();

        await vm.InitializeAsync();

        _mockFeatureVms[1].Verify(v => v.LoadSettingsAsync(), Times.Once);
        _mockFeatureVms[2].Verify(v => v.LoadSettingsAsync(), Times.Once);
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_SetsIsLoadingTrueDuringLoad()
    {
        var vm = CreateViewModel();
        var wasLoadingDuringInit = false;

        _mockFeatureVms[0]
            .Setup(v => v.LoadSettingsAsync())
            .Returns(() =>
            {
                wasLoadingDuringInit = vm.IsLoading;
                return Task.CompletedTask;
            });

        await vm.InitializeAsync();

        wasLoadingDuringInit.Should().BeTrue();
    }

    [Fact]
    public void OnNavigatedFrom_ClearsSearchText()
    {
        var vm = CreateViewModel();
        vm.SearchText = "test search";

        vm.OnNavigatedFrom();

        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void CurrentSectionKey_WhenSetToSection_UpdatesIsInDetailPage()
    {
        var vm = CreateViewModel();

        vm.CurrentSectionKey = "SectionA";

        vm.IsInDetailPage.Should().BeTrue();
    }

    [Fact]
    public void CurrentSectionKey_WhenSetBackToOverview_IsInDetailPageIsFalse()
    {
        var vm = CreateViewModel();
        vm.CurrentSectionKey = "SectionA";

        vm.CurrentSectionKey = "Overview";

        vm.IsInDetailPage.Should().BeFalse();
    }

    [Fact]
    public void CurrentSectionKey_WhenChanged_RaisesPropertyChangedForIsInDetailPage()
    {
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        vm.CurrentSectionKey = "SectionA";

        raisedProperties.Should().Contain(nameof(vm.IsInDetailPage));
    }

    [Fact]
    public void CurrentSectionKey_WhenChanged_RaisesPropertyChangedForCurrentSectionName()
    {
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        vm.CurrentSectionKey = "SectionB";

        raisedProperties.Should().Contain(nameof(vm.CurrentSectionName));
    }

    [Fact]
    public void CurrentSectionKey_WhenChangedWithActiveSearch_ClearsSearchText()
    {
        var vm = CreateViewModel();
        vm.CurrentSectionKey = "SectionA";
        vm.SearchText = "some search";

        vm.CurrentSectionKey = "SectionB";

        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void CurrentSectionName_WhenOverview_ReturnsOverview()
    {
        var vm = CreateViewModel();

        vm.CurrentSectionName.Should().Be("Overview");
    }

    [Fact]
    public void CurrentSectionName_WhenInSection_ReturnsDisplayNameFromViewModel()
    {
        var vm = CreateViewModel();

        vm.CurrentSectionKey = "SectionA";

        vm.CurrentSectionName.Should().Be("Section A");
    }

    [Fact]
    public void GetSectionViewModel_WithValidKey_ReturnsCorrectViewModel()
    {
        var vm = CreateViewModel();

        var result = vm.GetSectionViewModel("SectionA");

        result.Should().NotBeNull();
        result!.ModuleId.Should().Be("ModuleA");
    }

    [Fact]
    public void GetSectionViewModel_WithInvalidKey_ReturnsNull()
    {
        var vm = CreateViewModel();

        var result = vm.GetSectionViewModel("NonExistentSection");

        result.Should().BeNull();
    }

    [Fact]
    public void GetSectionViewModel_WithOverviewKey_ReturnsNull()
    {
        var vm = CreateViewModel();

        var result = vm.GetSectionViewModel("Overview");

        result.Should().BeNull();
    }

    [Fact]
    public void GetSectionDisplayName_WithValidKey_ReturnsDisplayName()
    {
        var vm = CreateViewModel();

        var name = vm.GetSectionDisplayName("SectionB");

        name.Should().Be("Section B");
    }

    [Fact]
    public void GetSectionDisplayName_WithInvalidKey_ReturnsOverview()
    {
        var vm = CreateViewModel();

        var name = vm.GetSectionDisplayName("UnknownKey");

        name.Should().Be("Overview");
    }

    [Fact]
    public void SearchText_WhenSetInDetailPage_AppliesFilterToCurrentSectionVM()
    {
        var vm = CreateViewModel();
        vm.CurrentSectionKey = "SectionA";

        vm.SearchText = "test query";

        _mockFeatureVms[0].Verify(v => v.ApplySearchFilter("test query"), Times.AtLeastOnce);
    }

    [Fact]
    public void SearchText_WhenSetInOverview_AppliesFilterToAllFeatureVMs()
    {
        var vm = CreateViewModel();

        vm.SearchText = "global search";

        foreach (var mockVm in _mockFeatureVms)
        {
            mockVm.Verify(v => v.ApplySearchFilter("global search"), Times.Once);
        }
    }

    [Fact]
    public void SearchText_WhenCleared_AppliesEmptyFilterToVMs()
    {
        var vm = CreateViewModel();
        vm.SearchText = "something";

        vm.SearchText = string.Empty;

        foreach (var mockVm in _mockFeatureVms)
        {
            mockVm.Verify(v => v.ApplySearchFilter(string.Empty), Times.AtLeastOnce);
        }
    }

    [Fact]
    public void HasNoSearchResults_WhenNoSearchText_ReturnsFalse()
    {
        var vm = CreateViewModel();

        vm.HasNoSearchResults.Should().BeFalse();
    }

    [Fact]
    public void HasNoSearchResults_WhenSearchTextSetAndNoVisibleSettings_ReturnsTrue()
    {
        var vm = CreateViewModel();

        vm.SearchText = "xyz no matches";

        vm.HasNoSearchResults.Should().BeTrue();
    }

    [Fact]
    public void HasNoSearchResults_WhenSearchTextSetAndHasVisibleSettings_ReturnsFalse()
    {
        _mockFeatureVms[1].Setup(v => v.HasVisibleSettings).Returns(true);
        var vm = CreateViewModel();

        vm.SearchText = "something";

        vm.HasNoSearchResults.Should().BeFalse();
    }

    [Fact]
    public void HasNoSearchResults_InDetailPage_ChecksCurrentSectionOnly()
    {
        _mockFeatureVms[0].Setup(v => v.HasVisibleSettings).Returns(false);
        _mockFeatureVms[1].Setup(v => v.HasVisibleSettings).Returns(true);
        var vm = CreateViewModel();
        vm.CurrentSectionKey = "SectionA";

        vm.SearchText = "query";

        vm.HasNoSearchResults.Should().BeTrue();
    }

    [Fact]
    public void SearchSuggestions_DefaultsToEmptyCollection()
    {
        var vm = CreateViewModel();

        vm.SearchSuggestions.Should().NotBeNull();
        vm.SearchSuggestions.Should().BeEmpty();
    }

    [Fact]
    public void LanguageChanged_RaisesPropertyChangedForLocalizedProperties()
    {
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        raisedProperties.Should().Contain(nameof(vm.PageTitle));
        raisedProperties.Should().Contain(nameof(vm.PageDescription));
        raisedProperties.Should().Contain(nameof(vm.BreadcrumbRootText));
        raisedProperties.Should().Contain(nameof(vm.SearchPlaceholder));
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var vm = CreateViewModel();

        var action = () => vm.Dispose();

        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var vm = CreateViewModel();

        var action = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        vm.Dispose();
        raisedProperties.Clear();

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        raisedProperties.Should().NotContain(nameof(vm.PageTitle));
    }

    [Fact]
    public void IsNotLoading_WhenIsLoadingChanges_UpdatesAccordingly()
    {
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        vm.IsLoading = false;

        vm.IsNotLoading.Should().BeTrue();
        raisedProperties.Should().Contain(nameof(vm.IsNotLoading));
    }

    [Fact]
    public void CurrentSectionKey_WhenChangedWithNoSearchText_AppliesEmptyFilterToTargetSection()
    {
        var vm = CreateViewModel();

        vm.CurrentSectionKey = "SectionB";

        _mockFeatureVms[1].Verify(v => v.ApplySearchFilter(string.Empty), Times.AtLeastOnce);
    }

    [Fact]
    public void SearchText_WhenChanged_RaisesPropertyChangedForHasNoSearchResults()
    {
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        vm.SearchText = "test";

        raisedProperties.Should().Contain(nameof(vm.HasNoSearchResults));
    }
}
