using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    public record SettingGroup
    {
        public required string Name { get; init; }
        public required string FeatureId { get; init; }
        public required IReadOnlyList<SettingDefinition> Settings { get; init; }
    }
}
