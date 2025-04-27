using System;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents the result of a registry value test.
    /// </summary>
    public class RegistryTestResult
    {
        /// <summary>
        /// Gets or sets the registry key path.
        /// </summary>
        public string KeyPath { get; set; }

        /// <summary>
        /// Gets or sets the registry value name.
        /// </summary>
        public string ValueName { get; set; }

        /// <summary>
        /// Gets or sets the expected value.
        /// </summary>
        public object ExpectedValue { get; set; }

        /// <summary>
        /// Gets or sets the actual value.
        /// </summary>
        public object ActualValue { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the test was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the test result message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the category of the registry setting.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets the description of the registry setting.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets a formatted string representation of the test result.
        /// </summary>
        /// <returns>A formatted string representation of the test result.</returns>
        public override string ToString()
        {
            return $"{(IsSuccess ? "✓" : "✗")} {KeyPath}\\{ValueName}: {Message}";
        }
    }
}