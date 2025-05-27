namespace Winhance.Core.Models.Enums
{
    /// <summary>
    /// Represents User Account Control (UAC) levels in Windows.
    /// </summary>
    public enum UacLevel
    {
        /// <summary>
        /// Never notify the user when programs try to install software or make changes to the computer.
        /// </summary>
        NeverNotify = 0,

        /// <summary>
        /// Notify the user only when programs try to make changes to the computer, without dimming the desktop.
        /// </summary>
        NotifyNoDesktopDim = 1,

        /// <summary>
        /// Notify the user only when programs try to make changes to the computer (default).
        /// </summary>
        NotifyChangesOnly = 2,

        /// <summary>
        /// Always notify the user when programs try to install software or make changes to the computer
        /// or when the user makes changes to Windows settings.
        /// </summary>
        AlwaysNotify = 3,

        /// <summary>
        /// Custom UAC setting that doesn't match any of the standard Windows GUI options.
        /// This is used when the registry contains values that don't match the standard options.
        /// </summary>
        Custom = 99,
    }
}
