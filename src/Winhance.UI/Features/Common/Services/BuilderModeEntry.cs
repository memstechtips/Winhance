using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
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
    private readonly IEventBus _eventBus;
    private readonly ILogService _logService;

    public BuilderModeEntry(
        IApplicationModeService mode,
        IConfigurationService configurationService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IUserPreferencesService userPreferencesService,
        IBuilderSeedSource builderSeedSource,
        ISelectionSetBuilder selectionSetBuilder,
        IEventBus eventBus,
        ILogService logService)
    {
        _mode = mode;
        _configurationService = configurationService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;
        _builderSeedSource = builderSeedSource;
        _selectionSetBuilder = selectionSetBuilder;
        _eventBus = eventBus;
        _logService = logService;
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

        try
        {
            if (_mode.CurrentMode == WinhanceMode.ConfigReview)
                await _configurationService.CancelReviewModeAsync();

            _mode.EnterBuilderMode(target);

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
}
