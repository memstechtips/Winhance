using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// What a mode does with a setting edit: apply it, record it, or refuse it.
///
/// This exists so the code around a write does not branch on the mode. Before it, each of the five
/// input handlers carried its own <c>if (builderMode) { ...; return; }</c> early return, and each
/// hand-maintained its own copy of the bookkeeping that follows a change — which is how two of the
/// five came to skip recording the edit entirely. One implementation per mode means a new mode
/// implements this interface and the compiler names every operation it has to answer for, instead
/// of a person remembering to add a sixth branch in five places.
/// </summary>
public interface ISettingWriteStrategy
{
    /// <summary>
    /// Carry out <paramref name="request"/> under this mode's rules, reporting in-flight machine
    /// work through <paramref name="progress"/>.
    ///
    /// Never throws. A failure comes back as <see cref="Enums.SettingWriteOutcome.Rejected"/> so
    /// that every caller handles a failed write the same way it handles a cancelled one — there is
    /// no second error path to keep in sync.
    /// </summary>
    Task<SettingWriteResult> WriteAsync(SettingWriteRequest request, ISettingWriteProgress progress);
}
