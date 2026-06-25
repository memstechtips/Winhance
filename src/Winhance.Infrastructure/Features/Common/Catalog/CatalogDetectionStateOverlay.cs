using System;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Catalog;

/// <summary>
/// Phase 6.3 detection cutover (transitional): overlays the NEW catalog engine's authoritative primary state onto the
/// OLD <see cref="SettingStateResult"/>, leaving the old result's auxiliary data (RawValues, TooltipData, the AC/DC
/// split) in place until later phases retire it. The new engine decides a toggle's on/off and a selection's chosen
/// option; everything else stays as the old discovery produced it. Removed once the UI binds to the Setting model
/// directly (Phase 6.7).
/// </summary>
public static class CatalogDetectionStateOverlay
{
    /// <summary>Returns <paramref name="old"/> with the new engine's state overlaid. When there is no new result
    /// (the setting has no catalog peer) or the new engine returned nothing usable, the old result is returned
    /// unchanged, so an unpaired or detector-null setting never regresses.</summary>
    public static SettingStateResult Apply(SettingDefinition def, SettingStateResult old, CatalogDetectionResult? newResult)
    {
        if (newResult is null)
            return old;

        switch (def.InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
                // A new toggle resolves to the literal "Enabled"/"Disabled" labels; any other value (a custom detector
                // that returned null -> Custom) is a no-op, so the old state stands.
                if (newResult.StateLabel is "Enabled" or "Disabled")
                    return old with { IsEnabled = newResult.StateLabel == "Enabled" };
                return old;

            case InputType.Selection:
                // The new label is an option DisplayName; resolve it back to the index the view-model consumes. A label
                // that matches no option (Custom) or a setting with no options keeps the old resolved index.
                var options = def.ComboBox?.Options;
                if (newResult.StateLabel is { } label && options is not null)
                {
                    for (int i = 0; i < options.Count; i++)
                    {
                        if (string.Equals(options[i].DisplayName, label, StringComparison.Ordinal))
                            return old with { CurrentValue = i };
                    }
                }
                return old;

            default:
                // NumericRange keeps the old value (the new Value is AC-only and equals old AC by equivalence; the
                // slider's AC/DC come from the old RawValues). Action carries no state. Both leave the old untouched.
                return old;
        }
    }
}
