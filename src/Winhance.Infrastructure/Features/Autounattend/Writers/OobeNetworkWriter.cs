using System.Xml.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Infrastructure.Features.Autounattend.Writers;

// Whether Setup runs offline is only the card's choice when this file creates the accounts: a local account
// cannot be made while OOBE is online, and a Microsoft account cannot be made while it is not. The account
// card's Controls carries those two answers, so this reads the decision rather than repeating it, and a value
// left over from another account mode - the card is greyed, not cleared - never reaches the file.
internal sealed class OobeNetworkWriter : IAutounattendElementWriter
{
    private const string SettingId = "autounattend-oobe-network";

    public IReadOnlyCollection<string> Handles => [SettingId];

    public void Write(XDocument doc, AutounattendRenderContext context)
    {
        var setting = SettingCatalog.ById[SettingId];
        var chosen = Decided(context) ?? context.StateOf(SettingId)?.Label;

        if (setting.States.FirstOrDefault(state => state.Label == chosen) is not { } state)
            return;

        AutounattendElementWriter.WriteState(doc, setting, state);
    }

    private static LocKey? Decided(AutounattendRenderContext context) =>
        context.StateOf("autounattend-account")?.Controls is { } controls
        && controls.TryGetValue(SettingId, out var label)
            ? label
            : null;
}
