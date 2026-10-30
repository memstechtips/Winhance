using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Interfaces;

public interface IBuilderModeEntry
{
    // The profile this Builder session was opened from, until Builder is left.
    SettingsProfile? EditingProfile { get; }

    Task<bool> EnterAsync(BuilderTarget target);

    // Opens a session holding exactly what the file holds, with no intro and no seed to choose.
    Task<bool> EnterFromAsync(BuilderTarget target, WinhanceConfigFile config);

    Task<bool> EnterFromProfileAsync(SettingsProfile profile);
}
