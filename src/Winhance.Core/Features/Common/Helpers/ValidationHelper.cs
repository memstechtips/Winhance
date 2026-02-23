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

    }
}
