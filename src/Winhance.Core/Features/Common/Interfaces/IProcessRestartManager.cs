using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IProcessRestartManager
{
    /// <summary>Catalog-Setting overload of the single-setting restart: reads the unified ApplyBehavior.Restart
    /// (RestartProcess / RestartService), equivalent to the def's separate RestartProcess/RestartService because no
    /// setting sets both (RestartTargetCatalogEquivalenceTests). SAS repoints its single-setting restart onto this
    /// as it ports off SettingDefinition; the def overload stays for the not-yet-ported paths.</summary>
    Task HandleProcessAndServiceRestartsAsync(Setting setting);

    /// <summary>Catalog-Setting overload of the batch flush: reads the unified ApplyBehavior.Restart. The
    /// apply-cluster (RecommendedSettingsApplier / BulkSettingsActionService) uses this once it deals in
    /// Setting; the SettingDefinition overload stays for the not-yet-ported SAS path. Equivalent because no
    /// setting sets both a process and a service restart (RestartTargetCatalogEquivalenceTests).</summary>
    Task FlushCoalescedRestartsAsync(IEnumerable<Setting> appliedSettings);

    /// <summary>
    /// Suppresses all process/service restarts until the returned scope is disposed.
    /// Used by the dependency resolver when auto-enabling multiple children,
    /// so that a single restart from the parent covers all of them.
    /// </summary>
    IDisposable SuppressRestarts();
}
