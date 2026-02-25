using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools.Models;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

/// <summary>
/// ViewModel for the AdvancedTools page, coordinating sub-feature navigation.
/// </summary>
public partial class AdvancedToolsViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly ILocalizationService _localizationService;

    [ObservableProperty]
    public partial string CurrentSectionKey { get; set; }

    /// <summary>
    /// Gets the localized page title.
    /// </summary>
    public string PageTitle => _localizationService.GetString("Nav_AdvancedTools");

    /// <summary>
    /// Gets the localized page description.
    /// </summary>
    public string PageDescription => _localizationService.GetString("Category_AdvancedTools_StatusText");

    /// <summary>
    /// Gets the localized breadcrumb root text.
    /// </summary>
    public string BreadcrumbRootText => _localizationService.GetString("Nav_AdvancedTools") ?? "Advanced Tools";

    /// <summary>
    /// Gets whether navigation is currently on a detail page (not overview).
    /// </summary>
    public bool IsInDetailPage => CurrentSectionKey != "Overview";

    /// <summary>
    /// Gets the localized WIM Utility display name for overview card.
    /// </summary>
    public string WimUtilDisplayName => _localizationService.GetString("WIMUtil_Title") ?? "WIMUtil";

    /// <summary>
    /// Gets the localized WIM Utility description for overview card.
    /// </summary>
    public string WimUtilDescription => _localizationService.GetString("WIMUtil_Subtitle") ?? "Create Custom Windows Installation Media";

    /// <summary>
    /// Gets the localized Autounattend XML display name for overview card.
    /// </summary>
    public string AutounattendXmlDisplayName => _localizationService.GetString("AdvancedTools_MenuItem_CreateXML") ?? "Create Autounattend XML";

    /// <summary>
    /// Gets the localized Autounattend XML description for overview card.
    /// </summary>
    public string AutounattendXmlDescription => _localizationService.GetString("AdvancedTools_GenerateCard_Description") ?? "Generate an autounattend.xml file based on your current Winhance selections to customize Windows during installation.";

    /// <summary>
    /// Gets the display name of the current section.
    /// </summary>
    public string CurrentSectionName => GetSectionDisplayName(CurrentSectionKey);

    /// <summary>
    /// Section definitions for navigation.
    /// </summary>
    public static readonly List<AdvancedToolsSectionInfo> Sections = new()
    {
        new("WimUtil", "WimUtilIconPath", "WIMUtil"),
        new("AutounattendXml", "AutounattendXmlIconPath", "Create Autounattend XML"),
    };

    public AdvancedToolsViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        CurrentSectionKey = "Overview";
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(BreadcrumbRootText));
        OnPropertyChanged(nameof(WimUtilDisplayName));
        OnPropertyChanged(nameof(WimUtilDescription));
        OnPropertyChanged(nameof(AutounattendXmlDisplayName));
    }

    /// <summary>
    /// Gets the display name for the specified section key.
    /// </summary>
    public string GetSectionDisplayName(string sectionKey)
    {
        return sectionKey switch
        {
            "WimUtil" => _localizationService.GetString("WIMUtil_Title") ?? "WIMUtil",
            "AutounattendXml" => _localizationService.GetString("AdvancedTools_MenuItem_CreateXML") ?? "Create Autounattend XML",
            _ => "Overview"
        };
    }

    partial void OnCurrentSectionKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsInDetailPage));
        OnPropertyChanged(nameof(CurrentSectionName));
    }
}
