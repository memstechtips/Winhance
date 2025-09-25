using System.Collections.Generic;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public record ItemGroup
    {
        public required string Name { get; init; }
        public required string FeatureId { get; init; }
        public required IReadOnlyList<ItemDefinition> Items { get; init; }
    }
}