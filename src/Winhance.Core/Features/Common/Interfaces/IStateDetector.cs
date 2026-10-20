using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

// Returns the matching state's label, or null for Custom.
public interface IStateDetector
{
    // A string, not a LocKey: a keyed selection returns the id the machine supplied, which is no localization key.
    string? Detect(Setting setting, IDetectionContext context);
}
