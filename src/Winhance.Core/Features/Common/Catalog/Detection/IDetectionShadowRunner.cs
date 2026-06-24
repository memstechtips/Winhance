using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Runs the new catalog detection engine alongside the old discovery result and logs per-setting
/// divergences for a human to review. Observe-only: it never changes what the UI consumes and never throws into
/// its caller. A no-op unless explicitly enabled (the WINHANCE_CATALOG_SHADOW environment variable).</summary>
public interface IDetectionShadowRunner
{
    Task RunAsync(
        IReadOnlyList<SettingDefinition> oldDefinitions,
        IReadOnlyDictionary<string, SettingStateResult> oldStates);
}
