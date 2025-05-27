using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service for managing UAC settings persistence.
    /// </summary>
    public class UacSettingsService : IUacSettingsService
    {
        private const string CustomUacSettingsKey = "CustomUacSettings";
        private readonly UserPreferencesService _userPreferencesService;
        private readonly ILogService _logService;
        
        // Cache for custom UAC settings during the current session
        private CustomUacSettings _cachedSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="UacSettingsService"/> class.
        /// </summary>
        /// <param name="userPreferencesService">The user preferences service.</param>
        /// <param name="logService">The log service.</param>
        public UacSettingsService(UserPreferencesService userPreferencesService, ILogService logService)
        {
            _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Saves custom UAC settings.
        /// </summary>
        /// <param name="consentPromptValue">The ConsentPromptBehaviorAdmin registry value.</param>
        /// <param name="secureDesktopValue">The PromptOnSecureDesktop registry value.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SaveCustomUacSettingsAsync(int consentPromptValue, int secureDesktopValue)
        {
            try
            {
                // Create a CustomUacSettings object
                var settings = new CustomUacSettings(consentPromptValue, secureDesktopValue);
                
                // Cache the settings
                _cachedSettings = settings;
                
                // Save to user preferences
                await _userPreferencesService.SetPreferenceAsync(CustomUacSettingsKey, settings);
                
                _logService.Log(LogLevel.Info, 
                    $"Saved custom UAC settings: ConsentPrompt={consentPromptValue}, SecureDesktop={secureDesktopValue}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error saving custom UAC settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads custom UAC settings.
        /// </summary>
        /// <returns>A CustomUacSettings object if settings exist, null otherwise.</returns>
        public async Task<CustomUacSettings> LoadCustomUacSettingsAsync()
        {
            try
            {
                // Try to get from cache first
                if (_cachedSettings != null)
                {
                    return _cachedSettings;
                }
                
                // Try to load from preferences
                var settings = await _userPreferencesService.GetPreferenceAsync<CustomUacSettings>(CustomUacSettingsKey, default(CustomUacSettings));
                if (settings != null)
                {
                    // Cache the settings
                    _cachedSettings = settings;
                    return settings;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading custom UAC settings: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if custom UAC settings exist.
        /// </summary>
        /// <returns>True if custom settings exist, false otherwise.</returns>
        public async Task<bool> HasCustomUacSettingsAsync()
        {
            try
            {
                // Check cache first
                if (_cachedSettings != null)
                {
                    return true;
                }
                
                // Check user preferences
                var settings = await _userPreferencesService.GetPreferenceAsync<CustomUacSettings>(CustomUacSettingsKey, null);
                return settings != null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking for custom UAC settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets custom UAC settings if they exist.
        /// </summary>
        /// <param name="consentPromptValue">The ConsentPromptBehaviorAdmin registry value.</param>
        /// <param name="secureDesktopValue">The PromptOnSecureDesktop registry value.</param>
        /// <returns>True if custom settings were retrieved, false otherwise.</returns>
        public bool TryGetCustomUacValues(out int consentPromptValue, out int secureDesktopValue)
        {
            // Initialize out parameters
            consentPromptValue = 0;
            secureDesktopValue = 0;
            
            try
            {
                // Check cache first
                if (_cachedSettings != null)
                {
                    consentPromptValue = _cachedSettings.ConsentPromptValue;
                    secureDesktopValue = _cachedSettings.SecureDesktopValue;
                    return true;
                }
                
                // Try to load from preferences (completely synchronously)
                string preferencesFilePath = GetPreferencesFilePath();
                if (File.Exists(preferencesFilePath))
                {
                    try
                    {
                        string json = File.ReadAllText(preferencesFilePath);
                        var preferences = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        
                        if (preferences != null && preferences.ContainsKey(CustomUacSettingsKey))
                        {
                            var settingsToken = preferences[CustomUacSettingsKey];
                            var settings = JsonConvert.DeserializeObject<CustomUacSettings>(settingsToken.ToString());
                            
                            if (settings != null)
                            {
                                // Cache the settings
                                _cachedSettings = settings;
                                
                                consentPromptValue = settings.ConsentPromptValue;
                                secureDesktopValue = settings.SecureDesktopValue;
                                return true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Error, $"Error reading preferences file: {ex.Message}");
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting custom UAC values: {ex.Message}");

                return false;
            }
        }
        
        /// <summary>
        /// Gets the path to the user preferences file.
        /// </summary>
        /// <returns>The full path to the user preferences file.</returns>
        private string GetPreferencesFilePath()
        {
            // Get the LocalApplicationData folder (e.g., C:\Users\Username\AppData\Local)
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            
            // Combine with Winhance/Config
            string appDataPath = Path.Combine(localAppData, "Winhance", "Config");
            
            // Ensure the directory exists
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            // Return the full path to the preferences file
            return Path.Combine(appDataPath, "UserPreferences.json");
        }
    }
}
