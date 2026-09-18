namespace Winhance.Core.Features.Common.Models;

public sealed record PredefinedPowerPlan(string Name, string Description, string LocalizationKey, string Guid);

public sealed record PowerPlanImportResult(bool Success, string ImportedGuid, string ErrorMessage = "");
