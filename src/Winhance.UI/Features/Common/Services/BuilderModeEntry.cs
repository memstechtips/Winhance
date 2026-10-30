using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public sealed class BuilderModeEntry : IBuilderModeEntry
{
    private const string IntroDontShowKey = "BuilderModeIntroDontShow";

    private readonly IApplicationModeService _mode;
    private readonly IConfigurationService _configurationService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IBuilderSeedSource _builderSeedSource;
    private readonly ISelectionSetBuilder _selectionSetBuilder;
    private readonly ICatalogSettingsRegistry _registry;
    private readonly IConfigAppSelectionService _appSelection;
    private readonly IEventBus _eventBus;
    private readonly ILogService _logService;
    private readonly IFileStore _fileStore;
    private readonly IConfigLoadService _configLoadService;
    private readonly ITargetWindowsService _targetWindows;
    private readonly IFilePickerService _filePicker;
    private readonly IAutounattendReader _autounattendReader;
    private readonly IInstallOptionsService _installOptions;
    private bool _installOptionsOpen;

    public BuilderModeEntry(
        IApplicationModeService mode,
        IConfigurationService configurationService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IUserPreferencesService userPreferencesService,
        IBuilderSeedSource builderSeedSource,
        ISelectionSetBuilder selectionSetBuilder,
        ICatalogSettingsRegistry registry,
        IConfigAppSelectionService appSelection,
        IEventBus eventBus,
        ILogService logService,
        IFileStore fileStore,
        IConfigLoadService configLoadService,
        ITargetWindowsService targetWindows,
        IFilePickerService filePicker,
        IAutounattendReader autounattendReader,
        IInstallOptionsService installOptions)
    {
        _mode = mode;
        _configurationService = configurationService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;
        _builderSeedSource = builderSeedSource;
        _selectionSetBuilder = selectionSetBuilder;
        _registry = registry;
        _appSelection = appSelection;
        _eventBus = eventBus;
        _logService = logService;
        _fileStore = fileStore;
        _configLoadService = configLoadService;
        _targetWindows = targetWindows;
        _filePicker = filePicker;
        _autounattendReader = autounattendReader;
        _installOptions = installOptions;
        _mode.ModeChanged += OnModeChanged;
    }

    public async Task<bool> EnterAsync(BuilderTarget target)
    {
        if (!await ShowIntroIfNeededAsync())
        {
            return false;
        }

        var seed = await _dialogService.ShowBuilderSeedDialogAsync();
        if (seed is null)
        {
            return false;
        }

        if (seed == BuilderSeed.ConfigFile)
            return await EnterFromPickedFileAsync(target);

        try
        {
            if (_mode.CurrentMode == WinhanceMode.ConfigReview)
                await _configurationService.CancelReviewModeAsync();

            _mode.EnterBuilderMode(target);
            OpenInstallOptions(null);

            // After entering, never before: entering clears the session, so choices recorded first would be lost.
            if (seed.Value != BuilderSeed.CurrentMachine)
            {
                foreach (var choice in await _builderSeedSource.ChoicesForAsync(seed.Value, _selectionSetBuilder.CurrentScope))
                    _mode.RecordBuilderEdit(choice);
                _eventBus.Publish(new BuilderSeededEvent());
            }

            return true;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Failed to enter Builder mode with target {target}: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> EnterFromPickedFileAsync(BuilderTarget target)
    {
        var path = _filePicker.PickFile([ConfigFileConstants.ConfigOrAnswerFileFilter, ConfigFileConstants.ConfigOrAnswerFilePattern]);
        if (string.IsNullOrEmpty(path))
            return false;

        if (!Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            var config = await _configLoadService.LoadAndValidateConfigurationAsync(path);
            return config is not null && await EnterFromFileAsync(target, config);
        }

        // Only an answer file Winhance made carries the choices Builder needs; anything else is refused.
        var made = await _autounattendReader.ReadAsync(path);
        if (made?.Config is { } carried)
            return await EnterFromFileAsync(BuilderTarget.Autounattend, carried);

        var message = made is null
            ? _localizationService.GetString("Dialog_BuilderSeed_NotWinhanceFile")
            : _localizationService.GetString("Dialog_BuilderSeed_NoChoices", made.Version);
        await _dialogService.ShowInformationAsync(message, _localizationService.GetString("Dialog_BuilderSeed_Title"));
        return false;
    }

    // Apps are ticked before entering, because the session's summary counts them as soon as its page loads.
    public Task<bool> EnterFromAsync(BuilderTarget target, WinhanceConfigFile config) =>
        EnterFromFileAsync(target, config);

    private async Task<bool> EnterFromFileAsync(BuilderTarget target, WinhanceConfigFile config)
    {
        try
        {
            if (_mode.CurrentMode == WinhanceMode.ConfigReview)
                await _configurationService.CancelReviewModeAsync();

            await _appSelection.SelectWindowsAppsFromConfigAsync(config.WindowsApps);
            await _appSelection.SelectExternalAppsFromConfigAsync(config.ExternalApps);
            await _registry.InitializeAsync();

            _mode.EnterBuilderMode(target);
            OpenInstallOptions(config.AppInstallOptions);

            // Before the scope is read, or a choice for a Windows version this PC is not on falls out of the file.
            await _targetWindows.SetAsync(config.TargetBuild);
            var byFeature = _registry.GetAll(_selectionSetBuilder.CurrentScope);
            var settings = byFeature.Values.SelectMany(s => s).ToList();
            // A carried picture lands on this PC, so the next save can carry it again.
            var choices = (await _fileStore.MaterializeAsync(ConfigFileMapper.FromFile(config, byFeature))).Settings;

            // An Action's include tick is its whole choice, so it gets no edit.
            var actions = settings.Where(s => s.Control == ControlKind.Action).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var choice in choices.Where(c => !actions.Contains(c.SettingId)))
                _mode.RecordBuilderEdit(choice);

            // A file holds only what was included when it was made, so whatever it leaves out was left out then.
            var chosen = choices.Select(c => c.SettingId).ToHashSet(StringComparer.Ordinal);
            foreach (var setting in settings.Where(s => !chosen.Contains(s.Id)))
                _mode.SetIncluded(setting.Id, false);

            _eventBus.Publish(new BuilderSeededEvent());
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Failed to open the file's choices in Builder mode: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ShowIntroIfNeededAsync()
    {
        if (_userPreferencesService.GetPreference(IntroDontShowKey, false))
        {
            return true;
        }

        var response = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = _localizationService.GetString("Dialog_BuilderIntro_Title"),
            Message = _localizationService.GetString("Dialog_BuilderIntro_Message"),
            CheckboxText = _localizationService.GetString("Dialog_Mode_DontShowAgain"),
            CheckboxInitiallyChecked = false,
            ConfirmButtonText = _localizationService.GetString("Dialog_BuilderIntro_Confirm"),
            CancelButtonText = _localizationService.GetString("Button_Cancel"),
        });

        if (response.Confirmed && response.CheckboxChecked)
        {
            await _userPreferencesService.SetPreferenceAsync(IntroDontShowKey, true);
        }

        return response.Confirmed;
    }

    // A fresh file starts from this PC's saved options; leaving Builder drops whatever the session authored.
    private void OpenInstallOptions(InstallOptionsStore? carried)
    {
        _installOptions.EndSession();
        _installOptions.BeginSession(carried ?? _installOptions.Current);
        _installOptionsOpen = true;
    }

    private void OnModeChanged(object? sender, EventArgs e)
    {
        if (!_installOptionsOpen || _mode.CurrentMode == WinhanceMode.Builder)
            return;
        _installOptions.EndSession();
        _installOptionsOpen = false;
    }
}
