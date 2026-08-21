using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using ISettingsLoadingService = Winhance.UI.Features.Common.Interfaces.ISettingsLoadingService;

namespace Winhance.UI.Features.Optimize.ViewModels;

public abstract partial class BaseSettingsFeatureViewModel : BaseViewModel, ISettingsFeatureViewModel
{
    protected readonly ISettingsLoadingService _settingsLoadingService;
    protected readonly ILogService _logService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IDispatcherService _dispatcherService;
    protected readonly IEventBus _eventBus;
    protected readonly IApplicationModeService _applicationModeService;

    private bool _settingsLoaded = false;
    private bool _isSubscribed = false;
    private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
    private CancellationTokenSource? _searchDebounceTokenSource;
    private ISubscriptionToken? _settingAppliedSubscription;
    private ISubscriptionToken? _filterStateChangedSubscription;
    private ISubscriptionToken? _reviewModeExitedSubscription;
    private ISubscriptionToken? _authoringModeExitedSubscription;
    private ISubscriptionToken? _builderSeededSubscription;
    private volatile Dictionary<string, SettingItemViewModel> _settingsById = new();
    private volatile Dictionary<string, List<SettingItemViewModel>> _childrenByParentId = new();

    // Related-card refresh coalescing: a burst of relationship applies is drained once by a ~300ms
    // UI-thread debounce timer. The pending set is guarded (mutated off the UI thread in QueueRelatedRefresh,
    // drained on the UI thread in OnRelatedRefreshTick); the timer is created and driven only on the UI thread.
    private readonly object _relatedRefreshLock = new();
    private readonly HashSet<SettingItemViewModel> _pendingRelatedRefresh = new();
    private DispatcherQueueTimer? _relatedRefreshTimer;

    [ObservableProperty]
    public partial ObservableCollection<SettingItemViewModel> Settings { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<SettingsGroup> GroupedSettings { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

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

    public IRelayCommand LoadSettingsCommand { get; }
    public IRelayCommand ToggleExpandCommand { get; }

    protected BaseSettingsFeatureViewModel(
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus,
        IApplicationModeService applicationModeService)
    {
        _settingsLoadingService = settingsLoadingService;
        _logService = logService;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;
        _eventBus = eventBus;
        _applicationModeService = applicationModeService;

        Settings = new ObservableCollection<SettingItemViewModel>();
        GroupedSettings = new ObservableCollection<SettingsGroup>();
        IsExpanded = true;
        SearchText = string.Empty;

        LoadSettingsCommand = new RelayCommand(() => LoadSettingsAsync().FireAndForget(_logService));
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    // Called from LoadSettingsAsync on first load, not the constructor, to avoid side effects during DI construction.
    private void SubscribeToEvents()
    {
        if (_isSubscribed) return;
        _isSubscribed = true;

        _localizationService.LanguageChanged += OnLanguageChanged;
        _settingAppliedSubscription = _eventBus.Subscribe<SettingAppliedEvent>(OnSettingApplied);
        _filterStateChangedSubscription = _eventBus.SubscribeAsync<FilterStateChangedEvent>(OnFilterStateChangedAsync);
        _reviewModeExitedSubscription = _eventBus.Subscribe<ReviewModeExitedEvent>(OnReviewModeExited);
        _authoringModeExitedSubscription = _eventBus.SubscribeAsync<AuthoringModeExitedEvent>(OnAuthoringModeExitedAsync);
        _builderSeededSubscription = _eventBus.Subscribe<BuilderSeededEvent>(OnBuilderSeeded);
    }

    private void OnSettingApplied(SettingAppliedEvent evt)
    {
        // Exact-id verbatim path: when the applied setting lives on THIS feature, update its own card
        // from the event payload without a re-read. The master-ads flow relies on this. The gate pass that
        // follows is deliberately over the WHOLE list rather than this setting's children - the applied
        // card may be the TARGET of a gate declared by something that is not nested under it at all.
        if (_settingsById.TryGetValue(evt.SettingId, out var setting))
        {
            _dispatcherService.RunOnUIThread(() =>
            {
                setting.UpdateStateFromEvent(evt.IsEnabled, evt.Value);
                RefreshDeclaredGates();
            });
        }

        // Related-card refresh: re-detect any loaded VM that shares a registry surface with, or is in the UiParent
        // family of, the applied setting - even when the applied setting lives on a DIFFERENT feature (so this runs
        // regardless of the exact-id lookup above). Re-detection publishes no events, so there is no feedback loop.
        QueueRelatedRefresh(evt.SettingId);
    }

    // Coalesces re-detection of the settings related to an applied one. The applied setting is resolved from the
    // static catalog (alias-normalized like CatalogSettingsRegistry) so a cross-feature apply still finds its
    // relations. A gesture can publish 1 root + N relationship-leaf events, so the pending VM set is drained once
    // by the ~300ms debounce timer rather than per event.
    private void QueueRelatedRefresh(string appliedSettingId)
    {
        var applied = SettingCatalog.ById.TryGetValue(SettingIdAliases.Normalize(appliedSettingId), out var a) ? a : null;
        if (applied is null)
            return;

        var byId = _settingsById; // volatile snapshot; the dictionary is swapped wholesale, never mutated in place
        var related = new List<SettingItemViewModel>();
        foreach (var vm in byId.Values)
        {
            var candidate = vm.Setting;
            if (candidate is null || candidate.Id == applied.Id)
                continue;
            if (SharesRegistrySurface(applied, candidate) || IsUiParentFamily(applied, candidate))
                related.Add(vm);
        }

        if (related.Count == 0)
            return;

        lock (_relatedRefreshLock)
        {
            foreach (var vm in related)
                _pendingRelatedRefresh.Add(vm);
        }

        _dispatcherService.RunOnUIThread(ArmRelatedRefreshTimer);
    }

    // UI thread. Creates the debounce timer lazily (its Tick fires on the UI thread) and restarts the ~300ms
    // interval on every event, so a burst of relationship-leaf applies re-detects the union exactly once.
    private void ArmRelatedRefreshTimer()
    {
        if (_relatedRefreshTimer is null)
        {
            var queue = DispatcherQueue.GetForCurrentThread();
            if (queue is null)
                return; // no UI dispatcher on this thread - cannot schedule (never happens on the real UI thread)
            _relatedRefreshTimer = queue.CreateTimer();
            _relatedRefreshTimer.Interval = TimeSpan.FromMilliseconds(300);
            _relatedRefreshTimer.IsRepeating = false;
            _relatedRefreshTimer.Tick += (_, _) => OnRelatedRefreshTick();
        }
        _relatedRefreshTimer.Stop();
        _relatedRefreshTimer.Start();
    }

    // UI thread (timer Tick). Drains the pending set and re-detects it as one subset. Respects the same Builder-mode
    // guard as RefreshSettingStatesAsync: Builder authors un-applied state, so a live re-read there would clobber it.
    private void OnRelatedRefreshTick()
    {
        List<SettingItemViewModel> subset;
        lock (_relatedRefreshLock)
        {
            subset = _pendingRelatedRefresh.ToList();
            _pendingRelatedRefresh.Clear();
        }

        if (subset.Count == 0 || _applicationModeService.Capabilities().AuthorsIntent)
            return;

        RefreshRelatedStatesAsync(subset).FireAndForget(_logService);
    }

    private async Task RefreshRelatedStatesAsync(IReadOnlyList<SettingItemViewModel> subset)
    {
        try
        {
            var states = await _settingsLoadingService.RefreshSettingStatesAsync(subset);

            _dispatcherService.RunOnUIThread(() =>
            {
                foreach (var vm in subset)
                {
                    if (states.TryGetValue(vm.SettingId, out var state))
                        vm.UpdateStateFromSystemState(state);
                }

                RefreshDeclaredGates();
            });
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning, $"[{GetType().Name}] Related-card refresh failed: {ex.Message}");
        }
    }

    // Registry-surface overlap: the applied setting WROTE some (path, valueName) pairs (its ApplyOnly targets
    // are included - they are written), and the candidate READS an overlapping pair, so the candidate's card is now
    // stale. The candidate side EXCLUDES its own ApplyOnly targets - written on apply but never read on detect, so
    // overlap through them cannot stale the card. Every entry of Paths (a mirror list) is compared,
    // case-insensitive on path and valueName.
    private static bool SharesRegistrySurface(Setting applied, Setting candidate)
    {
        var appliedPairs = RegistrySurfacePairs(applied, includeApplyOnly: true);
        if (appliedPairs.Count == 0)
            return false;
        foreach (var pair in RegistrySurfacePairs(candidate, includeApplyOnly: false))
            if (appliedPairs.Contains(pair))
                return true;
        return false;
    }

    // A null valueName (key-existence) is its own token so it only matches another null, never a named value.
    private const string KeyExistenceToken = "\0__keyexists__";

    private static HashSet<(string, string)> RegistrySurfacePairs(Setting setting, bool includeApplyOnly)
    {
        var pairs = new HashSet<(string, string)>();
        foreach (var reg in setting.Targets.OfType<RegTarget>())
        {
            if (!includeApplyOnly && reg.ApplyOnly)
                continue;
            var value = reg.ValueName is null ? KeyExistenceToken : reg.ValueName.ToLowerInvariant();
            foreach (var path in reg.Paths)
                pairs.Add((path.ToLowerInvariant(), value));
        }
        return pairs;
    }

    private static bool IsUiParentFamily(Setting applied, Setting candidate)
    {
        if (!string.IsNullOrEmpty(applied.UiParentId)
            && (candidate.UiParentId == applied.UiParentId || candidate.Id == applied.UiParentId))
            return true;
        if (!string.IsNullOrEmpty(candidate.UiParentId) && candidate.UiParentId == applied.Id)
            return true;
        return false;
    }

    protected abstract string GetDisplayNameKey();

    private string GetDisplayName()
    {
        var key = GetDisplayNameKey();
        return _localizationService.GetString(key);
    }

    private async void OnLanguageChanged(object? sender, EventArgs e)
    {
        try
        {
            _settingsLoaded = false;

            OnPropertyChanged(nameof(DisplayName));
            await LoadSettingsAsync();

            // Notify pages that settings were recreated so they can re-apply view state (badges, etc.)
            _eventBus.Publish(new SettingsRefreshedEvent(DisplayName));
        }
        catch (Exception ex)
        {
            _logService.LogDebug($"[{DisplayName}] Error handling language change: {ex.Message}");
        }
    }

    private async Task OnFilterStateChangedAsync(FilterStateChangedEvent e)
    {
        await ApplyFilterScopeChangeAsync();
    }

    private void OnReviewModeExited(ReviewModeExitedEvent e)
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            foreach (var setting in Settings)
            {
                setting.ClearReviewState();
            }
        });
    }

    private async Task OnAuthoringModeExitedAsync(AuthoringModeExitedEvent e)
    {
        // The mode we just left moved the toggles to authored, un-applied positions on the shared
        // VMs. Reload from live system state so what is on screen is the truth again. Only touch
        // features that are actually loaded — unopened ones read fresh system state on first open.
        //
        // A total rebuild rather than a per-field reset, and that is why this side needs no overlay
        // the way review state does: the ViewModels are disposed and recreated, so a newly added
        // field cannot survive the transition. There is no cleanup list here to fall out of date.
        if (Settings?.Any() != true) return;
        await ReloadAllSettingsAsync();
    }

    // The seed records its edits after these cards were built from live state, so their toggles still show the
    // machine. Cards built after the seed pick it up in SettingViewModelFactory instead.
    private void OnBuilderSeeded(BuilderSeededEvent e)
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            foreach (var setting in Settings)
            {
                setting.ApplyAuthoredOverlay();
                setting.ComputeBadgeState();
                setting.UpdateDetectionOutcomeBanner();
                setting.RefreshTechnicalDetails();
            }
        });
    }

    private async Task ReloadAllSettingsAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, $"Reloading every setting for {DisplayName}");

            _settingsLoaded = false;

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

            // Notify pages that settings were recreated so they can re-apply view state (badges, etc.)
            _eventBus.Publish(new SettingsRefreshedEvent(DisplayName));
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error reloading settings for {DisplayName}: {ex.Message}");
        }
    }

    // Changes only what the scope change actually moved. A hardware-gate flip alters membership for nine
    // settings, all of them in Power, so nine of the ten features reconcile to "nothing moved" and never
    // touch the visual tree - which is where the saving is, since the cost the user sees is the WinUI layout
    // pass, not the managed work. Authoring exit deliberately does NOT come through here; see
    // OnAuthoringModeExitedAsync for why that one still tears everything down.
    private async Task ApplyFilterScopeChangeAsync()
    {
        // An unopened feature reads fresh state when it is first opened, so there is nothing to reconcile.
        if (!_settingsLoaded || Settings is null || Settings.Count == 0)
            return;

        try
        {
            var membership = _settingsLoadingService.GetFeatureSettingIds(ModuleId);
            var currentIds = new HashSet<string>(membership, StringComparer.Ordinal);
            var byId = _settingsById; // volatile snapshot; the dictionary is swapped wholesale, never mutated in place

            var arrivedIds = new List<string>();
            foreach (var id in membership)
            {
                if (!byId.ContainsKey(id))
                    arrivedIds.Add(id);
            }

            var departed = new List<SettingItemViewModel>();
            var survivors = new List<SettingItemViewModel>();
            foreach (var setting in Settings)
            {
                if (currentIds.Contains(setting.SettingId))
                    survivors.Add(setting);
                else
                    departed.Add(setting);
            }

            foreach (var setting in departed)
            {
                Settings.Remove(setting);
                setting.Dispose();
            }

            IReadOnlyList<SettingItemViewModel> arrived = arrivedIds.Count > 0
                ? await _settingsLoadingService.LoadSettingsSubsetAsync(ModuleId, arrivedIds, this)
                : Array.Empty<SettingItemViewModel>();

            InsertInCatalogOrder(membership, arrived);

            await _settingsLoadingService.RefreshScopeDerivedStateAsync(survivors);

            RebuildSettingIndexes();

            if (arrived.Count == 0 && departed.Count == 0)
                return;

            RebuildGroupedSettings();

            OnPropertyChanged(nameof(HasVisibleSettings));
            OnPropertyChanged(nameof(IsVisibleInSearch));
            OnPropertyChanged(nameof(SettingsCount));
            OnPropertyChanged(nameof(GroupDescriptionText));

            _logService.Log(LogLevel.Info, $"Scope change moved {arrived.Count} in and {departed.Count} out of {DisplayName}");

            // Fresh cards carry default badge/technical-details visibility, so the page has to re-apply the
            // View-menu state the same way it does after a full rebuild.
            _eventBus.Publish(new SettingsRefreshedEvent(DisplayName));
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error applying scope change for {DisplayName}: {ex.Message}");
        }
    }

    // Settings holds the survivors in catalog order and membership is that same order with the arrivals back
    // in, so walking the two together lands each new card exactly where a full load would have put it.
    // RebuildGroupedSettings groups by first-seen order, so an insert in the wrong place reorders the groups.
    private void InsertInCatalogOrder(IReadOnlyList<string> membership, IReadOnlyList<SettingItemViewModel> arrived)
    {
        if (arrived.Count == 0)
            return;

        var arrivedById = arrived.ToDictionary(setting => setting.SettingId, StringComparer.Ordinal);
        int index = 0;
        foreach (var id in membership)
        {
            if (index < Settings.Count && string.Equals(Settings[index].SettingId, id, StringComparison.Ordinal))
                index++;
            else if (arrivedById.TryGetValue(id, out var setting))
                Settings.Insert(index++, setting);
        }
    }

    public void ApplySearchFilter(string searchText)
    {
        SearchText = searchText ?? string.Empty;
    }

    partial void OnSearchTextChanged(string value)
    {
        var newCts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _searchDebounceTokenSource, newCts);
        oldCts?.Cancel();
        oldCts?.Dispose();
        var token = newCts.Token;

        Task.Run(async () =>
        {
            try
            {
                token.ThrowIfCancellationRequested();

                bool featureMatches = string.IsNullOrWhiteSpace(value) ||
                    DisplayName.Contains(value, StringComparison.OrdinalIgnoreCase);

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

                        foreach (var kvp in _childrenByParentId)
                        {
                            if (_settingsById.TryGetValue(kvp.Key, out var parent) && parent.IsVisible)
                            {
                                foreach (var child in kvp.Value)
                                    child.IsVisible = true;
                            }
                        }

                        foreach (var kvp in _childrenByParentId)
                        {
                            if (kvp.Value.Any(c => c.IsVisible))
                            {
                                if (_settingsById.TryGetValue(kvp.Key, out var parent))
                                    parent.IsVisible = true;
                            }
                        }
                    }

                    OnPropertyChanged(nameof(HasVisibleSettings));
                    OnPropertyChanged(nameof(IsVisibleInSearch));
                });
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    public virtual async Task LoadSettingsAsync()
    {
        SubscribeToEvents();

        // SemaphoreSlim is async-safe. WaitAsync(0) returns false immediately
        // if already held, preventing duplicate concurrent loads.
        if (!await _loadingSemaphore.WaitAsync(0))
            return;

        try
        {
            if (_settingsLoaded)
                return;

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
                ModuleId,
                $"Loading {DisplayName} settings...",
                this
            );

            Settings = loadedSettings;

            RebuildSettingIndexes();
            RebuildGroupedSettings();

            OnPropertyChanged(nameof(HasVisibleSettings));
            OnPropertyChanged(nameof(IsVisibleInSearch));
            OnPropertyChanged(nameof(SettingsCount));
            OnPropertyChanged(nameof(GroupDescriptionText));

            _settingsLoaded = true;
            _logService.Log(LogLevel.Info, $"{GetType().Name}: Successfully loaded {Settings.Count} settings, HasVisibleSettings={HasVisibleSettings}");
        }
        catch (Exception ex)
        {
            _settingsLoaded = false;
            _logService.Log(LogLevel.Error, $"Error loading {DisplayName} settings: {ex.Message}");
            throw;
        }
        finally
        {
            IsLoading = false;
            _loadingSemaphore.Release();
        }
    }

    public virtual async Task RefreshSettingsAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, $"Refreshing settings for {DisplayName}");

            _settingsLoaded = false;

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

            // Rebuilding the list creates fresh SettingItemViewModels whose badge/technical-details
            // visibility defaults are not the user's current View-menu state. Publish the same event
            // the language- and filter-change rebuild paths do so the page re-applies that state
            // (otherwise Info badges silently disappear until the user re-toggles them).
            _eventBus.Publish(new SettingsRefreshedEvent(DisplayName));
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

        // Builder mode authors un-applied state into these ViewModels. The section pages
        // call this on every navigation to keep Normal mode truthful, but re-reading the
        // system here would clobber the authored Builder values — skip until Builder exit,
        // which reloads from live state anyway (AuthoringModeExitedEvent).
        if (_applicationModeService.Capabilities().AuthorsIntent)
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

                RefreshDeclaredGates();
            });
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning, $"[{GetType().Name}] Error refreshing setting states: {ex.Message}");
        }
    }

    // THE PRESENTATION GATE - one implementation, four callers.
    //
    // A card is greyed only when its catalog DECLARES an EnabledWhen and the setting that names is
    // currently outside the declared states. Nesting under a UiParentId does not gate anything:
    // "nested under" and "meaningless unless" are different facts, and only the setting's author knows
    // the second one. Do not gate on the parent's selected INDEX being non-zero either - that greyed both
    // Windows-theme sub-toggles on every stock Windows 11 install, because "Light Mode" happens to be state 0.
    //
    // Every path that can move a card's state in NORMAL mode ends here: the initial load, an apply
    // event, the navigation refresh (RefreshSettingStatesAsync) and the related-refresh debounce.
    // Recomputing the whole list costs one pass over a few dozen cards, so there is no reason to work
    // out which cards could have changed.
    //
    // Builder mode is the one gap: it authors un-applied state with no
    // SettingAppliedEvent, and both refresh paths deliberately skip it so a live re-read cannot clobber
    // what the user is authoring - so a gate holds its load-time verdict until Builder exit, which
    // reloads from live state anyway.
    private void RefreshDeclaredGates()
    {
        if (Settings is null)
            return;

        foreach (var setting in Settings)
            setting.ParentIsEnabled = IsGateSatisfied(setting);
    }

    // TRUE (usable) unless the card declares an EnabledWhen whose named setting is loaded here, has a state we can
    // name, and that state is NOT one of the declared ones. The two "not gated" answers are deliberate: a setting
    // this feature has not loaded cannot be read at all, and a card whose own state does not resolve is not
    // evidence that anything else is meaningless - so neither is grounds for taking a control away. A gate is a
    // positive claim, only made when it can actually be checked.
    private bool IsGateSatisfied(SettingItemViewModel item)
    {
        if (item.Setting?.EnabledWhen is not { } gate)
            return true;

        if (!_settingsById.TryGetValue(gate.OtherId, out var other))
            return true;

        if (other.CurrentStateLabel is not { } label)
            return true;

        return gate.States.Contains(label, StringComparer.Ordinal);
    }

    // Every per-card index this feature holds is derived from Settings, and nothing else holds per-card
    // state - which is what lets the filter path mutate Settings in place and then just re-run this. The
    // dictionaries are built whole and swapped by reference, so a reader on another thread
    // (OnSettingApplied) sees either the old complete one or the new complete one, never a partial build.
    private void RebuildSettingIndexes()
    {
        var newSettingsById = new Dictionary<string, SettingItemViewModel>();
        var newChildrenByParentId = new Dictionary<string, List<SettingItemViewModel>>();
        foreach (var setting in Settings)
        {
            if (!string.IsNullOrEmpty(setting.SettingId))
                newSettingsById[setting.SettingId] = setting;

            var parentId = setting.EffectiveUiParentId;
            if (!string.IsNullOrEmpty(parentId))
            {
                if (!newChildrenByParentId.TryGetValue(parentId, out var children))
                {
                    children = new List<SettingItemViewModel>();
                    newChildrenByParentId[parentId] = children;
                }
                children.Add(setting);
            }
        }
        _settingsById = newSettingsById;
        _childrenByParentId = newChildrenByParentId;

        foreach (var kvp in newChildrenByParentId)
        {
            if (newSettingsById.TryGetValue(kvp.Key, out var parentVm))
            {
                var childList = kvp.Value;
                for (int i = 0; i < childList.Count; i++)
                    childList[i].IsLastChild = i == childList.Count - 1;

                // Replacing Children rebinds the parent's expander, so only do it when the child set moved.
                if (parentVm.Children is null || !parentVm.Children.SequenceEqual(childList))
                    parentVm.Children = new ObservableCollection<SettingItemViewModel>(childList);
            }
        }

        RefreshDeclaredGates();
    }

    private void RebuildGroupedSettings()
    {
        GroupedSettings.Clear();

        if (Settings == null || Settings.Count == 0)
            return;

        var otherGroupName = _localizationService.TryGetString("SettingGroup_Other", out var localizedOther)
            ? localizedOther
            : "Other";

        var groupOrder = new List<string>();
        var groupedDict = new Dictionary<string, List<SettingItemViewModel>>();

        foreach (var setting in Settings)
        {
            // Children render inside their parent's SettingsExpander, not in the flat list
            if (setting.IsSubSetting)
                continue;

            var groupName = string.IsNullOrEmpty(setting.GroupName) ? otherGroupName : setting.GroupName;

            if (!groupedDict.TryGetValue(groupName, out var groupSettings))
            {
                groupOrder.Add(groupName);
                groupSettings = new List<SettingItemViewModel>();
                groupedDict[groupName] = groupSettings;
            }

            groupSettings.Add(setting);
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

            _filterStateChangedSubscription?.Dispose();
            _filterStateChangedSubscription = null;

            _reviewModeExitedSubscription?.Dispose();
            _reviewModeExitedSubscription = null;

            _authoringModeExitedSubscription?.Dispose();
            _authoringModeExitedSubscription = null;

            _builderSeededSubscription?.Dispose();
            _builderSeededSubscription = null;

            _localizationService.LanguageChanged -= OnLanguageChanged;

            _relatedRefreshTimer?.Stop();
            _relatedRefreshTimer = null;

            if (Settings != null)
            {
                foreach (var setting in Settings.OfType<IDisposable>())
                {
                    setting?.Dispose();
                }
                Settings.Clear();
            }

            var cts = Interlocked.Exchange(ref _searchDebounceTokenSource, null);
            cts?.Cancel();
            cts?.Dispose();

            _settingsById = new Dictionary<string, SettingItemViewModel>();
            _childrenByParentId = new Dictionary<string, List<SettingItemViewModel>>();
            _settingsLoaded = false;
            _loadingSemaphore.Dispose();
        }

        base.Dispose(disposing);
    }
}
