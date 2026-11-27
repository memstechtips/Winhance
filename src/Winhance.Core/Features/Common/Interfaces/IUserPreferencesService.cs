using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IUserPreferencesService
{
    Task<Dictionary<string, object>> GetPreferencesAsync();
    Task<OperationResult> SavePreferencesAsync(Dictionary<string, object> preferences);
    Task<OperationResult> SetPreferenceAsync<T>(string key, T value);

    // Synchronous: callers read on the UI thread, where an await on the file read deadlocks.
    T GetPreference<T>(string key, T defaultValue);
}
