using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.ViewModels;
using ISettingsLoadingService = Winhance.UI.Features.Common.Interfaces.ISettingsLoadingService;

namespace Winhance.UI.Features.Optimize.ViewModels;

public abstract partial class BaseSettingsFeatureViewModel : BaseViewModel, ISettingsFeatureViewModel
{
    protected readonly IDomainServiceRouter _domainServiceRouter;
    protected readonly ISettingsLoadingService _settingsLoadingService;
    protected readonly ILogService _logService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IDispatcherService _dispatcherService;
    protected readonly MainWindowViewModel? _mainWindowViewModel;
    protected readonly IEventBus _eventBus;

    private bool _settingsLoaded = false;
    private readonly object _loadingLock = new();
    private CancellationTokenSource? _searchDebounceTokenSource;
    private ISubscriptionToken? _settingAppliedSubscription;
    private Dictionary<string, SettingItemViewModel> _settingsById = new();
    private Dictionary<string, List<SettingItemViewModel>> _childrenByParentId = new();

    [ObservableProperty]
    private ObservableCollection<SettingItemViewModel> _settings = new();

    [ObservableProperty]
    private ObservableCollection<SettingsGroup> _groupedSettings = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public abstract string ModuleId { get; }
    public virtual string DisplayName => GetDisplayName();
    public bool HasVisibleSettings => Settings.Any(s => s.IsVisible);
    public bool IsVisibleInSearch => HasVisibleSettings;
    public int SettingsCount => Settings?.Count ?? 0;

    public string GroupDescriptionText
    {
        get
        {
            if (Settings == null || Settings.Count == 0)
                return string.Empty;

            var groups = Settings
                .Where(s => !string.IsNullOrEmpty(s.GroupName))
                .Select(s => s.GroupName)
                .Distinct()
                .Take(4)
                .ToList();

            if (groups.Count == 0)
                return string.Empty;

            var totalGroups = Settings
                .Where(s => !string.IsNullOrEmpty(s.GroupName))
                .Select(s => s.GroupName)
                .Distinct()
                .Count();

            var text = string.Join(", ", groups);
            if (totalGroups > 4)
                text += ", ...";

            return text;
        }
    }

    public event EventHandler<FeatureVisibilityChangedEventArgs>? VisibilityChanged;

    public IRelayCommand LoadSettingsCommand { get; }
    public IRelayCommand ToggleExpandCommand { get; }

    protected BaseSettingsFeatureViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus,
        MainWindowViewModel? mainWindowViewModel = null)
    {
        _domainServiceRouter = domainServiceRouter ?? throw new ArgumentNullException(nameof(domainServiceRouter));
        _settingsLoadingService = settingsLoadingService ?? throw new ArgumentNullException(nameof(settingsLoadingService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _mainWindowViewModel = mainWindowViewModel;

        LoadSettingsCommand = new RelayCommand(() => _ = LoadSettingsAsync());
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);

        _localizationService.LanguageChanged += OnLanguageChanged;

        // Subscribe to setting applied events for cross-module dependency updates
        _settingAppliedSubscription = _eventBus.Subscribe<SettingAppliedEvent>(OnSettingApplied);

        if (_mainWindowViewModel != null)
        {
            _mainWindowViewModel.FilterStateChanged += OnFilterStateChanged;
        }
    }

    private void OnSettingApplied(SettingAppliedEvent evt)
    {
        if (!_settingsById.TryGetValue(evt.SettingId, out var setting))
            return;

        _dispatcherService.RunOnUIThread(() =>
        {
            setting.UpdateStateFromEvent(evt.IsEnabled, evt.Value);

            // Update children's ParentIsEnabled if this setting has any children
            if (_childrenByParentId.TryGetValue(evt.SettingId, out var children))
            {
                bool parentEnabled = setting.InputType switch
                {
                    InputType.Toggle => setting.IsSelected,
                    InputType.Selection => setting.SelectedValue is int index && index != 0,
                    _ => setting.IsSelected
                };

                foreach (var child in children)
                {
                    child.ParentIsEnabled = parentEnabled;
                }
            }
        });
    }

    protected abstract string GetDisplayNameKey();

    private string GetDisplayName()
    {
        var key = GetDisplayNameKey();
        return _localizationService.GetString(key);
    }

    private async void OnLanguageChanged(object? sender, EventArgs e)
    {
        lock (_loadingLock)
        {
            _settingsLoaded = false;
        }

        OnPropertyChanged(nameof(DisplayName));
        await LoadSettingsAsync();
    }

    private async void OnFilterStateChanged(object? sender, FilterStateChangedEventArgs e)
    {
        await RefreshSettingsForFilterChangeAsync();
    }

    private async Task RefreshSettingsForFilterChangeAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, $"Refreshing settings for {DisplayName} due to filter change");

            // Reset the loaded flag to allow reloading
            lock (_loadingLock)
            {
                _settingsLoaded = false;
            }

            // Clear and reload settings
            if (Settings?.Any() == true)
            {
                foreach (var setting in Settings.OfType<IDisposable>())
                {
                    setting?.Dispose();
                }
                Settings.Clear();
            }

            await LoadSettingsAsync();

            _logService.Log(LogLevel.Info, $"Successfully refreshed {Settings!.Count} settings for {DisplayName}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error refreshing settings for filter change: {ex.Message}");
        }
    }

    public void ApplySearchFilter(string searchText)
    {
        SearchText = searchText ?? string.Empty;
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceTokenSource?.Cancel();
        _searchDebounceTokenSource = new CancellationTokenSource();
        var token = _searchDebounceTokenSource.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                token.ThrowIfCancellationRequested();

                bool featureMatches = string.IsNullOrWhiteSpace(value) ||
                    DisplayName.ToLowerInvariant().Contains(value.ToLowerInvariant());

                _dispatcherService.RunOnUIThread(() =>
                {
                    if (featureMatches)
                    {
                        foreach (var setting in Settings)
                        {
                            setting.IsVisible = true;
                        }
                    }
                    else
                    {
                        foreach (var setting in Settings)
                        {
                            setting.UpdateVisibility(value);
                        }
                    }

                    OnPropertyChanged(nameof(HasVisibleSettings));
                    OnPropertyChanged(nameof(IsVisibleInSearch));
                    VisibilityChanged?.Invoke(this, new FeatureVisibilityChangedEventArgs(ModuleId, IsVisibleInSearch, value));
                });
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    public virtual async Task LoadSettingsAsync()
    {
        lock (_loadingLock)
        {
            if (_settingsLoaded)
                return;
            _settingsLoaded = true;
        }

        try
        {
            IsLoading = true;

            if (Settings?.Any() == true)
            {
                foreach (var setting in Settings.OfType<IDisposable>())
                {
                    setting?.Dispose();
                }
                Settings.Clear();
            }

            var loadedSettings = await _settingsLoadingService.LoadConfiguredSettingsAsync(
                _domainServiceRouter.GetDomainService(ModuleId),
                ModuleId,
                $"Loading {DisplayName} settings...",
                this
            );

            var settingViewModels = loadedSettings.Cast<SettingItemViewModel>().ToList();
            Settings = new ObservableCollection<SettingItemViewModel>(settingViewModels);

            // Build dictionaries for O(1) lookups
            _settingsById.Clear();
            _childrenByParentId.Clear();
            foreach (var setting in Settings)
            {
                if (!string.IsNullOrEmpty(setting.SettingId))
                    _settingsById[setting.SettingId] = setting;

                // Index children by their parent ID for fast lookup when parent changes
                var parentId = setting.SettingDefinition?.ParentSettingId;
                if (!string.IsNullOrEmpty(parentId))
                {
                    if (!_childrenByParentId.TryGetValue(parentId, out var children))
                    {
                        children = new List<SettingItemViewModel>();
                        _childrenByParentId[parentId] = children;
                    }
                    children.Add(setting);
                }
            }

            UpdateParentChildRelationships();
            RebuildGroupedSettings();

            OnPropertyChanged(nameof(HasVisibleSettings));
            OnPropertyChanged(nameof(IsVisibleInSearch));
            OnPropertyChanged(nameof(SettingsCount));
            OnPropertyChanged(nameof(GroupDescriptionText));

            _logService.Log(LogLevel.Info, $"{GetType().Name}: Successfully loaded {Settings.Count} settings, HasVisibleSettings={HasVisibleSettings}");
        }
        catch (Exception ex)
        {
            lock (_loadingLock)
            {
                _settingsLoaded = false;
            }
            _logService.Log(LogLevel.Error, $"Error loading {DisplayName} settings: {ex.Message}");
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public virtual async Task RefreshSettingsAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, $"Refreshing settings for {DisplayName}");

            lock (_loadingLock)
            {
                _settingsLoaded = false;
            }

            if (Settings?.Any() == true)
            {
                foreach (var setting in Settings.OfType<IDisposable>())
                {
                    setting?.Dispose();
                }
                Settings.Clear();
            }

            await LoadSettingsAsync();

            _logService.Log(LogLevel.Info, $"Successfully refreshed {Settings!.Count} settings for {DisplayName}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error refreshing settings: {ex.Message}");
        }
    }

    public virtual async Task RefreshSettingStatesAsync()
    {
        if (!_settingsLoaded || Settings == null || Settings.Count == 0)
            return;

        try
        {
            var states = await _settingsLoadingService.RefreshSettingStatesAsync(Settings);

            _dispatcherService.RunOnUIThread(() =>
            {
                foreach (var setting in Settings)
                {
                    if (states.TryGetValue(setting.SettingId, out var state))
                    {
                        setting.UpdateStateFromSystemState(state);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning, $"[{GetType().Name}] Error refreshing setting states: {ex.Message}");
        }
    }

    public virtual Task<bool> HandleDomainContextSettingAsync(SettingDefinition setting, object? value, bool additionalContext = false)
    {
        return Task.FromResult(false);
    }

    private void UpdateParentChildRelationships()
    {
        foreach (var setting in Settings)
        {
            if (!string.IsNullOrEmpty(setting.SettingDefinition?.ParentSettingId))
            {
                var parent = Settings.FirstOrDefault(s => s.SettingId == setting.SettingDefinition.ParentSettingId);
                if (parent != null)
                {
                    bool parentEnabled = parent.InputType switch
                    {
                        InputType.Toggle => parent.IsSelected,
                        InputType.Selection => parent.SelectedValue is int index && index != 0,
                        _ => parent.IsSelected
                    };

                    setting.ParentIsEnabled = parentEnabled;
                }
            }
        }
    }

    private void RebuildGroupedSettings()
    {
        GroupedSettings.Clear();

        if (Settings == null || Settings.Count == 0)
            return;

        var otherGroupName = _localizationService.GetString("SettingGroup_Other");
        if (otherGroupName.StartsWith("[") && otherGroupName.EndsWith("]"))
            otherGroupName = "Other";

        var groupOrder = new List<string>();
        var groupedDict = new Dictionary<string, List<SettingItemViewModel>>();

        foreach (var setting in Settings)
        {
            var groupName = string.IsNullOrEmpty(setting.GroupName) ? otherGroupName : setting.GroupName;

            if (!groupedDict.ContainsKey(groupName))
            {
                groupOrder.Add(groupName);
                groupedDict[groupName] = new List<SettingItemViewModel>();
            }

            groupedDict[groupName].Add(setting);
        }

        foreach (var groupName in groupOrder)
        {
            var group = new SettingsGroup(groupName, groupedDict[groupName]);
            GroupedSettings.Add(group);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _settingAppliedSubscription?.Dispose();
            _settingAppliedSubscription = null;

            _localizationService.LanguageChanged -= OnLanguageChanged;

            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.FilterStateChanged -= OnFilterStateChanged;
            }

            if (Settings != null)
            {
                foreach (var setting in Settings.OfType<IDisposable>())
                {
                    setting?.Dispose();
                }
                Settings.Clear();
            }

            _settingsById.Clear();
            _childrenByParentId.Clear();
            _settingsLoaded = false;
            VisibilityChanged = null;
        }

        base.Dispose(disposing);
    }
}
