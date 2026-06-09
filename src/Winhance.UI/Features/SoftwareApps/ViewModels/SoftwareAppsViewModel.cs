using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.SoftwareApps.Models;
using Winhance.UI.Features.SoftwareApps.Views;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

public partial class SoftwareAppsViewModel : BaseViewModel
{
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IConfigReviewModeService _configReviewModeService;
    private readonly IConfigReviewBadgeService _configReviewBadgeService;
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IApplicationModeService _applicationModeService;
    private bool _isSubscribed;

    public SoftwareAppsViewModel(
        WindowsAppsViewModel windowsAppsViewModel,
        ExternalAppsViewModel externalAppsViewModel,
        ILocalizationService localizationService,
        ILogService logService,
        IDialogService dialogService,
        IUserPreferencesService userPreferencesService,
        IConfigReviewModeService configReviewModeService,
        IConfigReviewBadgeService configReviewBadgeService,
        IScheduledTaskService scheduledTaskService,
        IFileSystemService fileSystemService,
        IApplicationModeService applicationModeService)
    {
        WindowsAppsViewModel = windowsAppsViewModel;
        ExternalAppsViewModel = externalAppsViewModel;
        _localizationService = localizationService;
        _logService = logService;
        _dialogService = dialogService;
        _userPreferencesService = userPreferencesService;
        _configReviewModeService = configReviewModeService;
        _configReviewBadgeService = configReviewBadgeService;
        _scheduledTaskService = scheduledTaskService;
        _fileSystemService = fileSystemService;
        _applicationModeService = applicationModeService;

        // Initialize partial property defaults (SearchText first since
        // tab-change handlers forward it to child ViewModels)
        SearchText = string.Empty;
        IsWindowsAppsTabSelected = true;
    }

    public WindowsAppsViewModel WindowsAppsViewModel { get; }
    public ExternalAppsViewModel ExternalAppsViewModel { get; }

    [ObservableProperty]
    public partial bool IsWindowsAppsTabSelected { get; set; }

    [ObservableProperty]
    public partial bool IsExternalAppsTabSelected { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial SoftwareAppsViewMode ViewMode { get; set; } = SoftwareAppsViewMode.Card;

    public bool IsCardView => ViewMode == SoftwareAppsViewMode.Card;
    public bool IsTableView => ViewMode == SoftwareAppsViewMode.Table;
    public bool IsCompactView => ViewMode == SoftwareAppsViewMode.Compact;

    [ObservableProperty]
    public partial AppSortMode SortMode { get; set; } = AppSortMode.NameAscInstalledFirst;

    public bool IsSortInstalledFirst => SortMode == AppSortMode.NameAscInstalledFirst;
    public bool IsSortNameAsc => SortMode == AppSortMode.NameAsc;
    public bool IsSortNameDesc => SortMode == AppSortMode.NameDesc;

    [ObservableProperty]
    public partial bool IsInReviewMode { get; set; }

    [ObservableProperty]
    public partial int WindowsAppsSelectedCount { get; set; }

    [ObservableProperty]
    public partial int ExternalAppsSelectedCount { get; set; }

    // Action choice properties for review mode
    [ObservableProperty]
    public partial bool IsWindowsAppsInstallAction { get; set; }

    [ObservableProperty]
    public partial bool IsWindowsAppsRemoveAction { get; set; }

    [ObservableProperty]
    public partial bool IsExternalAppsInstallAction { get; set; }

    [ObservableProperty]
    public partial bool IsExternalAppsRemoveAction { get; set; }

    [ObservableProperty]
    public partial bool CanInstallItems { get; set; }

    [ObservableProperty]
    public partial bool CanRemoveItems { get; set; }

    public bool IsWindowsAppsActionChosen => IsWindowsAppsInstallAction || IsWindowsAppsRemoveAction;
    public bool IsExternalAppsActionChosen => IsExternalAppsInstallAction || IsExternalAppsRemoveAction;

    public bool HasWindowsAppsInConfig => _configReviewBadgeService.IsFeatureInConfig(
        FeatureIds.WindowsApps);
    public bool HasExternalAppsInConfig => _configReviewBadgeService.IsFeatureInConfig(
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
        _configReviewBadgeService.IsSoftwareAppsReviewed = IsSoftwareAppsReviewed;
        _configReviewBadgeService.NotifyBadgeStateChanged();
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
    public string ViewModeCompactTooltip => _localizationService.GetString("ViewMode_Compact");

    public string SortButtonText => _localizationService.GetString("SoftwareApps_Sort_Button") ?? "Sort";
    public string SortInstalledFirstText => _localizationService.GetString("SoftwareApps_Sort_NameAZInstalledFirst") ?? "Name A-Z (Installed First)";
    public string SortNameAscText => _localizationService.GetString("SoftwareApps_Sort_NameAZ") ?? "Name A-Z";
    public string SortNameDescText => _localizationService.GetString("SoftwareApps_Sort_NameZA") ?? "Name Z-A";

    /// <summary>
    /// Hint shown when hovering the Sort button while it is disabled in Table view —
    /// sorting in Table view is driven by clicking the column headers instead.
    /// </summary>
    public string SortTableHintText => _localizationService.GetString("SoftwareApps_Sort_TableHint") ?? "Click a column header to sort by it in ascending or descending order.";

    /// <summary>
    /// The Sort dropdown is only usable in Card and Compact views; in Table view sorting is
    /// done by clicking a column header (see <see cref="SortTableHintText"/>), so the button
    /// is disabled there.
    /// </summary>
    public bool IsSortButtonEnabled => !IsTableView;

    // Table-view column header texts. Applied to the DataGrid columns in code-behind
    // (SoftwareAppsPage.LocalizeColumnHeaders) because CommunityToolkit DataGrid columns
    // live outside the page's compiled-binding tree, and re-applied on language change.
    public string ColumnHeaderName => _localizationService.GetString("SoftwareApps_Column_Name") ?? "Name";
    public string ColumnHeaderDescription => _localizationService.GetString("SoftwareApps_Column_Description") ?? "Description";
    public string ColumnHeaderType => _localizationService.GetString("SoftwareApps_Column_Type") ?? "Type";
    public string ColumnHeaderStatus => _localizationService.GetString("SoftwareApps_Column_Status") ?? "Status";
    public string ColumnHeaderInstallable => _localizationService.GetString("SoftwareApps_Column_Installable") ?? "Installable";
    public string ColumnHeaderGroup => _localizationService.GetString("SoftwareApps_Column_Group") ?? "Group";

    public string ReviewWindowsAppsBannerText
    {
        get
        {
            if (IsWindowsAppsInstallAction)
                return _localizationService.GetString("Review_Mode_Action_Install");
            if (IsWindowsAppsRemoveAction)
                return _localizationService.GetString("Review_Mode_Action_Remove");
            return _localizationService.GetString("Review_Mode_Select_Action");
        }
    }

    public string ReviewExternalAppsBannerText
    {
        get
        {
            if (IsExternalAppsInstallAction)
                return _localizationService.GetString("Review_Mode_Action_Install");
            if (IsExternalAppsRemoveAction)
                return _localizationService.GetString("Review_Mode_Action_Remove");
            return _localizationService.GetString("Review_Mode_Select_Action");
        }
    }

    public string RemoveButtonText => _localizationService.GetString("SoftwareApps_Button_UninstallSelected");

    public bool IsLoading => IsWindowsAppsTabSelected
        ? WindowsAppsViewModel.IsLoading
        : ExternalAppsViewModel.IsLoading;

    partial void OnViewModeChanged(SoftwareAppsViewMode value)
    {
        _userPreferencesService.SetPreferenceAsync("SoftwareAppsViewMode", value.ToString()).FireAndForget(_logService);
        OnPropertyChanged(nameof(IsCardView));
        OnPropertyChanged(nameof(IsTableView));
        OnPropertyChanged(nameof(IsCompactView));
        OnPropertyChanged(nameof(IsSortButtonEnabled));
    }

    partial void OnSortModeChanged(AppSortMode value)
    {
        _userPreferencesService.SetPreferenceAsync("SoftwareAppsSortMode", value.ToString()).FireAndForget(_logService);
        WindowsAppsViewModel.SortMode = value;
        ExternalAppsViewModel.SortMode = value;
        OnPropertyChanged(nameof(IsSortInstalledFirst));
        OnPropertyChanged(nameof(IsSortNameAsc));
        OnPropertyChanged(nameof(IsSortNameDesc));
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
        OnPropertyChanged(nameof(ExternalAppsTabLockedTooltip));
        OnPropertyChanged(nameof(BuilderWindowsAppsBannerText));
        OnPropertyChanged(nameof(BuilderExternalAppsBannerText));
        OnPropertyChanged(nameof(InstallButtonText));
        OnPropertyChanged(nameof(RemoveButtonText));
        OnPropertyChanged(nameof(RefreshButtonText));
        OnPropertyChanged(nameof(HelpButtonText));
        OnPropertyChanged(nameof(ViewModeTableTooltip));
        OnPropertyChanged(nameof(ViewModeCardTooltip));
        OnPropertyChanged(nameof(ViewModeCompactTooltip));
        OnPropertyChanged(nameof(SortButtonText));
        OnPropertyChanged(nameof(SortInstalledFirstText));
        OnPropertyChanged(nameof(SortNameAscText));
        OnPropertyChanged(nameof(SortNameDescText));
        OnPropertyChanged(nameof(SortTableHintText));
        OnPropertyChanged(nameof(ColumnHeaderName));
        OnPropertyChanged(nameof(ColumnHeaderDescription));
        OnPropertyChanged(nameof(ColumnHeaderType));
        OnPropertyChanged(nameof(ColumnHeaderStatus));
        OnPropertyChanged(nameof(ColumnHeaderInstallable));
        OnPropertyChanged(nameof(ColumnHeaderGroup));
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
        IsInReviewMode = _configReviewModeService.IsInReviewMode;

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

    // Builder mode authors a config without touching this PC, so live install/remove
    // must stay disabled — app checkboxes are serialized into the saved config instead.
    private bool IsBuilderMode => _applicationModeService.CurrentMode == WinhanceMode.Builder;

    /// <summary>True while the app is in Builder mode (drives the Builder info ribbons).</summary>
    public bool IsBuilderModeActive => IsBuilderMode;

    // Autounattend removes Windows apps from the image and can't install external apps
    // (those install post-setup, and that path isn't built yet) — lock the External Apps
    // tab while the Autounattend target is active.
    public bool IsExternalAppsTabLocked =>
        IsBuilderMode && _applicationModeService.CurrentBuilderTarget == BuilderTarget.Autounattend;

    public bool IsExternalAppsTabEnabled => !IsExternalAppsTabLocked;

    public string? ExternalAppsTabLockedTooltip =>
        IsExternalAppsTabLocked
            ? (_localizationService.GetString("SoftwareApps_ExternalTab_AutounattendLocked")
                ?? "External Apps in autounattend is an upcoming feature.")
            : null;

    public string BuilderWindowsAppsBannerText =>
        _applicationModeService.CurrentBuilderTarget == BuilderTarget.Autounattend
            ? (_localizationService.GetString("SoftwareApps_Builder_Banner_Autounattend_WindowsApps")
                ?? "Checked apps will be removed from the Windows image during installation.")
            : (_localizationService.GetString("SoftwareApps_Builder_Banner_Config")
                ?? "These selections are saved to the config. You choose whether to install or uninstall them when you import.");

    public string BuilderExternalAppsBannerText =>
        _localizationService.GetString("SoftwareApps_Builder_Banner_Config")
            ?? "These selections are saved to the config. You choose whether to install or uninstall them when you import.";

    private void OnApplicationModeChanged(object? sender, EventArgs e)
    {
        // If the External Apps tab just got locked (Builder + Autounattend) while open,
        // move the user to Windows Apps so they aren't stranded on a disabled tab.
        if (IsExternalAppsTabLocked && IsExternalAppsTabSelected)
        {
            IsWindowsAppsTabSelected = true;
        }

        UpdateButtonStates();
        OnPropertyChanged(nameof(IsBuilderModeActive));
        OnPropertyChanged(nameof(IsExternalAppsTabLocked));
        OnPropertyChanged(nameof(IsExternalAppsTabEnabled));
        OnPropertyChanged(nameof(ExternalAppsTabLockedTooltip));
        OnPropertyChanged(nameof(BuilderWindowsAppsBannerText));
        OnPropertyChanged(nameof(BuilderExternalAppsBannerText));
    }

    private void UpdateButtonStates()
    {
        bool isAnyTaskRunning = WindowsAppsViewModel.IsTaskRunning || ExternalAppsViewModel.IsTaskRunning;

        if (IsInReviewMode || IsBuilderMode)
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

    /// <summary>
    /// One-time event subscription / preference load. Idempotent — guarded by
    /// <see cref="_isSubscribed"/>. Called from each Initialize* entry point so
    /// any of them can be the first call without ordering assumptions.
    /// </summary>
    private void EnsureSubscriptions()
    {
        if (_isSubscribed) return;
        _isSubscribed = true;

        var savedViewMode = _userPreferencesService.GetPreference("SoftwareAppsViewMode", "Card");
        ViewMode = savedViewMode switch
        {
            "Compact" => SoftwareAppsViewMode.Compact,
            "Table" => SoftwareAppsViewMode.Table,
            _ => SoftwareAppsViewMode.Card,
        };

        var savedSortMode = _userPreferencesService.GetPreference("SoftwareAppsSortMode", "NameAscInstalledFirst");
        SortMode = savedSortMode switch
        {
            "NameAsc" => AppSortMode.NameAsc,
            "NameDesc" => AppSortMode.NameDesc,
            _ => AppSortMode.NameAscInstalledFirst,
        };

        WindowsAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
        ExternalAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
        WindowsAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;
        ExternalAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;
        _configReviewModeService.ReviewModeChanged += OnReviewModeChanged;
        _applicationModeService.ModeChanged += OnApplicationModeChanged;

        UpdateButtonStates();
    }

    /// <summary>
    /// Loads only the Windows Apps tab. Called by the cold-start path so the
    /// startup loading overlay can drop as soon as the fast tab is ready,
    /// without waiting on the slower External Apps icon resolution.
    /// </summary>
    public async Task InitializeWindowsAppsAsync()
    {
        EnsureSubscriptions();
        if (!WindowsAppsViewModel.IsInitialized)
        {
            _logService.LogInformation("[SoftwareAppsViewModel] Loading WindowsAppsViewModel");
            await WindowsAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
        }
    }

    /// <summary>
    /// Loads only the External Apps tab. Cold-start fires this in the background
    /// (see <see cref="StartupUiCoordinator"/>); the tab's per-tab loading
    /// overlay (bound to <see cref="ExternalAppsViewModel.IsLoading"/>) covers
    /// the case where the user clicks External Apps before resolution completes.
    /// </summary>
    public async Task InitializeExternalAppsAsync()
    {
        EnsureSubscriptions();
        if (!ExternalAppsViewModel.IsInitialized)
        {
            _logService.LogInformation("[SoftwareAppsViewModel] Loading ExternalAppsViewModel");
            await ExternalAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        _logService.LogInformation("[SoftwareAppsViewModel] InitializeAsync started");

        try
        {
            await InitializeWindowsAppsAsync();
            await InitializeExternalAppsAsync();
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
    private async Task ShowHelpAsync()
    {
        var closeButtonText = _localizationService.GetString("Help_CloseHelp");

        if (IsWindowsAppsTabSelected)
        {
            var vm = new RemovalStatusContainerViewModel(_scheduledTaskService, _logService, _fileSystemService);
            var content = new WindowsAppsHelpContent(_localizationService) { DataContext = vm };
            _ = vm.RefreshAllStatusesAsync();
            await _dialogService.ShowCustomContentDialogAsync(
                _localizationService.GetString("Help_WindowsApps_Title"),
                content,
                closeButtonText);
            vm.Dispose();
        }
        else
        {
            await _dialogService.ShowCustomContentDialogAsync(
                _localizationService.GetString("Help_ExternalApps_Title"),
                new ExternalAppsHelpContent(_localizationService),
                closeButtonText);
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
            _configReviewModeService.ReviewModeChanged -= OnReviewModeChanged;
            _applicationModeService.ModeChanged -= OnApplicationModeChanged;
            WindowsAppsViewModel.PropertyChanged -= ChildViewModel_PropertyChanged;
            ExternalAppsViewModel.PropertyChanged -= ChildViewModel_PropertyChanged;
            WindowsAppsViewModel.SelectedItemsChanged -= ChildViewModel_SelectedItemsChanged;
            ExternalAppsViewModel.SelectedItemsChanged -= ChildViewModel_SelectedItemsChanged;
        }
        base.Dispose(disposing);
    }
}
