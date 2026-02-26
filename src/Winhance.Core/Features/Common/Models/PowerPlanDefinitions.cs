using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Models;

public record PredefinedPowerPlan(string Name, string Description, string LocalizationKey, string Guid);

public record PowerPlanComboBoxOption
{
    public string DisplayName { get; init; } = string.Empty;
    public PredefinedPowerPlan? PredefinedPlan { get; init; }
    public PowerPlan? SystemPlan { get; init; }
    public bool ExistsOnSystem { get; init; }
    public bool IsActive { get; init; }
    public int Index { get; init; }
}

public record PowerPlanImportResult(bool Success, string ImportedGuid, string ErrorMessage = "");

public record PowerPlanResolutionResult
{
    public bool Success { get; init; }
    public string Guid { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
