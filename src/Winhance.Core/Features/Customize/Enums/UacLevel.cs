namespace Winhance.Core.Features.Customize.Enums
{
    /// <summary>
    /// Represents the User Account Control (UAC) security level.
    /// </summary>
    public enum UacLevel
    {
        /// <summary>
        /// Never notify (Low security). Registry value: 0.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Notify me only when apps try to make changes (Moderate security). Registry value: 5.
        /// </summary>
        Moderate = 1,

        /// <summary>
        /// Always notify (High security). Registry value: 2.
        /// </summary>
        High = 2
    }
}