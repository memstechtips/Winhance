namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Aggregate interface that combines all Config Review sub-interfaces.
    /// Consumers should prefer depending on the specific sub-interface they need:
    /// <see cref="IConfigReviewModeService"/>, <see cref="IConfigReviewDiffService"/>,
    /// or <see cref="IConfigReviewBadgeService"/>.
    /// </summary>
    public interface IConfigReviewService : IConfigReviewModeService, IConfigReviewDiffService, IConfigReviewBadgeService
    {
    }
}
