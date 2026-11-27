using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.Autounattend;

public static class AutounattendInputService
{
    public const string SettingId = "gaming-touch-keyboard-service";

    public static LocKey RequiredState => LocKey.ServiceOption.AutomaticRecommended;

    public static bool IsRequired(string settingId, WinhanceMode mode, BuilderTarget target) =>
        mode == WinhanceMode.Builder && target == BuilderTarget.Autounattend && settingId == SettingId;

    public static SelectionSet Enforce(SelectionSet set)
    {
        var setting = SettingCatalog.Find(SettingId)!;
        var index = setting.States.ToList().FindIndex(state => state.Label == RequiredState);
        var choice = new SettingChoice(SettingId, new ChoiceValue.Option(index));
        return set with
        {
            Settings = [.. set.Settings.Where(c => SettingIdAliases.Normalize(c.SettingId) != SettingId), choice],
        };
    }
}
