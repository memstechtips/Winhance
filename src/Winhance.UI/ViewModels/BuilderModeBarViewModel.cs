using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.ViewModels;

public partial class BuilderModeBarViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IApplicationModeService _applicationModeService;
    private readonly IBuilderSaveService _builderSave;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService _dialogService;
    private readonly IHardwareFilterService _hardwareFilter;
    private readonly ITargetWindowsService _targetWindows;
    private readonly ICatalogScopeProvider _scope;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly ILogService _logService;

    [ObservableProperty]
    public partial bool IsBuilderActive { get; set; }

    public bool IsConfigTarget => _applicationModeService.CurrentBuilderTarget == BuilderTarget.Config;
    public bool IsAutounattendTarget => _applicationModeService.CurrentBuilderTarget == BuilderTarget.Autounattend;

    public string BuilderModeTitleText =>
        _localizationService.GetStringOrDefault("Builder_Mode_Title", "Builder Mode");

    public string BuilderModeDescriptionText =>
        _localizationService.GetStringOrDefault("Builder_Mode_Description", "Build a config or answer file from your choices. Nothing changes on this PC.");

    public string BuilderConfigTargetText =>
        _localizationService.GetStringOrDefault("Builder_Mode_Target_Config", "Config / Profile");

    public string BuilderAutounattendTargetText =>
        _localizationService.GetStringOrDefault("Builder_Mode_Target_Autounattend", "Autounattend");

    public bool ShowOtherHardware => !_hardwareFilter.IsFilterEnabled;

    public string ShowOtherHardwareText => _localizationService.GetString("Builder_Mode_ShowOtherHardware");

    public string ShowOtherHardwareTooltip => _localizationService.GetString("Builder_Mode_ShowOtherHardware_Tooltip");

    public IReadOnlyList<WindowsRelease> TargetReleases => WindowsReleases.All;

    public int? TargetBuild => _targetWindows.Build;

    public string TargetWindowsText => NameOf(_targetWindows.Build);

    public string TargetWindowsTooltip => _localizationService.GetString("Builder_Mode_TargetWindows_Tooltip");

    public string AllVersionsText => _localizationService.GetString("Builder_Mode_TargetWindows_AllVersions");

    public string BuilderSaveButtonText =>
        IsAutounattendTarget
            ? (_localizationService.GetStringOrDefault("Builder_Mode_Save_Autounattend", "Save autounattend.xml"))
            : _localizationService.GetString("Builder_Mode_Save");

    public string SaveAsConfigText => _localizationService.GetString("Builder_Mode_Save_AsConfig");

    public string SaveAsProfileText => _localizationService.GetString("Builder_Mode_Save_AsProfile");

    public string BuilderCancelButtonText =>
        _localizationService.GetStringOrDefault("Button_Cancel", "Cancel");

    public BuilderModeBarViewModel(
        IApplicationModeService applicationModeService,
        IBuilderSaveService builderSave,
        IDispatcherService dispatcherService,
        ILocalizationService localizationService,
        IDialogService dialogService,
        IHardwareFilterService hardwareFilter,
        ITargetWindowsService targetWindows,
        ICatalogScopeProvider scope,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        ILogService logService)
    {
        _applicationModeService = applicationModeService;
        _builderSave = builderSave;
        _dispatcherService = dispatcherService;
        _localizationService = localizationService;
        _dialogService = dialogService;
        _hardwareFilter = hardwareFilter;
        _targetWindows = targetWindows;
        _scope = scope;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _logService = logService;

        _applicationModeService.ModeChanged += OnModeChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;
        _hardwareFilter.FilterStateChanged += OnHardwareFilterChanged;
        _targetWindows.Changed += OnTargetWindowsChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _applicationModeService.ModeChanged -= OnModeChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _hardwareFilter.FilterStateChanged -= OnHardwareFilterChanged;
        _targetWindows.Changed -= OnTargetWindowsChanged;
        GC.SuppressFinalize(this);
    }

    private void OnModeChanged(object? sender, EventArgs e)
    {
        _dispatcherService.RunOnUIThreadAsync(() =>
        {
            IsBuilderActive = _applicationModeService.CurrentMode == WinhanceMode.Builder;
            OnPropertyChanged(nameof(IsConfigTarget));
            OnPropertyChanged(nameof(IsAutounattendTarget));
            OnPropertyChanged(nameof(BuilderSaveButtonText));
            return Task.CompletedTask;
        }).FireAndForget(_logService);
    }

    private void OnHardwareFilterChanged(object? sender, bool e)
    {
        _dispatcherService.RunOnUIThreadAsync(() =>
        {
            OnPropertyChanged(nameof(ShowOtherHardware));
            return Task.CompletedTask;
        }).FireAndForget(_logService);
    }

    private void OnTargetWindowsChanged(object? sender, EventArgs e)
    {
        _dispatcherService.RunOnUIThreadAsync(() =>
        {
            OnPropertyChanged(nameof(TargetBuild));
            OnPropertyChanged(nameof(TargetWindowsText));
            return Task.CompletedTask;
        }).FireAndForget(_logService);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(BuilderModeTitleText));
        OnPropertyChanged(nameof(BuilderModeDescriptionText));
        OnPropertyChanged(nameof(BuilderConfigTargetText));
        OnPropertyChanged(nameof(BuilderAutounattendTargetText));
        OnPropertyChanged(nameof(BuilderSaveButtonText));
        OnPropertyChanged(nameof(SaveAsConfigText));
        OnPropertyChanged(nameof(SaveAsProfileText));
        OnPropertyChanged(nameof(BuilderCancelButtonText));
        OnPropertyChanged(nameof(ShowOtherHardwareText));
        OnPropertyChanged(nameof(ShowOtherHardwareTooltip));
        OnPropertyChanged(nameof(TargetWindowsText));
        OnPropertyChanged(nameof(TargetWindowsTooltip));
        OnPropertyChanged(nameof(AllVersionsText));
    }

    public async Task SetShowOtherHardwareAsync(bool show)
    {
        if (!show && !await ConfirmNarrowingAsync(
                _scope.Current with { IncludeOtherHardware = false },
                _localizationService.GetString("Dialog_NarrowHardware_Title"),
                count => _localizationService.GetString("Dialog_NarrowHardware_Message", count)))
        {
            // IsChecked binds OneWay, so nothing has moved the checkbox back off the state the user declined.
            OnPropertyChanged(nameof(ShowOtherHardware));
            return;
        }

        await _hardwareFilter.SetAsync(!show);
    }

    public async Task SetTargetWindowsAsync(int? build)
    {
        if (build is { } narrowedTo && !await ConfirmNarrowingAsync(
                _scope.Current with { IncludeOtherOsVersions = false, TargetBuild = WindowsReleases.TargetOf(narrowedTo) },
                _localizationService.GetString("Dialog_NarrowWindows_Title"),
                count => _localizationService.GetString("Dialog_NarrowWindows_Message", count, NameOf(narrowedTo))))
        {
            OnPropertyChanged(nameof(TargetBuild));
            return;
        }

        await _targetWindows.SetAsync(build);
    }

    private string NameOf(int? build) => build switch
    {
        null => AllVersionsText,
        { } b when WindowsReleases.ForBuild(b) is { } release => release.Name,
        { } b => _localizationService.GetString("Builder_Mode_TargetWindows_Build", b),
    };

    // The edits survive the narrowing either way - the writers just resolve the catalog under the narrowed
    // scope at Save, so anything absent from it silently misses the file.
    private async Task<bool> ConfirmNarrowingAsync(CatalogScope narrowed, string title, Func<int, string> message)
    {
        int outOfScope = _applicationModeService.GetBuilderEdits()
            .Count(edit => _catalogSettingsRegistry.GetById(edit.SettingId, narrowed) is null);
        if (outOfScope == 0) return true;

        var request = new ConfirmationRequest
        {
            Title = title,
            Message = message(outOfScope),
        };

        return (await _dialogService.ShowConfirmationAsync(request)).Confirmed;
    }

    public void SelectConfigTarget()
    {
        _applicationModeService.SetBuilderTarget(BuilderTarget.Config);
        OnPropertyChanged(nameof(IsConfigTarget));
        OnPropertyChanged(nameof(IsAutounattendTarget));
        OnPropertyChanged(nameof(BuilderSaveButtonText));
    }

    public void SelectAutounattendTarget()
    {
        _applicationModeService.SetBuilderTarget(BuilderTarget.Autounattend);
        OnPropertyChanged(nameof(IsConfigTarget));
        OnPropertyChanged(nameof(IsAutounattendTarget));
        OnPropertyChanged(nameof(BuilderSaveButtonText));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await _builderSave.SaveAsync(_applicationModeService.CurrentBuilderTarget);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Builder Save failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private Task SaveAsConfigAsync() => _builderSave.SaveAsync(BuilderTarget.Config);

    [RelayCommand]
    private Task SaveAsProfileAsync() => _builderSave.SaveAsProfileAsync();

    [RelayCommand]
    private async Task CancelAsync()
    {
        var title = _localizationService.GetStringOrDefault("Builder_Mode_Cancel_Confirmation_Title", "Leave Builder Mode");
        var message = _localizationService.GetStringOrDefault("Builder_Mode_Cancel_Confirmation", "Leave Builder mode? Your selections here will be discarded. Nothing was applied to your system.");

        var confirmed = (await _dialogService.ShowConfirmationAsync(
            new ConfirmationRequest
            {
                Message = message,
                Title = title,
                ConfirmButtonText = _localizationService.GetString("Button_Leave"),
                CancelButtonText = _localizationService.GetString("Button_Stay"),
            })).Confirmed;
        if (confirmed)
        {
            _applicationModeService.EnterNormalMode();
        }
    }
}
