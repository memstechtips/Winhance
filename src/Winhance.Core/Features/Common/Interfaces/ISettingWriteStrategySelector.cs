namespace Winhance.Core.Features.Common.Interfaces;

// Resolved per write rather than injected once, because the mode changes while ViewModels stay alive.
public interface ISettingWriteStrategySelector
{
    ISettingWriteStrategy ForCurrentMode();
}
