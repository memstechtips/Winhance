using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class AdvancedToolsViewModelTests
{
    private readonly Mock<ILocalizationService> _mockLocalization = new();

    public AdvancedToolsViewModelTests()
    {
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
    }

    private AdvancedToolsViewModel CreateViewModel()
    {
        return new AdvancedToolsViewModel(_mockLocalization.Object);
    }

    // -------------------------------------------------------
    // Constructor / Initialization
    // -------------------------------------------------------

    [Fact]
    public void Constructor_DefaultsToOverview()
    {
        var vm = CreateViewModel();

        vm.CurrentSectionKey.Should().Be("Overview");
    }

    [Fact]
    public void Constructor_SubscribesToLanguageChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalization.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(vm.PageTitle));
        changedProperties.Should().Contain(nameof(vm.PageDescription));
        changedProperties.Should().Contain(nameof(vm.BreadcrumbRootText));
        changedProperties.Should().Contain(nameof(vm.WimUtilDisplayName));
        changedProperties.Should().Contain(nameof(vm.WimUtilDescription));
        changedProperties.Should().Contain(nameof(vm.AutounattendXmlDisplayName));
    }

    // -------------------------------------------------------
    // Localized string properties
    // -------------------------------------------------------

    [Fact]
    public void PageTitle_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Nav_AdvancedTools"))
            .Returns("Advanced Tools");

        var vm = CreateViewModel();

        vm.PageTitle.Should().Be("Advanced Tools");
    }

    [Fact]
    public void PageDescription_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Category_AdvancedTools_StatusText"))
            .Returns("Power user tools");

        var vm = CreateViewModel();

        vm.PageDescription.Should().Be("Power user tools");
    }

    [Fact]
    public void BreadcrumbRootText_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Nav_AdvancedTools"))
            .Returns("Tools");

        var vm = CreateViewModel();

        vm.BreadcrumbRootText.Should().Be("Tools");
    }

    [Fact]
    public void BreadcrumbRootText_WhenNull_ReturnsFallback()
    {
        _mockLocalization
            .Setup(l => l.GetString("Nav_AdvancedTools"))
            .Returns((string?)null!);

        var vm = CreateViewModel();

        vm.BreadcrumbRootText.Should().Be("Advanced Tools");
    }

    [Fact]
    public void WimUtilDisplayName_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("WIMUtil_Title"))
            .Returns("WIM Utility");

        var vm = CreateViewModel();

        vm.WimUtilDisplayName.Should().Be("WIM Utility");
    }

    [Fact]
    public void WimUtilDisplayName_WhenNull_ReturnsFallback()
    {
        _mockLocalization
            .Setup(l => l.GetString("WIMUtil_Title"))
            .Returns((string?)null!);

        var vm = CreateViewModel();

        vm.WimUtilDisplayName.Should().Be("WIMUtil");
    }

    [Fact]
    public void WimUtilDescription_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("WIMUtil_Subtitle"))
            .Returns("Create custom images");

        var vm = CreateViewModel();

        vm.WimUtilDescription.Should().Be("Create custom images");
    }

    [Fact]
    public void WimUtilDescription_WhenNull_ReturnsFallback()
    {
        _mockLocalization
            .Setup(l => l.GetString("WIMUtil_Subtitle"))
            .Returns((string?)null!);

        var vm = CreateViewModel();

        vm.WimUtilDescription.Should().Be("Create Custom Windows Installation Media");
    }

    [Fact]
    public void AutounattendXmlDisplayName_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("AdvancedTools_MenuItem_CreateXML"))
            .Returns("XML Builder");

        var vm = CreateViewModel();

        vm.AutounattendXmlDisplayName.Should().Be("XML Builder");
    }

    [Fact]
    public void AutounattendXmlDisplayName_WhenNull_ReturnsFallback()
    {
        _mockLocalization
            .Setup(l => l.GetString("AdvancedTools_MenuItem_CreateXML"))
            .Returns((string?)null!);

        var vm = CreateViewModel();

        vm.AutounattendXmlDisplayName.Should().Be("Create Autounattend XML");
    }

    [Fact]
    public void AutounattendXmlDescription_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("AdvancedTools_GenerateCard_Description"))
            .Returns("Generate XML");

        var vm = CreateViewModel();

        vm.AutounattendXmlDescription.Should().Be("Generate XML");
    }

    // -------------------------------------------------------
    // IsInDetailPage
    // -------------------------------------------------------

    [Fact]
    public void IsInDetailPage_WhenOverview_ReturnsFalse()
    {
        var vm = CreateViewModel();

        vm.IsInDetailPage.Should().BeFalse();
    }

    [Fact]
    public void IsInDetailPage_WhenNotOverview_ReturnsTrue()
    {
        var vm = CreateViewModel();

        vm.CurrentSectionKey = "WimUtil";

        vm.IsInDetailPage.Should().BeTrue();
    }

    // -------------------------------------------------------
    // CurrentSectionKey changes
    // -------------------------------------------------------

    [Fact]
    public void CurrentSectionKey_Changed_RaisesIsInDetailPageAndCurrentSectionName()
    {
        var vm = CreateViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        vm.CurrentSectionKey = "WimUtil";

        changedProperties.Should().Contain(nameof(vm.CurrentSectionKey));
        changedProperties.Should().Contain(nameof(vm.IsInDetailPage));
        changedProperties.Should().Contain(nameof(vm.CurrentSectionName));
    }

    // -------------------------------------------------------
    // GetSectionDisplayName
    // -------------------------------------------------------

    [Fact]
    public void GetSectionDisplayName_WimUtil_ReturnsLocalized()
    {
        _mockLocalization
            .Setup(l => l.GetString("WIMUtil_Title"))
            .Returns("WIM Utility");

        var vm = CreateViewModel();

        vm.GetSectionDisplayName("WimUtil").Should().Be("WIM Utility");
    }

    [Fact]
    public void GetSectionDisplayName_AutounattendXml_ReturnsLocalized()
    {
        _mockLocalization
            .Setup(l => l.GetString("AdvancedTools_MenuItem_CreateXML"))
            .Returns("XML Maker");

        var vm = CreateViewModel();

        vm.GetSectionDisplayName("AutounattendXml").Should().Be("XML Maker");
    }

    [Fact]
    public void GetSectionDisplayName_UnknownKey_ReturnsOverview()
    {
        var vm = CreateViewModel();

        vm.GetSectionDisplayName("SomeUnknownSection").Should().Be("Overview");
    }

    [Fact]
    public void GetSectionDisplayName_WimUtil_WhenNull_ReturnsFallback()
    {
        _mockLocalization
            .Setup(l => l.GetString("WIMUtil_Title"))
            .Returns((string?)null!);

        var vm = CreateViewModel();

        vm.GetSectionDisplayName("WimUtil").Should().Be("WIMUtil");
    }

    // -------------------------------------------------------
    // CurrentSectionName
    // -------------------------------------------------------

    [Fact]
    public void CurrentSectionName_ReflectsCurrentSectionKey()
    {
        _mockLocalization
            .Setup(l => l.GetString("WIMUtil_Title"))
            .Returns("WIM Utility");

        var vm = CreateViewModel();
        vm.CurrentSectionKey = "WimUtil";

        vm.CurrentSectionName.Should().Be("WIM Utility");
    }

    [Fact]
    public void CurrentSectionName_WhenOverview_ReturnsOverview()
    {
        var vm = CreateViewModel();

        vm.CurrentSectionName.Should().Be("Overview");
    }

    // -------------------------------------------------------
    // Static Sections list
    // -------------------------------------------------------

    [Fact]
    public void Sections_ContainsExpectedEntries()
    {
        AdvancedToolsViewModel.Sections.Should().HaveCount(2);

        AdvancedToolsViewModel.Sections[0].Key.Should().Be("WimUtil");
        AdvancedToolsViewModel.Sections[0].IconResourceKey.Should().Be("WimUtilIconPath");
        AdvancedToolsViewModel.Sections[0].DisplayName.Should().Be("WIMUtil");

        AdvancedToolsViewModel.Sections[1].Key.Should().Be("AutounattendXml");
        AdvancedToolsViewModel.Sections[1].IconResourceKey.Should().Be("AutounattendXmlIconPath");
        AdvancedToolsViewModel.Sections[1].DisplayName.Should().Be("Create Autounattend XML");
    }

    // -------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        var vm = CreateViewModel();
        vm.Dispose();

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
}
