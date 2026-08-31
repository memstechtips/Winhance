using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IOptionalFeatureService
{
    string BuildEnableStatement(IReadOnlyList<string> featureNames);
    Task<bool> EnableFeaturesAsync(IReadOnlyList<string> featureNames, IReadOnlyList<string>? displayNames = null, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
}
