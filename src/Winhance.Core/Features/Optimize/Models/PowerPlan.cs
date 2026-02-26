namespace Winhance.Core.Features.Optimize.Models;

public record PowerPlan
{
    public string Name { get; init; } = string.Empty;
    public string Guid { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
