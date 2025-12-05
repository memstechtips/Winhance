namespace Winhance.Core.Features.Common.Models
{
    public record FeatureDefinition(
        string Id,
        string DefaultName,
        string IconName,
        string Category,
        int SortOrder
    );
}
