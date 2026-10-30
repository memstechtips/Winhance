using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Services;

// Leaving Builder with authored work, or a pending Config Review, discards it. The mode switcher and Import both leave
// the current mode, so both ask through here.
public static class ModeSwitchConfirmation
{
    public static async Task<bool> ConfirmLeavingAsync(
        IApplicationModeService mode, IDialogService dialogs, ILocalizationService localization)
    {
        // HasBuilderChanges, not GetBuilderEdits().Count: an input shape with no serializable ChoiceValue is still
        // authored work, and counting edits silently skipped the prompt for anyone whose session had produced none.
        bool leavingBuilderWithEdits = mode.CurrentMode == WinhanceMode.Builder && mode.HasBuilderChanges;
        if (!leavingBuilderWithEdits && mode.CurrentMode != WinhanceMode.ConfigReview)
            return true;

        var response = await dialogs.ShowConfirmationAsync(new ConfirmationRequest
        {
            Message = localization.GetStringOrDefault("Mode_Switch_Confirmation", "Switch mode? Your unsaved progress in this mode will be discarded. Nothing was applied to your system."),
            Title = localization.GetStringOrDefault("Mode_Switch_Confirmation_Title", "Switch Mode"),
        });
        return response.Confirmed;
    }
}
