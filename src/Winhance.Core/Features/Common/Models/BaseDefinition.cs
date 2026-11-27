namespace Winhance.Core.Features.Common.Models;

public abstract record BaseDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? GroupName { get; init; }
}
