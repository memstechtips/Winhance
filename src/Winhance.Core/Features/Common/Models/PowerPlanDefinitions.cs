using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Models
{
    public record PredefinedPowerPlan(string Name, string Description, string Guid);

    public class PowerPlanComboBoxOption
    {
        public string DisplayName { get; set; } = string.Empty;
        public PredefinedPowerPlan? PredefinedPlan { get; set; }
        public PowerPlan? SystemPlan { get; set; }
        public bool ExistsOnSystem { get; set; }
        public bool IsActive { get; set; }
        public int Index { get; set; }
    }

    public record PowerPlanImportResult(bool Success, string ImportedGuid, string ErrorMessage = "");
}