using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>
/// Service for managing user preferences stored as JSON.
/// </summary>
public class UserPreferencesService : IUserPreferencesService
{
    private const string PreferencesFileName = "UserPreferences.json";
    private readonly ILogService _logService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IFileSystemService _fileSystemService;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    public UserPreferencesService(ILogService logService, IInteractiveUserService interactiveUserService, IFileSystemService fileSystemService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _interactiveUserService = interactiveUserService ?? throw new ArgumentNullException(nameof(interactiveUserService));
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
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

            string appDataPath = _fileSystemService.CombinePath(localAppData, "Winhance", "Config");

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
        try
        {
            string filePath = GetPreferencesFilePath();

            if (!_fileSystemService.FileExists(filePath))
            {
                _logService.Log(LogLevel.Info, $"User preferences file does not exist at '{filePath}', returning empty preferences");
                return new Dictionary<string, object>();
            }

            string json = await _fileSystemService.ReadAllTextAsync(filePath).ConfigureAwait(false);

            if (string.IsNullOrEmpty(json))
            {
                _logService.Log(LogLevel.Warning, "Preferences file exists but is empty");
                return new Dictionary<string, object>();
            }

            var preferences = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (preferences != null)
            {
                _logService.Log(LogLevel.Info, $"Successfully loaded {preferences.Count} preferences");

                return preferences;
            }
            else
            {
                _logService.Log(LogLevel.Warning, "Deserialized preferences is null, returning empty dictionary");
                return new Dictionary<string, object>();
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error getting user preferences: {ex.Message}");
            if (ex.InnerException != null)
            {
                _logService.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
            }
            return new Dictionary<string, object>();
        }
    }

    public async Task<OperationResult> SavePreferencesAsync(Dictionary<string, object> preferences)
    {
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
        var preferences = await GetPreferencesAsync().ConfigureAwait(false);

        if (preferences.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                // Handle bool conversion from non-JsonElement types (raw strings/numbers)
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
                    var convertedValue = (T)Convert.ChangeType(value, typeof(T));
                    return convertedValue;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error converting preference value for key '{key}': {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logService.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                _logService.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
                return defaultValue;
            }
        }

        return defaultValue;
    }

    public async Task<OperationResult> SetPreferenceAsync<T>(string key, T value)
    {
        try
        {
            var preferences = await GetPreferencesAsync().ConfigureAwait(false);

            preferences[key] = value!;

            var result = await SavePreferencesAsync(preferences).ConfigureAwait(false);

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
    }

    /// <summary>
    /// Synchronous version for use during startup to avoid async deadlocks on UI thread.
    /// </summary>
    public T GetPreference<T>(string key, T defaultValue)
    {
        try
        {
            string filePath = GetPreferencesFilePath();

            if (!_fileSystemService.FileExists(filePath))
            {
                return defaultValue;
            }

            string json = _fileSystemService.ReadAllText(filePath);

            if (string.IsNullOrEmpty(json))
            {
                return defaultValue;
            }

            var preferences = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (preferences != null && preferences.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                // Handle bool conversion from non-JsonElement types (raw strings/numbers)
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
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error getting preference '{key}' synchronously: {ex.Message}");
        }

        return defaultValue;
    }
}
