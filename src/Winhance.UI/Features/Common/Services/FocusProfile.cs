using Winhance.Core.Features.Common.Selections;

namespace Winhance.UI.Features.Common.Services;

// What Winhance Focus changes and the value each setting takes. Every value applies without a reboot or an Explorer
// restart, so a switch takes effect at once.
internal static class FocusProfile
{
    private static readonly ChoiceValue Off = new ChoiceValue.Toggle(false);
    private static readonly ChoiceValue On = new ChoiceValue.Toggle(true);

    // A service's first state is the one that writes Start=4.
    private static readonly ChoiceValue ServiceDisabled = new ChoiceValue.Option(0);

    private static readonly IReadOnlyDictionary<string, ChoiceValue> Values = new Dictionary<string, ChoiceValue>
    {
        ["windows-pushnotifications"] = Off,
        ["notifications-sound"] = Off,
        ["updates-delivery-optimization"] = new ChoiceValue.Option(3),
        ["updates-store-auto-download"] = Off,
        ["updates-restart-notification"] = Off,
        ["updates-policy-mode"] = new ChoiceValue.Option(2),
        ["gaming-xbox-game-dvr"] = Off,
        ["gaming-game-bar-tips"] = Off,
        ["gaming-background-apps"] = new ChoiceValue.Option(2),
        ["gaming-storage-sense"] = Off,
        ["gaming-game-mode"] = On,
        ["gaming-directx-flip-model"] = On,
        ["gaming-windows-search-service"] = ServiceDisabled,
        ["gaming-sysmain-service"] = ServiceDisabled,
        ["gaming-telemetry-service"] = ServiceDisabled,
        ["gaming-error-reporting-service"] = ServiceDisabled,
        ["gaming-compatibility-assistant-service"] = ServiceDisabled,
        ["gaming-task-compatibility-appraiser"] = Off,
        ["gaming-task-ceip-consolidator"] = Off,
        ["gaming-task-usb-ceip"] = Off,
        ["gaming-task-disk-diagnostic"] = Off,
        ["gaming-task-feedback-dmclient"] = Off,
        ["gaming-task-feedback-dmclient-download"] = Off,
        ["gaming-task-sqm"] = Off,
        ["gaming-task-program-data-updater"] = Off,
    };

    // Plugged in only: on battery the machine keeps its own value. A file entry without a DC value applies its AC
    // value to both, so these are always written as a pair with the DC read from the machine.
    private static readonly IReadOnlyDictionary<string, ChoiceValue> PowerAc = new Dictionary<string, ChoiceValue>
    {
        ["processor-min-state"] = new ChoiceValue.Number(100),
        ["processor-energy-performance-preference"] = new ChoiceValue.Number(0),
        ["usb-selective-suspend"] = new ChoiceValue.Option(0),
        ["pci-link-state-power-management"] = new ChoiceValue.Option(0),
    };

    internal static IReadOnlyList<string> Ids { get; } = [.. Values.Keys, .. PowerAc.Keys];

    internal static IReadOnlyList<SettingChoice> Overlay(IReadOnlyList<SettingChoice> current)
    {
        var choices = Values.Select(pair => new SettingChoice(pair.Key, pair.Value)).ToList();
        foreach (var (id, ac) in PowerAc)
        {
            if (KeepMachineDc(id, ac, current.FirstOrDefault(c => c.SettingId == id)?.Value) is { } value)
                choices.Add(new SettingChoice(id, value));
        }
        return choices;
    }

    // A power setting's value paired with the DC the machine reads; null when that reading is missing.
    internal static ChoiceValue? KeepMachineDc(string id, ChoiceValue value, ChoiceValue? machine) =>
        !PowerAc.ContainsKey(id) ? value : (value, machine) switch
        {
            (ChoiceValue.Number ac, ChoiceValue.AcDcNumber read) => new ChoiceValue.AcDcNumber(ac.Value, read.Dc),
            (ChoiceValue.AcDcNumber ac, ChoiceValue.AcDcNumber read) => new ChoiceValue.AcDcNumber(ac.Ac, read.Dc),
            (ChoiceValue.Option ac, ChoiceValue.AcDcOption read) => new ChoiceValue.AcDcOption(ac.Index, read.DcIndex),
            (ChoiceValue.AcDcOption ac, ChoiceValue.AcDcOption read) => new ChoiceValue.AcDcOption(ac.AcIndex, read.DcIndex),
            _ => null,
        };
}
