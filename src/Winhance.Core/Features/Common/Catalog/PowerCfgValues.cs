namespace Winhance.Core.Features.Common.Catalog;

// powercfg stores timeouts in seconds and the catalog shows them in Numeric.Units. Milliseconds stay 1:1: the only
// such setting (USB selective suspend timeout) is stored in milliseconds already, and multiplying by 1000 would push
// its recommended 1000 past the slider's Max, where the NumberBox clamps it and the clamped value gets written back.
public static class PowerCfgValues
{
    public static int ToDisplay(int systemValue, string? units) => units?.ToLowerInvariant() switch
    {
        "minutes" => systemValue / 60,
        "hours" => systemValue / 3600,
        _ => systemValue,
    };

    public static int ToSystem(int displayValue, string? units) => units?.ToLowerInvariant() switch
    {
        "minutes" => displayValue * 60,
        "hours" => displayValue * 3600,
        _ => displayValue,
    };

    // The option whose state value for the setting's powercfg target accepts a raw AC or DC reading; null when none
    // does, which the caller shows or stores as Custom rather than guessing an option.
    public static int? OptionIndex(Setting setting, int? rawValue)
    {
        if (rawValue is not { } raw) return null;
        if (setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault() is not { } target) return null;
        for (int i = 0; i < setting.States.Count; i++)
        {
            if (setting.States[i].Set.TryGetValue(target.Key, out var value) && value.Matches(raw, present: true))
                return i;
        }
        return null;
    }
}
