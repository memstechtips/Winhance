namespace Winhance.Core.Features.Common.Enums
{
    /// <summary>
    /// Enum representing the import options.
    /// </summary>
    public enum ImportOption
    {
        /// <summary>
        /// No option selected.
        /// </summary>
        None,

        /// <summary>
        /// Import own configuration.
        /// </summary>
        ImportOwn,

        /// <summary>
        /// Import recommended configuration.
        /// </summary>
        ImportRecommended,

        /// <summary>
        /// Import user backup configuration.
        /// </summary>
        ImportBackup,

        /// <summary>
        /// Import Windows defaults configuration.
        /// </summary>
        ImportWindowsDefaults
    }
}
