using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Extensions
{
    /// <summary>
    /// Extension methods for RegistrySetting.
    /// </summary>
    public static class RegistrySettingExtensions
    {
        /// <summary>
        /// Determines if the registry setting is for HttpAcceptLanguageOptOut.
        /// </summary>
        /// <param name="setting">The registry setting to check.</param>
        /// <returns>True if the setting is for HttpAcceptLanguageOptOut; otherwise, false.</returns>
        public static bool IsHttpAcceptLanguageOptOut(this RegistrySetting setting)
        {
            if (setting == null)
                return false;

            return setting.SubKey == "Control Panel\\International\\User Profile" &&
                   setting.Name == "HttpAcceptLanguageOptOut";
        }

        /// <summary>
        /// Determines if the registry setting requires special handling.
        /// </summary>
        /// <param name="setting">The registry setting to check.</param>
        /// <returns>True if the setting requires special handling; otherwise, false.</returns>
        public static bool RequiresSpecialHandling(this RegistrySetting setting)
        {
            if (setting == null)
                return false;

            // Currently, only HttpAcceptLanguageOptOut requires special handling
            return IsHttpAcceptLanguageOptOut(setting);
        }

        /// <summary>
        /// Applies special handling for the registry setting.
        /// </summary>
        /// <param name="setting">The registry setting to apply special handling for.</param>
        /// <param name="registryService">The registry service to use.</param>
        /// <param name="isEnabled">Whether the setting is being enabled or disabled.</param>
        /// <returns>True if the special handling was applied successfully; otherwise, false.</returns>
        public static bool ApplySpecialHandling(this RegistrySetting setting, IRegistryService registryService, bool isEnabled)
        {
            if (setting == null || registryService == null)
                return false;

            if (IsHttpAcceptLanguageOptOut(setting) && isEnabled && setting.EnabledValue != null &&
                ((setting.EnabledValue is int intValue && intValue == 0) ||
                 (setting.EnabledValue is string strValue && strValue == "0")))
            {
                // When enabling language list access, Windows deletes the key entirely
                return registryService.DeleteValue(
                    $"{setting.Hive}\\{setting.SubKey}",
                    setting.Name);
            }

            // No special handling needed or applicable
            return false;
        }


    }
}
