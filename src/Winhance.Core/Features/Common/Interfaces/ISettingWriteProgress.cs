namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// The card being written to, as much of it as a write strategy needs to see: somewhere to report
/// that a machine write is outstanding.
///
/// Only the applying strategy touches this. Authoring and refusal finish instantly, and a progress
/// ring that blinks on every Builder toggle would be a visible bug — which is now a claim a test
/// can make, rather than a consequence of where a line happens to sit.
/// </summary>
public interface ISettingWriteProgress
{
    /// <summary>True while a write to the machine is outstanding.</summary>
    bool IsApplying { get; set; }
}
