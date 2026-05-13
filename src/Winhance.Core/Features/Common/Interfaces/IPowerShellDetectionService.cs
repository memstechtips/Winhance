using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Runs read-only PowerShell detection scripts (the <see cref="PowerShellScriptSetting.DetectionScript"/>
/// of each setting) in-process via a shared Runspace. Returns the resolved enabled-state per setting ID.
///
/// One call to <see cref="DetectAsync"/> handles all settings in one runspace lifecycle:
/// open → run each script sequentially → close. A 10 s total batch timeout applies.
///
/// On any per-script error (throw, malformed output, timeout) the setting's value
/// is reported as <c>false</c> and a warning is logged.
/// </summary>
public interface IPowerShellDetectionService
{
    /// <summary>
    /// Resolves enabled-state for each input. Keys of the returned dictionary are the
    /// <see cref="SettingDefinition.Id"/> of every input setting that had a non-empty
    /// <see cref="PowerShellScriptSetting.DetectionScript"/>. Settings without a
    /// detection script are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<string, bool>> DetectAsync(
        IEnumerable<SettingDefinition> settings,
        CancellationToken ct = default);
}
