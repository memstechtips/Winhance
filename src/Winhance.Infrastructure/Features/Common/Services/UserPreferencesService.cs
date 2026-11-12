using System.Text.Json;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class UserPreferencesService : IUserPreferencesService
{
    private const string PreferencesFileName = "UserPreferences.json";
    private readonly ILogService _logService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IFileSystemService _fileSystemService;

    // Serializes file I/O — multiple fire-and-forget SetPreferenceAsync calls
    // from startup paths (NewBadgeService, ScriptMigrationService, StartupOrchestrator)
    // raced through read-modify-write and lost writes.
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    // Winhance is the only writer of the file, so it is parsed once and each save refreshes this copy. Guarded by
    // _fileLock.
    private Dictionary<string, object>? _cache;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    public UserPreferencesService(ILogService logService, IInteractiveUserService interactiveUserService, IFileSystemService fileSystemService)
    {
        _logService = logService;
        _interactiveUserService = interactiveUserService;
        _fileSystemService = fileSystemService;
    }

    private string GetPreferencesFilePath()
    {
        try
        {
            string localAppData = _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrEmpty(localAppData))
            {
                _logService.Log(LogLevel.Error, "LocalApplicationData folder path is empty");
                localAppData = _fileSystemService.CombinePath(_interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local");
                _logService.Log(LogLevel.Info, $"Using fallback path: {localAppData}");
            }

            string appDataPath = _fileSystemService.CombinePath(AppDataPaths.UserRoot(localAppData), "Config");

            if (!_fileSystemService.DirectoryExists(appDataPath))
            {
                _fileSystemService.CreateDirectory(appDataPath);
                _logService.Log(LogLevel.Info, $"Created preferences directory: {appDataPath}");
            }

            string filePath = _fileSystemService.CombinePath(appDataPath, PreferencesFileName);

            return filePath;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error getting preferences file path: {ex.Message}");

            string tempPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), "Winhance", "Config");
            _fileSystemService.CreateDirectory(tempPath);
            string tempFilePath = _fileSystemService.CombinePath(tempPath, PreferencesFileName);

            _logService.Log(LogLevel.Warning, $"Using fallback temporary path: {tempFilePath}");
            return tempFilePath;
        }
    }

    public async Task<Dictionary<string, object>> GetPreferencesAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // A copy: callers edit the dictionary and hand it back to SavePreferencesAsync.
            return new Dictionary<string, object>(await LoadUnsafeAsync().ConfigureAwait(false));
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<Dictionary<string, object>> LoadUnsafeAsync()
    {
        if (_cache != null)
            return _cache;

        try
        {
            string filePath = GetPreferencesFilePath();
            string? json = _fileSystemService.FileExists(filePath)
                ? await _fileSystemService.ReadAllTextAsync(filePath).ConfigureAwait(false)
                : null;
            return _cache = Parse(json, filePath);
        }
        catch (Exception ex)
        {
            LogReadError(ex);
            return new Dictionary<string, object>();
        }
    }

    // The sync twin for startup, where an await on the UI thread deadlocks.
    private Dictionary<string, object> LoadUnsafe()
    {
        if (_cache != null)
            return _cache;

        try
        {
            string filePath = GetPreferencesFilePath();
            string? json = _fileSystemService.FileExists(filePath) ? _fileSystemService.ReadAllText(filePath) : null;
            return _cache = Parse(json, filePath);
        }
        catch (Exception ex)
        {
            LogReadError(ex);
            return new Dictionary<string, object>();
        }
    }

    // A corrupt file stays corrupt until the next save replaces it, so it is cached as empty; a read that failed
    // (the file held by another process) is not cached and is tried again.
    private Dictionary<string, object> Parse(string? json, string filePath)
    {
        if (json == null)
        {
            _logService.Log(LogLevel.Info, $"User preferences file does not exist at '{filePath}', returning empty preferences");
            return new Dictionary<string, object>();
        }

        if (json.Length == 0)
        {
            _logService.Log(LogLevel.Warning, "Preferences file exists but is empty");
            return new Dictionary<string, object>();
        }

        Dictionary<string, object>? preferences;
        try
        {
            preferences = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch (JsonException ex)
        {
            _logService.Log(LogLevel.Error, $"Preferences file is not valid JSON, using empty preferences: {ex.Message}");
            return new Dictionary<string, object>();
        }

        if (preferences == null)
        {
            _logService.Log(LogLevel.Warning, "Deserialized preferences is null, returning empty dictionary");
            return new Dictionary<string, object>();
        }

        _logService.Log(LogLevel.Info, $"Loaded {preferences.Count} preferences from '{filePath}'");
        return preferences;
    }

    private void LogReadError(Exception ex)
    {
        _logService.Log(LogLevel.Error, $"Error getting user preferences: {ex.Message}");
        if (ex.InnerException != null)
        {
            _logService.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
        }
    }

    public async Task<OperationResult> SavePreferencesAsync(Dictionary<string, object> preferences)
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await SavePreferencesUnsafeAsync(preferences).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<OperationResult> SavePreferencesUnsafeAsync(Dictionary<string, object> preferences)
    {
        // Until the write is confirmed, the file may hold either version.
        _cache = null;
        try
        {
            string filePath = GetPreferencesFilePath();

            string json = JsonSerializer.Serialize(preferences, WriteOptions);

            string? directory = _fileSystemService.GetDirectoryName(filePath);
            if (directory != null && !_fileSystemService.DirectoryExists(directory))
            {
                _fileSystemService.CreateDirectory(directory);
            }

            await _fileSystemService.WriteAllTextAsync(filePath, json).ConfigureAwait(false);

            if (_fileSystemService.FileExists(filePath))
            {
                // Parsed back from what was written, so a read after a save sees the same JsonElement values a
                // fresh load would, not the caller's live objects.
                _cache = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                _logService.Log(LogLevel.Info, $"User preferences saved successfully to '{filePath}'");
                return OperationResult.Succeeded();
            }
            else
            {
                _logService.Log(LogLevel.Error, $"File not found after writing: '{filePath}'");
                return OperationResult.Failed($"File not found after writing: '{filePath}'");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error saving user preferences: {ex.Message}");
            if (ex.InnerException != null)
            {
                _logService.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
            }
            return OperationResult.Failed(ex.Message, ex);
        }
    }

    public async Task<T> GetPreferenceAsync<T>(string key, T defaultValue)
    {
        object? value;
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!(await LoadUnsafeAsync().ConfigureAwait(false)).TryGetValue(key, out value))
                return defaultValue;
        }
        finally
        {
            _fileLock.Release();
        }

        return ConvertValue(key, value, defaultValue);
    }

    public async Task<OperationResult> SetPreferenceAsync<T>(string key, T value)
    {
        // Hold the file lock across the whole read-modify-write so concurrent
        // SetPreferenceAsync calls don't clobber each other's writes.
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var preferences = new Dictionary<string, object>(await LoadUnsafeAsync().ConfigureAwait(false));

            preferences[key] = value!;

            var result = await SavePreferencesUnsafeAsync(preferences).ConfigureAwait(false);

            if (result.Success)
            {
                _logService.Log(LogLevel.Info, $"Successfully saved preference '{key}'");
            }
            else
            {
                _logService.Log(LogLevel.Error, $"Failed to save preference '{key}'");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error setting preference '{key}': {ex.Message}");
            return OperationResult.Failed(ex.Message, ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    // Same lock as the writers: File.WriteAllTextAsync does not share the file for reading, so an unlocked read
    // during a save throws "being used by another process" and quietly hands back the default.
    public T GetPreference<T>(string key, T defaultValue)
    {
        object? value;
        _fileLock.Wait();
        try
        {
            if (!LoadUnsafe().TryGetValue(key, out value))
                return defaultValue;
        }
        finally
        {
            _fileLock.Release();
        }

        return ConvertValue(key, value, defaultValue);
    }

    private T ConvertValue<T>(string key, object? value, T defaultValue)
    {
        try
        {
            if (value is T typedValue)
            {
                return typedValue;
            }

            if (typeof(T) == typeof(bool) && value != null)
            {
                string valueStr = value.ToString()?.ToLowerInvariant() ?? string.Empty;
                if (valueStr == "true" || valueStr == "1")
                    return (T)(object)true;
                if (valueStr == "false" || valueStr == "0")
                    return (T)(object)false;
            }

            if (value is JsonElement je)
            {
                if (typeof(T) == typeof(bool))
                {
                    if (je.ValueKind == JsonValueKind.True)
                        return (T)(object)true;
                    if (je.ValueKind == JsonValueKind.False)
                        return (T)(object)false;
                    if (je.ValueKind == JsonValueKind.String)
                    {
                        string strValue = je.GetString() ?? "";
                        if (bool.TryParse(strValue, out bool boolResult))
                            return (T)(object)boolResult;
                        if (strValue == "1")
                            return (T)(object)true;
                        if (strValue == "0")
                            return (T)(object)false;
                    }
                    if (je.ValueKind == JsonValueKind.Number)
                    {
                        return (T)(object)(je.GetDouble() != 0);
                    }
                }

                var deserialized = je.Deserialize<T>();
                if (deserialized != null)
                    return deserialized;
            }

            if (value != null)
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error converting preference value for key '{key}': {ex.Message}");
        }

        return defaultValue;
    }
}
