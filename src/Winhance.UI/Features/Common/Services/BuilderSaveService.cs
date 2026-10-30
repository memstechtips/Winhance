using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Services;

public sealed class BuilderSaveService : IBuilderSaveService
{
    private readonly ISelectionSetBuilder _selections;
    private readonly ISelectionSaveService _saves;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;
    private readonly IProfileService _profiles;
    private readonly IBuilderModeEntry _entry;

    public BuilderSaveService(
        ISelectionSetBuilder selections,
        ISelectionSaveService saves,
        IDialogService dialogs,
        ILocalizationService loc,
        ILogService log,
        IProfileService profiles,
        IBuilderModeEntry entry)
    {
        _selections = selections;
        _saves = saves;
        _dialogs = dialogs;
        _loc = loc;
        _log = log;
        _profiles = profiles;
        _entry = entry;
    }

    public async Task SaveAsync(BuilderTarget target)
    {
        try
        {
            var set = await _selections.FromBuilderSessionAsync();
            await _saves.SaveAsync(target, set);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"Builder Save failed: {ex.Message}");
            await _dialogs.ShowErrorAsync(
                _loc.GetString("Config_Export_Error_Message", ex.Message),
                _loc.GetString("Config_Export_Error_Title"));
        }
    }

    public async Task SaveAsProfileAsync()
    {
        var editing = _entry.EditingProfile is { Kind: ProfileKind.User } own ? own.Name : null;
        var name = await _dialogs.AskTextAsync(new TextInputRequest
        {
            Title = _loc.GetString("Builder_SaveProfile_Title"),
            Message = _loc.GetString("Builder_SaveProfile_Message"),
            Text = editing ?? string.Empty,
            Placeholder = _loc.GetString("Profiles_Save_Placeholder"),
            ConfirmButtonText = _loc.GetString("Profiles_Save_Button"),
            IsValid = text => _profiles.IsNameAvailable(text) || string.Equals(text.Trim(), editing, StringComparison.CurrentCultureIgnoreCase),
        });
        if (name is null)
            return;

        try
        {
            var set = await _selections.FromBuilderSessionAsync();
            if (await _profiles.SaveAsync(name, set.Settings) is null)
                await _dialogs.ShowErrorAsync(_loc.GetString("Profiles_Save_Failed"));
            else
                await _dialogs.ShowInformationAsync(_loc.GetString("Builder_SaveProfile_Saved", name), _loc.GetString("Builder_SaveProfile_Title"));
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"Builder profile save failed: {ex.Message}");
            await _dialogs.ShowErrorAsync(_loc.GetString("Profiles_Save_Failed"));
        }
    }
}
