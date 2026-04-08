using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IProcessRestartManager
{
    Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting);

    /// <summary>
    /// Suppresses all process/service restarts until the returned scope is disposed.
    /// Used by the dependency resolver when auto-enabling multiple children,
    /// so that a single restart from the parent covers all of them.
    /// </summary>
    IDisposable SuppressRestarts();
}
