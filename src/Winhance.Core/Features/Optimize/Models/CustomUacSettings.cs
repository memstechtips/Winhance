using System;
using Newtonsoft.Json;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Represents custom User Account Control (UAC) settings that don't match standard Windows GUI options.
    /// </summary>
    [Serializable]
    public class CustomUacSettings
    {
        /// <summary>
        /// Gets or sets the ConsentPromptBehaviorAdmin registry value.
        /// </summary>
        [JsonProperty("ConsentPromptValue")]
        public int ConsentPromptValue { get; set; }

        /// <summary>
        /// Gets or sets the PromptOnSecureDesktop registry value.
        /// </summary>
        [JsonProperty("SecureDesktopValue")]
        public int SecureDesktopValue { get; set; }

        /// <summary>
        /// Gets or sets a timestamp when these settings were last detected or saved.
        /// </summary>
        [JsonProperty("LastUpdated")]
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomUacSettings"/> class.
        /// </summary>
        public CustomUacSettings()
        {
            // Default constructor for serialization
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomUacSettings"/> class with specific values.
        /// </summary>
        /// <param name="consentPromptValue">The ConsentPromptBehaviorAdmin registry value.</param>
        /// <param name="secureDesktopValue">The PromptOnSecureDesktop registry value.</param>
        public CustomUacSettings(int consentPromptValue, int secureDesktopValue)
        {
            ConsentPromptValue = consentPromptValue;
            SecureDesktopValue = secureDesktopValue;
            LastUpdated = DateTime.Now;
        }
    }
}
