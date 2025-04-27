namespace Winhance.Core.Features.Common.Enums
{
    /// <summary>
    /// Defines the types of PowerShell streams.
    /// </summary>
    public enum PowerShellStreamType
    {
        /// <summary>
        /// The output stream.
        /// </summary>
        Output,
        
        /// <summary>
        /// The error stream.
        /// </summary>
        Error,
        
        /// <summary>
        /// The warning stream.
        /// </summary>
        Warning,
        
        /// <summary>
        /// The verbose stream.
        /// </summary>
        Verbose,
        
        /// <summary>
        /// The debug stream.
        /// </summary>
        Debug,
        
        /// <summary>
        /// The information stream.
        /// </summary>
        Information,
        
        /// <summary>
        /// The progress stream.
        /// </summary>
        Progress
    }
}