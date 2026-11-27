using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Interfaces;

public interface IBuilderModeEntry
{
    // The profile this Builder session was opened from, until Builder is left.
    SettingsProfile? EditingProfile { get; }

    // Where this Builder session started, until Builder is left; a profile or file opened from disk reads ConfigFile.
    BuilderSeed? StartedFrom { get; }

    event EventHandler? StartedFromChanged;

    // reason: why another page sent the user here, shown at the top of the entry dialog; null when they chose Builder.
    Task<bool> EnterAsync(BuilderTarget target, string? reason);

    // Opens a session holding exactly what the file holds, with no intro and no seed to choose.
    Task<bool> EnterFromAsync(BuilderTarget target, WinhanceConfigFile config);

    Task<bool> EnterFromProfileAsync(SettingsProfile profile);

    // Asks once, then starts the open session over from another seed. False, with nothing changed, when declined.
    Task<bool> ReseedAsync(BuilderSeed seed);
}
