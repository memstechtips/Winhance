using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

public partial class SoftwareAppsViewModel : BaseViewModel
{
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IConfigReviewService _configReviewService;

    public SoftwareAppsViewModel(
        WindowsAppsViewModel windowsAppsViewModel,
        ExternalAppsViewModel externalAppsViewModel,
        ILocalizationService localizationService,
        ILogService logService,
        IUserPreferencesService userPreferencesService,
        IConfigReviewService configReviewService)
    {
        WindowsAppsViewModel = windowsAppsViewModel;
        ExternalAppsViewModel = externalAppsViewModel;
        _localizationService = localizationService;
        _logService = logService;
        _userPreferencesService = userPreferencesService;
        _configReviewService = configReviewService;

        // Load saved view mode preference (default: Card)
        var savedViewMode = _userPreferencesService.GetPreference("SoftwareAppsViewMode", "Card");
        _isCardViewMode = savedViewMode == "Card";

        WindowsAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
        ExternalAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
        WindowsAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;
        ExternalAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;
        _configReviewService.ReviewModeChanged += OnReviewModeChanged;

        UpdateButtonStates();
    }

    public WindowsAppsViewModel WindowsAppsViewModel { get; }
    public ExternalAppsViewModel ExternalAppsViewModel { get; }

    [ObservableProperty]
    private bool _isWindowsAppsTabSelected = true;

    [ObservableProperty]
    private bool _isExternalAppsTabSelected = false;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isCardViewMode = true;

    [ObservableProperty]
    private bool _isInReviewMode = false;

    [ObservableProperty]
    private int _windowsAppsSelectedCount = 0;

    [ObservableProperty]
    private int _externalAppsSelectedCount = 0;

    // Action choice properties for review mode
    [ObservableProperty]
    private bool _isWindowsAppsInstallAction = false;

    [ObservableProperty]
    private bool _isWindowsAppsRemoveAction = false;

    [ObservableProperty]
    private bool _isExternalAppsInstallAction = false;

    [ObservableProperty]
    private bool _isExternalAppsRemoveAction = false;

    [ObservableProperty]
    private bool _canInstallItems = false;

    [ObservableProperty]
    private bool _canRemoveItems = false;

    public bool IsWindowsAppsActionChosen => IsWindowsAppsInstallAction || IsWindowsAppsRemoveAction;
    public bool IsExternalAppsActionChosen => IsExternalAppsInstallAction || IsExternalAppsRemoveAction;

    public bool HasWindowsAppsInConfig => _configReviewService.IsFeatureInConfig(
        FeatureIds.WindowsApps);
    public bool HasExternalAppsInConfig => _configReviewService.IsFeatureInConfig(
        FeatureIds.ExternalApps);

    /// <summary>
    /// Whether all SoftwareApps sections have been reviewed.
    /// A section is reviewed when:
    /// - It's not in the config, OR
    /// - No items are selected (user chose to apply nothing), OR
    /// - An action button (Install/Remove) has been selected
    /// </summary>
    public bool IsSoftwareAppsReviewed
    {
        get
        {
            if (!IsInReviewMode) return false;
            bool windowsOk = !HasWindowsAppsInConfig || WindowsAppsSelectedCount == 0 || IsWindowsAppsActionChosen;
            bool externalOk = !HasExternalAppsInConfig || ExternalAppsSelectedCount == 0 || IsExternalAppsActionChosen;
            return windowsOk && externalOk;
        }
    }

    partial void OnIsWindowsAppsInstallActionChanged(bool value)
    {
        if (value && IsWindowsAppsRemoveAction) IsWindowsAppsRemoveAction = false;
        OnPropertyChanged(nameof(IsWindowsAppsActionChosen));
        OnPropertyChanged(nameof(IsSoftwareAppsReviewed));
        OnPropertyChanged(nameof(ReviewWindowsAppsBannerText));
        OnPropertyChanged(nameof(CurrentInstallAction));
        OnPropertyChanged(nameof(CurrentRemoveAction));
        SyncSoftwareAppsReviewedState();
    }

    partial void OnIsWindowsAppsRemoveActionChanged(bool value)
    {
        if (value && IsWindowsAppsInstallAction) IsWindowsAppsInstallAction = false;
        OnPropertyChanged(nameof(IsWindowsAppsActionChosen));
        OnPropertyChanged(nameof(IsSoftwareAppsReviewed));
        OnPropertyChanged(nameof(ReviewWindowsAppsBannerText));
        OnPropertyChanged(nameof(CurrentInstallAction));
        OnPropertyChanged(nameof(CurrentRemoveAction));
        SyncSoftwareAppsReviewedState();
    }

    partial void OnIsExternalAppsInstallActionChanged(bool value)
    {
        if (value && IsExternalAppsRemoveAction) IsExternalAppsRemoveAction = false;
        OnPropertyChanged(nameof(IsExternalAppsActionChosen));
        OnPropertyChanged(nameof(IsSoftwareAppsReviewed));
        OnPropertyChanged(nameof(ReviewExternalAppsBannerText));
        OnPropertyChanged(nameof(CurrentInstallAction));
        OnPropertyChanged(nameof(CurrentRemoveAction));
        SyncSoftwareAppsReviewedState();
    }

    partial void OnIsExternalAppsRemoveActionChanged(bool value)
    {
        if (value && IsExternalAppsInstallAction) IsExternalAppsInstallAction = false;
        OnPropertyChanged(nameof(IsExternalAppsActionChosen));
        OnPropertyChanged(nameof(IsSoftwareAppsReviewed));
        OnPropertyChanged(nameof(ReviewExternalAppsBannerText));
        OnPropertyChanged(nameof(CurrentInstallAction));
        OnPropertyChanged(nameof(CurrentRemoveAction));
        SyncSoftwareAppsReviewedState();
    }

    private void SyncSoftwareAppsReviewedState()
    {
        _configReviewService.IsSoftwareAppsReviewed = IsSoftwareAppsReviewed;
        _configReviewService.NotifyBadgeStateChanged();
    }

    /// <summary>
    /// Routes to the current tab's install action checkbox.
    /// </summary>
    public bool CurrentInstallAction
    {
        get => IsWindowsAppsTabSelected ? IsWindowsAppsInstallAction : IsExternalAppsInstallAction;
        set
        {
            if (IsWindowsAppsTabSelected)
                IsWindowsAppsInstallAction = value;
            else
                IsExternalAppsInstallAction = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Routes to the current tab's remove action checkbox.
    /// </summary>
    public bool CurrentRemoveAction
    {
        get => IsWindowsAppsTabSelected ? IsWindowsAppsRemoveAction : IsExternalAppsRemoveAction;
        set
        {
            if (IsWindowsAppsTabSelected)
                IsWindowsAppsRemoveAction = value;
            else
                IsExternalAppsRemoveAction = value;
            OnPropertyChanged();
        }
    }

    // Localized text properties
    public string PageTitle => _localizationService.GetString("Category_SoftwareApps_Title");
    public string PageDescription => _localizationService.GetString("Category_SoftwareApps_StatusText");
    public string SearchPlaceholder => _localizationService.GetString("Common_Search_Placeholder") ?? "Search apps...";
    public string WindowsAppsTabText => _localizationService.GetString("SoftwareApps_Tab_WindowsApps");
    public string ExternalAppsTabText => _localizationService.GetString("SoftwareApps_Tab_ExternalApps");
    public string InstallButtonText => _localizationService.GetString("SoftwareApps_Button_InstallSelected");
    public string RefreshButtonText => _localizationService.GetString("Button_Refresh");
    public string HelpButtonText => _localizationService.GetString("Button_Help");

    public string ViewModeTableTooltip => _localizationService.GetString("ViewMode_Table");
    public string ViewModeCardTooltip => _localizationService.GetString("ViewMode_Card");

    public string ReviewWindowsAppsBanner => ReviewWindowsAppsBannerText;
    public string ReviewExternalAppsBanner => ReviewExternalAppsBannerText;

    public string ReviewWindowsAppsBannerText
    {
        get
        {
            if (IsWindowsAppsInstallAction)
                return _localizationService.GetString("Review_Mode_Action_Install") ?? "Checked apps will be installed when you apply the config";
            if (IsWindowsAppsRemoveAction)
                return _localizationService.GetString("Review_Mode_Action_Remove") ?? "Checked apps will be removed when you apply the config";
            return _localizationService.GetString("Review_Mode_Select_Action") ?? "Select an action for checked apps using the checkboxes above";
        }
    }

    public string ReviewExternalAppsBannerText
    {
        get
        {
            if (IsExternalAppsInstallAction)
                return _localizationService.GetString("Review_Mode_Action_Install") ?? "Checked apps will be installed when you apply the config";
            if (IsExternalAppsRemoveAction)
                return _localizationService.GetString("Review_Mode_Action_Remove") ?? "Checked apps will be removed when you apply the config";
            return _localizationService.GetString("Review_Mode_Select_Action") ?? "Select an action for checked apps using the checkboxes above";
        }
    }

    public string RemoveButtonText => _localizationService.GetString("SoftwareApps_Button_UninstallSelected");

    public bool IsLoading => IsWindowsAppsTabSelected
        ? WindowsAppsViewModel.IsLoading
        : ExternalAppsViewModel.IsLoading;

    partial void OnIsCardViewModeChanged(bool value)
    {
        _ = _userPreferencesService.SetPreferenceAsync("SoftwareAppsViewMode", value ? "Card" : "Table");
    }

    partial void OnSearchTextChanged(string value)
    {
        if (IsWindowsAppsTabSelected)
        {
            WindowsAppsViewModel.SearchText = value;
        }
        else
        {
            ExternalAppsViewModel.SearchText = value;
        }
    }

    partial void OnIsWindowsAppsTabSelectedChanged(bool value)
    {
        if (value)
        {
            IsExternalAppsTabSelected = false;
            WindowsAppsViewModel.SearchText = SearchText;
            ExternalAppsViewModel.SearchText = string.Empty;
        }
        OnPropertyChanged(nameof(RemoveButtonText));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(CurrentInstallAction));
        OnPropertyChanged(nameof(CurrentRemoveAction));
        UpdateButtonStates();
    }

    partial void OnIsExternalAppsTabSelectedChanged(bool value)
    {
        if (value)
        {
            IsWindowsAppsTabSelected = false;
            ExternalAppsViewModel.SearchText = SearchText;
            WindowsAppsViewModel.SearchText = string.Empty;
        }
        OnPropertyChanged(nameof(RemoveButtonText));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(CurrentInstallAction));
        OnPropertyChanged(nameof(CurrentRemoveAction));
        UpdateButtonStates();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(WindowsAppsTabText));
        OnPropertyChanged(nameof(ExternalAppsTabText));
        OnPropertyChanged(nameof(InstallButtonText));
        OnPropertyChanged(nameof(RemoveButtonText));
        OnPropertyChanged(nameof(RefreshButtonText));
        OnPropertyChanged(nameof(HelpButtonText));
        OnPropertyChanged(nameof(ViewModeTableTooltip));
        OnPropertyChanged(nameof(ViewModeCardTooltip));
        OnPropertyChanged(nameof(ReviewWindowsAppsBannerText));
        OnPropertyChanged(nameof(ReviewExternalAppsBannerText));
    }

    private void ChildViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WindowsAppsViewModel.HasSelectedItems) ||
            e.PropertyName == nameof(ExternalAppsViewModel.HasSelectedItems) ||
            e.PropertyName == nameof(WindowsAppsViewModel.IsTaskRunning) ||
            e.PropertyName == nameof(ExternalAppsViewModel.IsTaskRunning))
        {
            UpdateButtonStates();
        }
        else if (e.PropertyName == nameof(WindowsAppsViewModel.IsLoading) ||
                 e.PropertyName == nameof(ExternalAppsViewModel.IsLoading))
        {
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    private void ChildViewModel_SelectedItemsChanged(object? sender, EventArgs e)
    {
        UpdateButtonStates();
        UpdateSelectedCounts();
    }

    private void OnReviewModeChanged(object? sender, EventArgs e)
    {
        IsInReviewMode = _configReviewService.IsInReviewMode;

        if (!IsInReviewMode)
        {
            // Reset action choices when exiting review mode
            IsWindowsAppsInstallAction = false;
            IsWindowsAppsRemoveAction = false;
            IsExternalAppsInstallAction = false;
            IsExternalAppsRemoveAction = false;
        }

        UpdateButtonStates();
        UpdateSelectedCounts();
    }

    private void UpdateSelectedCounts()
    {
        if (!IsInReviewMode)
        {
            WindowsAppsSelectedCount = 0;
            ExternalAppsSelectedCount = 0;
            return;
        }

        WindowsAppsSelectedCount = WindowsAppsViewModel.Items?.Count(a => a.IsSelected) ?? 0;
        ExternalAppsSelectedCount = ExternalAppsViewModel.Items?.Count(a => a.IsSelected) ?? 0;

        // Re-evaluate reviewed state since it depends on selected counts
        OnPropertyChanged(nameof(IsSoftwareAppsReviewed));
        SyncSoftwareAppsReviewedState();
    }

    private void UpdateButtonStates()
    {
        bool isAnyTaskRunning = WindowsAppsViewModel.IsTaskRunning || ExternalAppsViewModel.IsTaskRunning;

        if (IsInReviewMode)
        {
            CanInstallItems = false;
            CanRemoveItems = false;
        }
        else if (IsWindowsAppsTabSelected)
        {
            var hasSelected = WindowsAppsViewModel.HasSelectedItems;
            CanInstallItems = hasSelected && !isAnyTaskRunning;
            CanRemoveItems = hasSelected && !isAnyTaskRunning;
        }
        else if (IsExternalAppsTabSelected)
        {
            var hasSelected = ExternalAppsViewModel.HasSelectedItems;
            CanInstallItems = hasSelected && !isAnyTaskRunning;
            CanRemoveItems = hasSelected && !isAnyTaskRunning;
        }
        else
        {
            CanInstallItems = false;
            CanRemoveItems = false;
        }

        InstallSelectedItemsCommand.NotifyCanExecuteChanged();
        RemoveSelectedItemsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        _logService.LogInformation("[SoftwareAppsViewModel] InitializeAsync started");

        try
        {
            if (!WindowsAppsViewModel.IsInitialized)
            {
                _logService.LogInformation("[SoftwareAppsViewModel] Loading WindowsAppsViewModel");
                await WindowsAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
            }

            if (!ExternalAppsViewModel.IsInitialized)
            {
                _logService.LogInformation("[SoftwareAppsViewModel] Loading ExternalAppsViewModel");
                await ExternalAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
            }

            _logService.LogInformation("[SoftwareAppsViewModel] InitializeAsync completed");
        }
        catch (Exception ex)
        {
            _logService.LogError($"[SoftwareAppsViewModel] Error in InitializeAsync: {ex.Message}", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallItems))]
    private async Task InstallSelectedItemsAsync()
    {
        if (IsWindowsAppsTabSelected)
        {
            await WindowsAppsViewModel.InstallAppsAsync();
        }
        else
        {
            await ExternalAppsViewModel.InstallAppsAsync();
        }
        UpdateButtonStates();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveItems))]
    private async Task RemoveSelectedItemsAsync()
    {
        if (IsWindowsAppsTabSelected)
        {
            await WindowsAppsViewModel.RemoveAppsAsync();
        }
        else
        {
            await ExternalAppsViewModel.UninstallAppsAsync();
        }
        UpdateButtonStates();
    }

    [RelayCommand]
    private async Task RefreshInstallationStatusAsync()
    {
        if (IsWindowsAppsTabSelected)
        {
            await WindowsAppsViewModel.RefreshInstallationStatusAsync();
        }
        else
        {
            await ExternalAppsViewModel.RefreshInstallationStatusAsync();
        }
    }

    [RelayCommand]
    public void SelectWindowsAppsTab()
    {
        IsWindowsAppsTabSelected = true;
    }

    [RelayCommand]
    public void SelectExternalAppsTab()
    {
        IsExternalAppsTabSelected = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _configReviewService.ReviewModeChanged -= OnReviewModeChanged;
            WindowsAppsViewModel.PropertyChanged -= ChildViewModel_PropertyChanged;
            ExternalAppsViewModel.PropertyChanged -= ChildViewModel_PropertyChanged;
            WindowsAppsViewModel.SelectedItemsChanged -= ChildViewModel_SelectedItemsChanged;
            ExternalAppsViewModel.SelectedItemsChanged -= ChildViewModel_SelectedItemsChanged;
        }
        base.Dispose(disposing);
    }
}
