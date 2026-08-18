namespace Winhance.Core.Features.Common.Catalog;

// Returns the matching state's label, or null for Custom.
public interface IStateDetector
{
    string? Detect(Setting setting, IDetectionContext context);
}
