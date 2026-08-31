namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IOptionalFeatureService
{
    string BuildEnableStatement(IReadOnlyList<string> featureNames);
}
