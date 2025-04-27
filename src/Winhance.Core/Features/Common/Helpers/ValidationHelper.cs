using System;

namespace Winhance.Core.Features.Common.Helpers
{
    /// <summary>
    /// Helper class for common validation operations.
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// Validates that the specified object is not null.
        /// </summary>
        /// <param name="value">The object to validate.</param>
        /// <param name="paramName">The name of the parameter being validated.</param>
        /// <exception cref="ArgumentNullException">Thrown if the value is null.</exception>
        public static void NotNull(object value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        /// <summary>
        /// Validates that the specified string is not null or empty.
        /// </summary>
        /// <param name="value">The string to validate.</param>
        /// <param name="paramName">The name of the parameter being validated.</param>
        /// <exception cref="ArgumentNullException">Thrown if the value is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the value is empty.</exception>
        public static void NotNullOrEmpty(string value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be empty.", paramName);
            }
        }

        /// <summary>
        /// Validates that the specified string is not null, empty, or consists only of white-space characters.
        /// </summary>
        /// <param name="value">The string to validate.</param>
        /// <param name="paramName">The name of the parameter being validated.</param>
        /// <exception cref="ArgumentNullException">Thrown if the value is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the value is empty or consists only of white-space characters.</exception>
        public static void NotNullOrWhiteSpace(string value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty or consist only of white-space characters.", paramName);
            }
        }

        /// <summary>
        /// Validates that the specified value is greater than or equal to the minimum value.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="minValue">The minimum allowed value.</param>
        /// <param name="paramName">The name of the parameter being validated.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the value is less than the minimum value.</exception>
        public static void GreaterThanOrEqualTo(int value, int minValue, string paramName)
        {
            if (value < minValue)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"Value must be greater than or equal to {minValue}.");
            }
        }

        /// <summary>
        /// Validates that the specified value is less than or equal to the maximum value.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="maxValue">The maximum allowed value.</param>
        /// <param name="paramName">The name of the parameter being validated.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the value is greater than the maximum value.</exception>
        public static void LessThanOrEqualTo(int value, int maxValue, string paramName)
        {
            if (value > maxValue)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"Value must be less than or equal to {maxValue}.");
            }
        }

        /// <summary>
        /// Validates that the specified value is in the specified range.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="minValue">The minimum allowed value.</param>
        /// <param name="maxValue">The maximum allowed value.</param>
        /// <param name="paramName">The name of the parameter being validated.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the value is outside the specified range.</exception>
        public static void InRange(int value, int minValue, int maxValue, string paramName)
        {
            if (value < minValue || value > maxValue)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"Value must be between {minValue} and {maxValue}.");
            }
        }
    }
}
