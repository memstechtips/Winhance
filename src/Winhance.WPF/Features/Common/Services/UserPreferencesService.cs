using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services
{
    public class UserPreferencesService : IUserPreferencesService
    {
        private const string PreferencesFileName = "UserPreferences.json";
        private readonly ILogService _logService;

        public UserPreferencesService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Gets the path to the user preferences file
        /// </summary>
        /// <returns>The full path to the user preferences file</returns>
        private string GetPreferencesFilePath()
        {
            try
            {
                // Get the LocalApplicationData folder (e.g., C:\Users\Username\AppData\Local)
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                if (string.IsNullOrEmpty(localAppData))
                {
                    _logService.Log(LogLevel.Error, "LocalApplicationData folder path is empty");
                    // Fallback to a default path
                    localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local");
                    _logService.Log(LogLevel.Info, $"Using fallback path: {localAppData}");
                }

                // Combine with Winhance/Config
                string appDataPath = Path.Combine(localAppData, "Winhance", "Config");

                // Ensure the directory exists
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                    _logService.Log(LogLevel.Info, $"Created preferences directory: {appDataPath}");
                }

                // Get the full file path
                string filePath = Path.Combine(appDataPath, PreferencesFileName);

                return filePath;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting preferences file path: {ex.Message}");

                // Fallback to a temporary file
                string tempPath = Path.Combine(Path.GetTempPath(), "Winhance", "Config");
                Directory.CreateDirectory(tempPath);
                string tempFilePath = Path.Combine(tempPath, PreferencesFileName);

                _logService.Log(LogLevel.Warning, $"Using fallback temporary path: {tempFilePath}");
                return tempFilePath;
            }
        }

        /// <summary>
        /// Gets the user preferences
        /// </summary>
        /// <returns>A dictionary containing the user preferences</returns>
        public async Task<Dictionary<string, object>> GetPreferencesAsync()
        {
            try
            {
                string filePath = GetPreferencesFilePath();

                if (!File.Exists(filePath))
                {
                    _logService.Log(LogLevel.Info, $"User preferences file does not exist at '{filePath}', returning empty preferences");
                    return new Dictionary<string, object>();
                }

                // Read the file
                string json = await File.ReadAllTextAsync(filePath);

                if (string.IsNullOrEmpty(json))
                {
                    _logService.Log(LogLevel.Warning, "Preferences file exists but is empty");
                    return new Dictionary<string, object>();
                }

                // Use more robust deserialization settings
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    TypeNameHandling = TypeNameHandling.None,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                // Use a custom converter to properly handle boolean values
                var preferences = JsonConvert.DeserializeObject<Dictionary<string, object>>(json, settings);

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

        /// <summary>
        /// Saves the user preferences
        /// </summary>
        /// <param name="preferences">The preferences to save</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SavePreferencesAsync(Dictionary<string, object> preferences)
        {
            try
            {
                string filePath = GetPreferencesFilePath();

                // Use more robust serialization settings
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include,
                    TypeNameHandling = TypeNameHandling.None,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                string json = JsonConvert.SerializeObject(preferences, settings);

                // Ensure the directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write the file
                await File.WriteAllTextAsync(filePath, json);

                // Verify the file was written
                if (File.Exists(filePath))
                {
                    _logService.Log(LogLevel.Info, $"User preferences saved successfully to '{filePath}'");
                    return true;
                }
                else
                {
                    _logService.Log(LogLevel.Error, $"File not found after writing: '{filePath}'");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error saving user preferences: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logService.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Gets a specific preference value
        /// </summary>
        /// <typeparam name="T">The type of the preference value</typeparam>
        /// <param name="key">The preference key</param>
        /// <param name="defaultValue">The default value to return if the preference does not exist</param>
        /// <returns>The preference value, or the default value if not found</returns>
        public async Task<T> GetPreferenceAsync<T>(string key, T defaultValue)
        {
            var preferences = await GetPreferencesAsync();

            if (preferences.TryGetValue(key, out var value))
            {
                try
                {
                    // Special case for DontShowSupport boolean preference
                    if (key == "DontShowSupport" && typeof(T) == typeof(bool))
                    {
                        // Direct string comparison for known boolean values
                        if (value != null)
                        {
                            string valueStr = value.ToString().ToLowerInvariant();
                            if (valueStr == "true" || valueStr == "1")
                            {
                                return (T)(object)true;
                            }
                            else if (valueStr == "false" || valueStr == "0")
                            {
                                return (T)(object)false;
                            }
                        }
                    }

                    if (value is T typedValue)
                    {
                        return typedValue;
                    }

                    // Handle JToken conversion for primitive types
                    if (value is Newtonsoft.Json.Linq.JToken jToken)
                    {
                        // For boolean JToken values, handle them explicitly
                        if (typeof(T) == typeof(bool))
                        {
                            if (jToken.Type == Newtonsoft.Json.Linq.JTokenType.Boolean)
                            {
                                // Use ToObject instead of Value<T>
                                bool boolValue = (bool)jToken.ToObject(typeof(bool));
                                return (T)(object)boolValue;
                            }
                            else if (jToken.Type == Newtonsoft.Json.Linq.JTokenType.String)
                            {
                                // Try to parse string to boolean
                                string strValue = jToken.ToString();
                                if (bool.TryParse(strValue, out bool boolResult))
                                {
                                    return (T)(object)boolResult;
                                }
                                else if (strValue == "1")
                                {
                                    return (T)(object)true;
                                }
                                else if (strValue == "0")
                                {
                                    return (T)(object)false;
                                }
                            }
                            else if (jToken.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                            {
                                // Handle numeric representations (0 = false, non-zero = true)
                                double numValue = Convert.ToDouble(jToken.ToObject<object>());
                                bool boolValue = numValue != 0;
                                return (T)(object)boolValue;
                            }
                        }
                    }

                    // Try to convert the value to the requested type
                    var convertedValue = (T)Convert.ChangeType(value, typeof(T));
                    return convertedValue;
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

        /// <summary>
        /// Sets a specific preference value
        /// </summary>
        /// <typeparam name="T">The type of the preference value</typeparam>
        /// <param name="key">The preference key</param>
        /// <param name="value">The preference value</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SetPreferenceAsync<T>(string key, T value)
        {
            try
            {
                var preferences = await GetPreferencesAsync();

                preferences[key] = value;

                bool result = await SavePreferencesAsync(preferences);

                // Log the result
                if (result)
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
                return false;
            }
        }
    }
}