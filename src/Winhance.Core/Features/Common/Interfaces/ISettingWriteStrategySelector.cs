namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Resolves the <see cref="ISettingWriteStrategy"/> for whatever mode is active right now.
///
/// Resolved per write rather than injected once, because the mode changes while ViewModels stay
/// alive.
/// </summary>
public interface ISettingWriteStrategySelector
{
    /// <summary>The strategy the active mode's capabilities call for.</summary>
    ISettingWriteStrategy ForCurrentMode();
}
