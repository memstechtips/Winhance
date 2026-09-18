using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Common.Helpers;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed class BuilderSeedSource : IBuilderSeedSource
{
    private readonly ICatalogSettingsRegistry _registry;
    private readonly IWindowsVersionService _version;
    private readonly ILogService _log;

    public BuilderSeedSource(ICatalogSettingsRegistry registry, IWindowsVersionService version, ILogService log)
    {
        _registry = registry;
        _version = version;
        _log = log;
    }

    public async Task<IReadOnlyList<SettingChoice>> ChoicesForAsync(BuilderSeed seed, CatalogScope scope)
    {
        if (seed == BuilderSeed.CurrentMachine) return Array.Empty<SettingChoice>();

        await _registry.InitializeAsync().ConfigureAwait(false);
        var build = new WinBuild(_version.GetWindowsBuildNumber(), _version.GetWindowsBuildRevision());
        bool useRecommended = seed == BuilderSeed.Recommended;
        var choices = new List<SettingChoice>();

        foreach (var settings in _registry.GetAll(scope).Values)
        {
            foreach (var setting in settings)
            {
                if (setting.Control == ControlKind.Action) continue;

                // A card with ReadOnly targets is seeded from this PC's reading; a role sweep must not overwrite it.
                if (setting.IsAnswerFileOnly && setting.Targets.OfType<RegTarget>().Any(t => t.ReadOnly)) continue;

                if (ChoiceFor(setting, build, useRecommended) is { } value)
                    choices.Add(new SettingChoice(setting.Id, value));
            }
        }

        _log.Log(LogLevel.Info, $"Seed {seed} produced {choices.Count} setting choices");
        return choices;
    }

    private static ChoiceValue? ChoiceFor(Setting setting, WinBuild build, bool useRecommended)
    {
        switch (setting.Control)
        {
            case ControlKind.Toggle:
            case ControlKind.CheckBox:
                bool? on = useRecommended
                    ? TwoState.GetRecommended(setting, build)
                    : TwoState.GetDefault(setting, build);
                return on is { } isOn ? ChoiceValue.TwoState(setting.Control, isOn) : null;

            case ControlKind.Selection:
                object? optionValue = RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended);
                if (AcDcOf(optionValue) is { } indices) return new ChoiceValue.AcDcOption(indices.Ac, indices.Dc);
                if (optionValue is int powerIndex) return new ChoiceValue.Option(powerIndex);
                int? index = useRecommended
                    ? RecommendedSettingsResolver.GetRecommendedIndex(setting)
                    : RecommendedSettingsResolver.GetDefaultIndex(setting, build);
                return index is { } stateIndex ? new ChoiceValue.Option(stateIndex) : null;

            case ControlKind.Slider:
                // Numeric.Recommended / .WindowsDefault are DISPLAY units (power-harddisk-timeout is a Minutes
                // slider over a Seconds powercfg value); a ChoiceValue number is the SYSTEM value.
                object? numericValue = RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended);
                if (AcDcOf(numericValue) is { } contexts)
                    return new ChoiceValue.AcDcNumber(ToSystemUnits(contexts.Ac, setting), ToSystemUnits(contexts.Dc, setting));
                return numericValue is int display ? new ChoiceValue.Number(ToSystemUnits(display, setting)) : null;

            case ControlKind.TextBox:
                return setting.TextBox?.Default is { } text ? new ChoiceValue.Text(text) : null;

            default:
                return null;
        }
    }

    // A Separate-mode powercfg setting yields both contexts at once, keyed the way the apply pipeline reads them.
    private static (int Ac, int Dc)? AcDcOf(object? applyValue)
    {
        if (applyValue is not IReadOnlyDictionary<string, object?> pair) return null;
        if (!pair.TryGetValue("ACValue", out var ac) || !pair.TryGetValue("DCValue", out var dc)) return null;
        return (Convert.ToInt32(ac), Convert.ToInt32(dc));
    }

    private static int ToSystemUnits(int displayValue, Setting setting) =>
        RecommendedSettingsResolver.ConvertDisplayToSystemUnits(displayValue, setting.Numeric!.Units);
}
