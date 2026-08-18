using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

public partial class AdvancedToolsViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly ILocalizationService _localizationService;

    [ObservableProperty]
    public partial string CurrentSectionKey { get; set; }

    public string PageTitle => _localizationService.GetString("Nav_AdvancedTools");

    public string PageDescription => _localizationService.GetString("Category_AdvancedTools_StatusText");

    public string BreadcrumbRootText => _localizationService.GetStringOrDefault("Nav_AdvancedTools", "Advanced Tools");

    public bool IsInDetailPage => CurrentSectionKey != "Overview";

    public string WimUtilDisplayName => _localizationService.GetStringOrDefault("WIMUtil_Title", "WIMUtil");

    public string WimUtilDescription => _localizationService.GetStringOrDefault("WIMUtil_Subtitle", "Create Custom Windows Installation Media");

    public string AutounattendXmlDisplayName => _localizationService.GetStringOrDefault("AdvancedTools_MenuItem_CreateXML", "Create Autounattend XML");

    public string AutounattendXmlDescription => _localizationService.GetStringOrDefault("AdvancedTools_GenerateCard_Description", "Generate an autounattend.xml file based on your current Winhance selections to customize Windows during installation.");

    public string CurrentSectionName => GetSectionDisplayName(CurrentSectionKey);

    public static readonly IReadOnlyList<AdvancedToolsSectionInfo> Sections = new List<AdvancedToolsSectionInfo>()
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
        GC.SuppressFinalize(this);
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

    public string GetSectionDisplayName(string sectionKey)
    {
        return sectionKey switch
        {
            "WimUtil" => _localizationService.GetStringOrDefault("WIMUtil_Title", "WIMUtil"),
            "AutounattendXml" => _localizationService.GetStringOrDefault("AdvancedTools_MenuItem_CreateXML", "Create Autounattend XML"),
            _ => "Overview"
        };
    }

    partial void OnCurrentSectionKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsInDetailPage));
        OnPropertyChanged(nameof(CurrentSectionName));
    }
}
