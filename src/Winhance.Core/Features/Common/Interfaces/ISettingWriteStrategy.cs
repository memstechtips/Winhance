using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

// Exists so the code around a write does not branch on the mode. One implementation per mode: a new mode
// implements this and the compiler names every operation it has to answer for, instead of a person remembering
// to add a branch in five places.
public interface ISettingWriteStrategy
{
    // Never throws: a failure comes back as Rejected, so every caller handles a failed write the way it handles a cancelled one.
    Task<SettingWriteResult> WriteAsync(SettingWriteRequest request, ISettingWriteProgress progress);
}
