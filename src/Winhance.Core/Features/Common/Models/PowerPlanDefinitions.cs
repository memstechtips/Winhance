using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Models;

public sealed record PredefinedPowerPlan(string Name, string Description, string LocalizationKey, string Guid);

public sealed record PowerPlanComboBoxOption
{
    public string DisplayName { get; init; } = string.Empty;
    public string Guid { get; init; } = string.Empty;
    public PowerPlan? SystemPlan { get; init; }
    public bool ExistsOnSystem { get; init; }
    public bool IsActive { get; init; }
    public int Index { get; init; }
}

public sealed record PowerPlanImportResult(bool Success, string ImportedGuid, string ErrorMessage = "");
