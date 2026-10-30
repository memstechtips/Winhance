using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
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
    private readonly ISaveFilePicker _saveFilePicker;
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
        IApplicationModeService applicationModeService,
        ISaveFilePicker saveFilePicker)
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
        _saveFilePicker = saveFilePicker;

        // SearchText first: the tab-change handlers forward it to the child ViewModels.
        SearchText = string.Empty;
        IsWindowsAppsTabSelected = true;
    }

    public WindowsAppsViewModel WindowsAppsViewModel { get; }
    public ExternalAppsViewModel ExternalAppsViewModel { get; }

    [ObservableProperty]
    public partial bool IsWindowsAppsTabSelected { get; set; }

    [ObservableProperty]
    public partial bool IsExternalAppsTabSelected { get; set; }

    // A config review chooses an action for the ticked apps instead of running one.
    public bool IsActionButtonVisible => !IsInReviewMode;

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

    // A section is reviewed when it is not in the config, OR no items are selected (apply nothing), OR an action
    // (Install/Remove) has been chosen.
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

    public string PageTitle => _localizationService.GetString("Category_SoftwareApps_Title");
    public string PageDescription => _localizationService.GetString("Category_SoftwareApps_StatusText");
    public string SearchPlaceholder => _localizationService.GetStringOrDefault("Common_Search_Placeholder", "Type here to search...");
    public string WindowsAppsTabText => _localizationService.GetString("SoftwareApps_Tab_WindowsApps");
    public string ExternalAppsTabText => _localizationService.GetString("SoftwareApps_Tab_ExternalApps");
    public string PackageSearchPlaceholder => _localizationService.GetString("ExternalApps_PackageSearch_Placeholder");
    public string InstallButtonText => _localizationService.GetString("SoftwareApps_Button_InstallSelected");

    // On External Software the Install button also updates, so its label names what it will do with the ticked rows.
    // Builder and a config review only choose apps to install, whatever this PC has an update for.
    public string PrimaryActionText
    {
        get
        {
            if (!IsExternalAppsTabSelected || !_applicationModeService.Capabilities().AppliesToSystem)
                return InstallButtonText;

            var selected = ExternalAppsViewModel.Items.Where(a => a.IsSelected).ToList();
            var updates = selected.Count(a => a.HasUpdate);
            var key = updates == 0 ? "SoftwareApps_Button_InstallSelected"
                : updates == selected.Count ? "SoftwareApps_Button_UpdateSelected"
                : "SoftwareApps_Button_InstallOrUpdateSelected";
            return _localizationService.GetString(key);
        }
    }

    public string RefreshButtonText => _localizationService.GetString("Button_Refresh");
    public string HelpButtonText => _localizationService.GetString("Button_Help");
    public string InstallOptionsButtonText => _localizationService.GetString("ExternalApps_InstallOptions_Button");
    public string InstallOptionsMenuText => _localizationService.GetString("ExternalApps_InstallOptions_Menu");

    public string ViewModeTableTooltip => _localizationService.GetString("ViewMode_Table");
    public string ViewModeCardTooltip => _localizationService.GetString("ViewMode_Card");
    public string ViewModeCompactTooltip => _localizationService.GetString("ViewMode_Compact");

    public string ViewMenuText => _localizationService.GetString("View_Menu");

    public string SortInstalledFirstText => _localizationService.GetStringOrDefault("SoftwareApps_Sort_NameAZInstalledFirst", "Name A-Z (Installed First)");
    public string SortNameAscText => _localizationService.GetStringOrDefault("SoftwareApps_Sort_NameAZ", "Name A-Z");
    public string SortNameDescText => _localizationService.GetStringOrDefault("SoftwareApps_Sort_NameZA", "Name Z-A");

    public string FilterAllText => _localizationService.GetString("SoftwareApps_Filter_All");
    public string FilterInstalledText => _localizationService.GetString("SoftwareApps_ShowInstalledOnly");
    public string FilterUpdatesText => _localizationService.GetString("SoftwareApps_ShowUpdatesOnly");

    public string ExportListText => _localizationService.GetString("SoftwareApps_ExportList");

    public string SortTableHintText => _localizationService.GetStringOrDefault("SoftwareApps_Sort_TableHint", "Sort with the column headers");

    // In Table view sorting is by clicking a column header, so the sort items are disabled there.
    public bool IsSortEnabled => !IsTableView;

    // Table-view column header texts. Applied to the DataGrid columns in code-behind
    // (SoftwareAppsPage.LocalizeColumnHeaders) because CommunityToolkit DataGrid columns
    // live outside the page's compiled-binding tree, and re-applied on language change.
    public string ColumnHeaderName => _localizationService.GetStringOrDefault("SoftwareApps_Column_Name", "Name");
    public string ColumnHeaderDescription => _localizationService.GetStringOrDefault("SoftwareApps_Column_Description", "Description");
    public string ColumnHeaderType => _localizationService.GetStringOrDefault("SoftwareApps_Column_Type", "Type");
    public string ColumnHeaderStatus => _localizationService.GetStringOrDefault("SoftwareApps_Column_Status", "Status");
    public string ColumnHeaderInstallable => _localizationService.GetStringOrDefault("SoftwareApps_Column_Installable", "Installable");
    public string ColumnHeaderGroup => _localizationService.GetStringOrDefault("SoftwareApps_Column_Group", "Group");
    public string ColumnHeaderVersion => _localizationService.GetString("SoftwareApps_Column_Version");
    public string ColumnHeaderUpdate => _localizationService.GetString("SoftwareApps_Column_Update");
    public string ColumnHeaderInstalledOn => _localizationService.GetString("SoftwareApps_Column_InstalledOn");
    public string ColumnHeaderSize => _localizationService.GetString("SoftwareApps_Column_Size");
    private string ColumnHeaderPublisher => _localizationService.GetStringOrDefault("SoftwareApps_Column_Publisher", "Publisher");
    private string ColumnHeaderPackageId => _localizationService.GetStringOrDefault("SoftwareApps_Column_PackageId", "Package ID");

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

    public bool IsLoading => IsWindowsAppsTabSelected ? WindowsAppsViewModel.IsLoading : ExternalAppsViewModel.IsLoading;

    partial void OnViewModeChanged(SoftwareAppsViewMode value)
    {
        _userPreferencesService.SetPreferenceAsync(UserPreferenceKeys.SoftwareAppsViewMode, value.ToString()).FireAndForget(_logService);
        OnPropertyChanged(nameof(IsCardView));
        OnPropertyChanged(nameof(IsTableView));
        OnPropertyChanged(nameof(IsCompactView));
        OnPropertyChanged(nameof(IsSortEnabled));
    }

    partial void OnSortModeChanged(AppSortMode value)
    {
        _userPreferencesService.SetPreferenceAsync(UserPreferenceKeys.SoftwareAppsSortMode, value.ToString()).FireAndForget(_logService);
        WindowsAppsViewModel.SortMode = value;
        ExternalAppsViewModel.SortMode = value;
        OnPropertyChanged(nameof(IsSortInstalledFirst));
        OnPropertyChanged(nameof(IsSortNameAsc));
        OnPropertyChanged(nameof(IsSortNameDesc));
    }

    public AppFilter CurrentFilter => IsWindowsAppsTabSelected ? WindowsAppsViewModel.Filter : ExternalAppsViewModel.Filter;

    public bool IsFilterAll => CurrentFilter == AppFilter.All;
    public bool IsFilterInstalled => CurrentFilter == AppFilter.Installed;
    public bool IsFilterUpdates => CurrentFilter == AppFilter.UpdatesAvailable;

    // All and Installed apply to both tabs and are remembered. Updates available exists on External Software only and
    // lasts for the session, so Windows Apps keeps the filter it had.
    public void SetFilter(AppFilter value)
    {
        ExternalAppsViewModel.Filter = value;
        if (value != AppFilter.UpdatesAvailable)
        {
            WindowsAppsViewModel.Filter = value;
            _userPreferencesService.SetPreferenceAsync(UserPreferenceKeys.SoftwareAppsFilter, value.ToString()).FireAndForget(_logService);
        }
        NotifyFilterChanged();
    }

    private void NotifyFilterChanged()
    {
        OnPropertyChanged(nameof(CurrentFilter));
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterInstalled));
        OnPropertyChanged(nameof(IsFilterUpdates));
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
        OnPropertyChanged(nameof(IsInstallAccent));
        OnPropertyChanged(nameof(IsRemoveAccent));
        NotifyFilterChanged();
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
        OnPropertyChanged(nameof(IsInstallAccent));
        OnPropertyChanged(nameof(IsRemoveAccent));
        NotifyFilterChanged();
        UpdateButtonStates();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(WindowsAppsTabText));
        OnPropertyChanged(nameof(ExternalAppsTabText));
        OnPropertyChanged(nameof(PackageSearchPlaceholder));
        OnPropertyChanged(nameof(BuilderWindowsAppsBannerText));
        OnPropertyChanged(nameof(BuilderExternalAppsBannerText));
        OnPropertyChanged(nameof(InstallButtonText));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(RemoveButtonText));
        OnPropertyChanged(nameof(RefreshButtonText));
        OnPropertyChanged(nameof(HelpButtonText));
        OnPropertyChanged(nameof(InstallOptionsButtonText));
        OnPropertyChanged(nameof(ViewModeTableTooltip));
        OnPropertyChanged(nameof(ViewModeCardTooltip));
        OnPropertyChanged(nameof(ViewModeCompactTooltip));
        OnPropertyChanged(nameof(ViewMenuText));
        OnPropertyChanged(nameof(SortInstalledFirstText));
        OnPropertyChanged(nameof(SortNameAscText));
        OnPropertyChanged(nameof(SortNameDescText));
        OnPropertyChanged(nameof(SortTableHintText));
        OnPropertyChanged(nameof(FilterAllText));
        OnPropertyChanged(nameof(FilterInstalledText));
        OnPropertyChanged(nameof(FilterUpdatesText));
        OnPropertyChanged(nameof(ExportListText));
        OnPropertyChanged(nameof(ColumnHeaderUpdate));
        OnPropertyChanged(nameof(ColumnHeaderName));
        OnPropertyChanged(nameof(ColumnHeaderDescription));
        OnPropertyChanged(nameof(ColumnHeaderType));
        OnPropertyChanged(nameof(ColumnHeaderStatus));
        OnPropertyChanged(nameof(ColumnHeaderInstallable));
        OnPropertyChanged(nameof(ColumnHeaderGroup));
        OnPropertyChanged(nameof(ColumnHeaderVersion));
        OnPropertyChanged(nameof(ColumnHeaderInstalledOn));
        OnPropertyChanged(nameof(ColumnHeaderSize));
        OnPropertyChanged(nameof(ReviewWindowsAppsBannerText));
        OnPropertyChanged(nameof(ReviewExternalAppsBannerText));
    }

    private void ChildViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WindowsAppsViewModel.HasSelectedItems) or nameof(WindowsAppsViewModel.IsTaskRunning))
        {
            UpdateButtonStates();
        }
        else if (e.PropertyName == nameof(WindowsAppsViewModel.IsLoading))
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
            IsWindowsAppsInstallAction = false;
            IsWindowsAppsRemoveAction = false;
            IsExternalAppsInstallAction = false;
            IsExternalAppsRemoveAction = false;
        }

        UpdateButtonStates();
        UpdateSelectedCounts();
    }

    partial void OnIsInReviewModeChanged(bool value) => OnPropertyChanged(nameof(IsActionButtonVisible));

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

    public bool IsBuilderModeActive => IsBuilderMode;

    private bool IsAutounattendTarget =>
        IsBuilderMode && _applicationModeService.CurrentBuilderTarget == BuilderTarget.Autounattend;

    // The answer file removes the checked Windows Apps and installs the checked External Apps, so each tab has one
    // action. Its button is drawn in the accent colour and does nothing when clicked; the other stays disabled.
    public bool IsInstallAccent => IsAutounattendTarget && IsExternalAppsTabSelected;

    public bool IsRemoveAccent => IsAutounattendTarget && IsWindowsAppsTabSelected;

    public string BuilderWindowsAppsBannerText =>
        _applicationModeService.CurrentBuilderTarget == BuilderTarget.Autounattend
            ? (_localizationService.GetStringOrDefault("SoftwareApps_Builder_Banner_Autounattend_WindowsApps", "Checked apps will be removed from the Windows image during installation."))
            : (_localizationService.GetStringOrDefault("SoftwareApps_Builder_Banner_Config", "These selections are saved to the config. You choose whether to install or uninstall them when you import."));

    public string BuilderExternalAppsBannerText =>
        _applicationModeService.CurrentBuilderTarget == BuilderTarget.Autounattend
            ? (_localizationService.GetStringOrDefault("SoftwareApps_Builder_Banner_Autounattend_ExternalApps", "Checked apps will be installed for everyone on the new PC after setup."))
            : (_localizationService.GetStringOrDefault("SoftwareApps_Builder_Banner_Config", "These selections are saved to the config. You choose whether to install or uninstall them when you import."));

    private void OnApplicationModeChanged(object? sender, EventArgs e)
    {
        var capabilities = _applicationModeService.Capabilities();
        ExternalAppsViewModel.AuthorsIntent = capabilities.AuthorsIntent;

        // Updates are this PC's, so a mode that picks apps for a file goes back to the remembered filter, which Windows
        // Apps still holds.
        if (!capabilities.AppliesToSystem && ExternalAppsViewModel.Filter == AppFilter.UpdatesAvailable)
        {
            ExternalAppsViewModel.Filter = WindowsAppsViewModel.Filter;
            NotifyFilterChanged();
        }

        UpdateButtonStates();
        OnPropertyChanged(nameof(IsBuilderModeActive));
        OnPropertyChanged(nameof(IsInstallAccent));
        OnPropertyChanged(nameof(IsRemoveAccent));
        OnPropertyChanged(nameof(BuilderWindowsAppsBannerText));
        OnPropertyChanged(nameof(BuilderExternalAppsBannerText));
    }

    private void UpdateButtonStates()
    {
        bool isAnyTaskRunning = WindowsAppsViewModel.IsTaskRunning || ExternalAppsViewModel.IsTaskRunning;

        // Install/remove touch this PC, so they are gated on the capability rather than on a list of
        // modes — a fourth mode that also must not write is then covered without editing this line.
        // Read from the mode service, which is the source the local IsInReviewMode flag is derived
        // from, so the gate cannot lag behind a transition that has already happened.
        if (!_applicationModeService.Capabilities().AppliesToSystem)
        {
            // A button bound to a command that cannot execute draws disabled, so the accent one must stay executable.
            CanInstallItems = IsInstallAccent;
            CanRemoveItems = IsRemoveAccent;
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

        OnPropertyChanged(nameof(PrimaryActionText));
        InstallSelectedItemsCommand.NotifyCanExecuteChanged();
        RemoveSelectedItemsCommand.NotifyCanExecuteChanged();
    }

    // Idempotent; called from each Initialize* entry point, so any of them can be the first call without ordering
    // assumptions.
    private void EnsureSubscriptions()
    {
        if (_isSubscribed) return;
        _isSubscribed = true;

        var savedViewMode = _userPreferencesService.GetPreference(UserPreferenceKeys.SoftwareAppsViewMode, "Card");
        ViewMode = savedViewMode switch
        {
            "Compact" => SoftwareAppsViewMode.Compact,
            "Table" => SoftwareAppsViewMode.Table,
            _ => SoftwareAppsViewMode.Card,
        };

        var savedSortMode = _userPreferencesService.GetPreference(UserPreferenceKeys.SoftwareAppsSortMode, "NameAscInstalledFirst");
        SortMode = savedSortMode switch
        {
            "NameAsc" => AppSortMode.NameAsc,
            "NameDesc" => AppSortMode.NameDesc,
            _ => AppSortMode.NameAscInstalledFirst,
        };

        // A preferences file written before SoftwareAppsFilter existed holds the choice as this bool.
        var legacyFilter = _userPreferencesService.GetPreference("SoftwareAppsShowInstalledOnly", false)
            ? nameof(AppFilter.Installed)
            : nameof(AppFilter.All);
        var savedFilter = _userPreferencesService.GetPreference(UserPreferenceKeys.SoftwareAppsFilter, legacyFilter);
        var filter = savedFilter == nameof(AppFilter.Installed) ? AppFilter.Installed : AppFilter.All;
        WindowsAppsViewModel.Filter = filter;
        ExternalAppsViewModel.Filter = filter;
        NotifyFilterChanged();

        WindowsAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
        ExternalAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
        WindowsAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;
        ExternalAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;
        _configReviewModeService.ReviewModeChanged += OnReviewModeChanged;
        _applicationModeService.ModeChanged += OnApplicationModeChanged;

        ExternalAppsViewModel.AuthorsIntent = _applicationModeService.Capabilities().AuthorsIntent;
        UpdateButtonStates();
    }

    // The cold-start path loads this first so the startup overlay can drop as soon as the fast tab is ready,
    // without waiting on External Apps icon resolution.
    public async Task InitializeWindowsAppsAsync()
    {
        EnsureSubscriptions();
        if (!WindowsAppsViewModel.IsInitialized)
        {
            _logService.LogInformation("Loading WindowsAppsViewModel");
            await WindowsAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
        }
    }

    // Cold-start fires this in the background (Helpers.StartupUiCoordinator); until it finishes the tab shows only
    // its loader (ExternalAppsViewModel.IsContentReady).
    public async Task InitializeExternalAppsAsync()
    {
        EnsureSubscriptions();
        if (!ExternalAppsViewModel.IsInitialized)
        {
            _logService.LogInformation("Loading ExternalAppsViewModel");
            await ExternalAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        _logService.LogInformation("InitializeAsync started");

        try
        {
            await InitializeWindowsAppsAsync();
            await InitializeExternalAppsAsync();
            _logService.LogInformation("InitializeAsync completed");
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error in InitializeAsync: {ex.Message}", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallItems))]
    private async Task InstallSelectedItemsAsync()
    {
        if (!_applicationModeService.Capabilities().AppliesToSystem)
            return;

        if (IsWindowsAppsTabSelected)
        {
            await WindowsAppsViewModel.InstallAppsAsync();
        }
        else if (IsExternalAppsTabSelected)
        {
            await ExternalAppsViewModel.InstallOrUpdateSelectedAsync();
        }
        UpdateButtonStates();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveItems))]
    private async Task RemoveSelectedItemsAsync()
    {
        if (!_applicationModeService.Capabilities().AppliesToSystem)
            return;

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

    public async Task InstallOnNewPcAsync(IReadOnlyList<string> ids, string? ownInstallersFolder)
    {
        SelectExternalAppsTab();
        await InitializeExternalAppsAsync();
        await ExternalAppsViewModel.InstallOnNewPcAsync(ids, ownInstallersFolder);
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

    // The whole tab, not the filtered view, in the Table view's columns plus publisher and package id, so two exports a
    // month apart compare line by line. A package search result lives only as long as its search, so it is left out
    // unless the PC has it.
    [RelayCommand]
    private async Task ExportListAsync()
    {
        var tab = IsWindowsAppsTabSelected ? "WindowsApps" : "ExternalApps";
        var path = _saveFilePicker.PickSavePath(
            _localizationService.GetString("SoftwareApps_ExportList_DialogTitle"),
            "CSV Files",
            "*.csv",
            $"Winhance_{tab}_{DateTime.Now:yyyyMMdd}.csv",
            "csv");
        if (path is null)
            return;

        var csv = IsWindowsAppsTabSelected
            ? AppListCsv.Build(WindowsAppsColumns(), WindowsAppsViewModel.Items)
            : AppListCsv.Build(ExternalAppsColumns(), ExternalAppsViewModel.Items.Where(IsExported));
        try
        {
            // Without the byte-order mark Excel reads the file in the ANSI code page and garbles non-English names.
            await _fileSystemService.WriteAllTextAsync(path, "\uFEFF" + csv);
            _logService.Log(LogLevel.Info, $"Exported the {tab} list to {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.Log(LogLevel.Error, $"Could not export the {tab} list to {path}: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(ex.Message);
        }
    }

    internal IReadOnlyList<(string Header, Func<AppItemViewModel, string> Value)> WindowsAppsColumns() =>
    [
        (ColumnHeaderName, app => app.Name),
        (ColumnHeaderDescription, app => app.Description),
        (ColumnHeaderType, app => app.ItemTypeDescription),
        (ColumnHeaderGroup, app => app.CategoryDisplayName),
        (ColumnHeaderStatus, app => app.InstalledStatusText),
        (ColumnHeaderInstallable, app => app.ReinstallableStatusText),
        (ColumnHeaderPackageId, app => app.Definition.AppxPackageName?.FirstOrDefault()
            ?? app.Definition.CapabilityName ?? app.Definition.OptionalFeatureName ?? string.Empty),
    ];

    internal IReadOnlyList<(string Header, Func<AppItemViewModel, string> Value)> ExternalAppsColumns() =>
    [
        (ColumnHeaderName, app => app.Name),
        (ColumnHeaderDescription, app => app.Description),
        (ColumnHeaderGroup, app => app.CategoryDisplayName),
        (ColumnHeaderVersion, app => app.Version),
        (ColumnHeaderUpdate, app => app.AvailableVersion),
        (ColumnHeaderInstalledOn, app => app.InstalledOnText),
        (ColumnHeaderSize, app => app.SizeText),
        (ColumnHeaderStatus, app => app.InstalledStatusText),
        (ColumnHeaderPublisher, app => app.Installed?.Publisher ?? string.Empty),
        (ColumnHeaderPackageId, app => app.Definition.WinGetPackageId?.FirstOrDefault() ?? app.Installed?.WinGetId
            ?? app.Definition.MsStoreId ?? app.Installed?.MsStoreId ?? app.Definition.ChocoPackageId
            ?? app.Definition.AppxPackageName?.FirstOrDefault() ?? app.Installed?.PackageName ?? string.Empty),
    ];

    internal static bool IsExported(AppItemViewModel app) => !PackageSearchItem.Is(app.Id) || app.IsInstalled;

    // The tray can call this before the page has ever loaded, and the saved filter read on first load must not
    // replace the one set here.
    public void ShowAppUpdates()
    {
        EnsureSubscriptions();
        IsExternalAppsTabSelected = true;
        SetFilter(AppFilter.UpdatesAvailable);
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
