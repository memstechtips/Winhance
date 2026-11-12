using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Services;

// Leaving Builder with authored work, or a pending Config Review, discards it. Switching to Normal asks through here;
// entering Builder or Config Review shows the same warning inside that mode's own entry dialog instead.
public static class ModeSwitchConfirmation
{
    public static async Task<bool> ConfirmLeavingAsync(
        IApplicationModeService mode, IDialogService dialogs, ILocalizationService localization)
    {
        if (!DiscardsWork(mode))
            return true;

        var response = await dialogs.ShowConfirmationAsync(new ConfirmationRequest
        {
            Message = localization.GetStringOrDefault("Mode_Switch_Confirmation", "Switch mode? Your unsaved progress in this mode will be discarded. Nothing was applied to your system."),
            Title = localization.GetStringOrDefault("Mode_Switch_Confirmation_Title", "Switch Mode"),
        });
        return response.Confirmed;
    }

    public static string? DiscardWarning(IApplicationModeService mode, ILocalizationService localization) =>
        DiscardsWork(mode) ? localization.GetString("Dialog_ModeEntry_Discards") : null;

    // HasBuilderChanges, not GetBuilderEdits().Count: an input shape with no serializable ChoiceValue is still
    // authored work, and counting edits silently skipped the prompt for anyone whose session had produced none.
    private static bool DiscardsWork(IApplicationModeService mode) =>
        (mode.CurrentMode == WinhanceMode.Builder && mode.HasBuilderChanges) || mode.CurrentMode == WinhanceMode.ConfigReview;
}
