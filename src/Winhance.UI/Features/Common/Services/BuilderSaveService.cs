using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Services;

public sealed class BuilderSaveService(
    ISelectionSetBuilder selections,
    ISelectionSaveService saves,
    IDialogService dialogService,
    ILocalizationService localizationService,
    ILogService logService,
    IProfileService profiles,
    IBuilderModeEntry entry) : IBuilderSaveService
{
    public async Task SaveAsync(BuilderTarget target)
    {
        try
        {
            var set = await selections.FromBuilderSessionAsync();
            await saves.SaveAsync(target, set);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Builder Save failed: {ex.Message}");
            await dialogService.ShowErrorAsync(
                localizationService.GetString("Config_Export_Error_Message", ex.Message),
                localizationService.GetString("Config_Export_Error_Title"));
        }
    }

    public async Task SaveAsProfileAsync()
    {
        var editing = entry.EditingProfile is { Kind: ProfileKind.User } own ? own.Name : null;
        var name = await dialogService.AskTextAsync(new TextInputRequest
        {
            Title = localizationService.GetString("Builder_SaveProfile_Title"),
            Message = localizationService.GetString("Builder_SaveProfile_Message"),
            Text = editing ?? string.Empty,
            Placeholder = localizationService.GetString("Profiles_Save_Placeholder"),
            ConfirmButtonText = localizationService.GetString("Profiles_Save_Button"),
            IsValid = text => profiles.IsNameAvailable(text) || string.Equals(text.Trim(), editing, StringComparison.CurrentCultureIgnoreCase),
        });
        if (name is null)
            return;

        try
        {
            var set = await selections.FromBuilderSessionAsync();
            if (await profiles.SaveAsync(name, set.Settings) is null)
                await dialogService.ShowErrorAsync(localizationService.GetString("Profiles_Save_Failed"));
            else
                await dialogService.ShowInformationAsync(localizationService.GetString("Builder_SaveProfile_Saved", name), localizationService.GetString("Builder_SaveProfile_Title"));
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Builder profile save failed: {ex.Message}");
            await dialogService.ShowErrorAsync(localizationService.GetString("Profiles_Save_Failed"));
        }
    }
}
