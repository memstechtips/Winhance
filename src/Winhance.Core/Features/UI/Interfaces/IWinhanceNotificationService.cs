using System;

namespace Winhance.Core.Features.UI.Interfaces
{
    /// <summary>
    /// Interface for notification services that can display toast notifications and other alerts.
    /// </summary>
    public interface IWinhanceNotificationService
    {
        /// <summary>
        /// Shows a toast notification.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message content of the notification.</param>
        /// <param name="type">The type of notification.</param>
        void ShowToast(string title, string message, ToastType type);
    }

    /// <summary>
    /// Defines the types of toast notifications.
    /// </summary>
    public enum ToastType
    {
        /// <summary>
        /// Information notification.
        /// </summary>
        Information,

        /// <summary>
        /// Success notification.
        /// </summary>
        Success,

        /// <summary>
        /// Warning notification.
        /// </summary>
        Warning,

        /// <summary>
        /// Error notification.
        /// </summary>
        Error
    }
}
