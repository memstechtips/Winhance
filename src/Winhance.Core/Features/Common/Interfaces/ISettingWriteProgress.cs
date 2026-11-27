namespace Winhance.Core.Features.Common.Interfaces;

// Only the applying strategy touches this: authoring and refusal finish instantly, and a progress ring that
// blinks on every Builder toggle would be a visible bug.
public interface ISettingWriteProgress
{
    bool IsApplying { get; set; }
}
