using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class ConfigImportState : IConfigImportState
{
    public bool IsActive { get; set; }
    public string? SourceName { get; set; }
    public bool ImportSuppliesPowerValues { get; set; }
}
