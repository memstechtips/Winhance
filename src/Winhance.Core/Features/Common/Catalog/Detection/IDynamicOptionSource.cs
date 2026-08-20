namespace Winhance.Core.Features.Common.Catalog;

// ExistsOnSystem false = offered but not installed (a predefined power plan); selecting it creates/imports it.
public sealed record DynamicOption(string Label, string Value, bool ExistsOnSystem = true);

public interface IDynamicOptionSource
{
    IReadOnlyList<DynamicOption> EnumerateOptions(IDetectionContext context);

    string? CurrentSelection(IDetectionContext context);

    // The option Label is a localization key for a predefined plan, so the raw OS name is read separately here.
    string? CurrentSelectionName(IDetectionContext context) => null;
}

public sealed class PowerPlanOptionSource : IDynamicOptionSource
{
    public IReadOnlyList<DynamicOption> EnumerateOptions(IDetectionContext context) => context.InstalledPowerPlans();

    public string? CurrentSelection(IDetectionContext context) => context.ActivePowerPlanGuid();

    public string? CurrentSelectionName(IDetectionContext context) => context.ActivePowerPlanName();
}
