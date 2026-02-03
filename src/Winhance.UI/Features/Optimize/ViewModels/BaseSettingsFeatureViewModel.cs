using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using ISettingsLoadingService = Winhance.UI.Features.Common.Interfaces.ISettingsLoadingService;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// Base class for feature ViewModels that display settings.
/// </summary>
public abstract partial class BaseSettingsFeatureViewModel : BaseViewModel, ISettingsFeatureViewModel
{
    protected readonly IDomainServiceRouter _domainServiceRouter;
    protected readonly ISettingsLoadingService _settingsLoadingService;
    protected readonly ILogService _logService;
    protected readonly ILocalizationService _localizationService;
    private bool _settingsLoaded = false;
    private readonly object _loadingLock = new();
    private CancellationTokenSource? _searchDebounceTokenSource;

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

    /// <summary>
    /// Module identifier for this feature.
    /// </summary>
    public abstract string ModuleId { get; }

    /// <summary>
    /// Display name for this feature.
    /// </summary>
    public virtual string DisplayName => GetDisplayName();

    /// <summary>
    /// Indicates whether this feature has any visible settings.
    /// </summary>
    public bool HasVisibleSettings => Settings.Any(s => s.IsVisible);

    /// <summary>
    /// Indicates whether this feature is visible in search results.
    /// </summary>
    public bool IsVisibleInSearch => HasVisibleSettings;

    /// <summary>
    /// Number of settings in this feature.
    /// </summary>
    public int SettingsCount => Settings?.Count ?? 0;

    /// <summary>
    /// Gets a comma-separated list of unique group names for display on overview cards.
    /// </summary>
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

    /// <summary>
    /// Event raised when feature visibility changes due to search.
    /// </summary>
    public event EventHandler<FeatureVisibilityChangedEventArgs>? VisibilityChanged;

    public IRelayCommand LoadSettingsCommand { get; }
    public IRelayCommand ToggleExpandCommand { get; }

    protected BaseSettingsFeatureViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService)
    {
        _domainServiceRouter = domainServiceRouter ?? throw new ArgumentNullException(nameof(domainServiceRouter));
        _settingsLoadingService = settingsLoadingService ?? throw new ArgumentNullException(nameof(settingsLoadingService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        LoadSettingsCommand = new RelayCommand(() => _ = LoadSettingsAsync());
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);

        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// Gets the localization key for the display name.
    /// </summary>
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

    /// <summary>
    /// Applies a search filter to all settings.
    /// </summary>
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
                await Task.Delay(100, token);

                bool featureMatches = string.IsNullOrWhiteSpace(value) ||
                    DisplayName.ToLowerInvariant().Contains(value.ToLowerInvariant());

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
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled, ignore
            }
        });
    }

    /// <summary>
    /// Loads all settings for this feature.
    /// </summary>
    public virtual async Task LoadSettingsAsync()
    {
        lock (_loadingLock)
        {
            if (_settingsLoaded)
            {
                return;
            }
            _settingsLoaded = true;
        }

        try
        {
            IsLoading = true;

            // Clear existing settings
            if (Settings?.Any() == true)
            {
                foreach (var setting in Settings.OfType<IDisposable>())
                {
                    setting?.Dispose();
                }
                Settings.Clear();
            }

            // Load settings using the settings loading service
            var loadedSettings = await _settingsLoadingService.LoadConfiguredSettingsAsync(
                _domainServiceRouter.GetDomainService(ModuleId),
                ModuleId,
                $"Loading {DisplayName} settings...",
                this
            );

            // Convert to our ViewModel type
            var settingViewModels = loadedSettings.Cast<SettingItemViewModel>().ToList();
            Settings = new ObservableCollection<SettingItemViewModel>(settingViewModels);

            UpdateParentChildRelationships();

            // Build the grouped settings collection
            RebuildGroupedSettings();

            // Notify that computed properties have changed
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

    /// <summary>
    /// Refreshes all settings.
    /// </summary>
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

            _logService.Log(LogLevel.Info, $"Successfully refreshed {Settings.Count} settings for {DisplayName}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error refreshing settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles domain-specific setting context changes.
    /// </summary>
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

    /// <summary>
    /// Rebuilds the grouped settings collection from the flat settings list.
    /// Groups settings by their GroupName property, preserving original order.
    /// Settings without a GroupName are placed in an "Other" group.
    /// </summary>
    private void RebuildGroupedSettings()
    {
        GroupedSettings.Clear();

        if (Settings == null || Settings.Count == 0)
            return;

        // Get localized "Other" group name
        var otherGroupName = _localizationService.GetString("SettingGroup_Other");
        if (otherGroupName.StartsWith("[") && otherGroupName.EndsWith("]"))
        {
            otherGroupName = "Other"; // Fallback if not localized
        }

        // Group settings by GroupName, preserving order of first occurrence
        var groupOrder = new List<string>();
        var groupedDict = new Dictionary<string, List<SettingItemViewModel>>();

        foreach (var setting in Settings)
        {
            // Use "Other" for settings without a group name
            var groupName = string.IsNullOrEmpty(setting.GroupName) ? otherGroupName : setting.GroupName;

            if (!groupedDict.ContainsKey(groupName))
            {
                groupOrder.Add(groupName);
                groupedDict[groupName] = new List<SettingItemViewModel>();
            }

            groupedDict[groupName].Add(setting);
        }

        // Build the grouped collection in order
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
            _localizationService.LanguageChanged -= OnLanguageChanged;

            if (Settings != null)
            {
                foreach (var setting in Settings.OfType<IDisposable>())
                {
                    setting?.Dispose();
                }
                Settings.Clear();
            }

            _settingsLoaded = false;
            VisibilityChanged = null;
        }

        base.Dispose(disposing);
    }
}
