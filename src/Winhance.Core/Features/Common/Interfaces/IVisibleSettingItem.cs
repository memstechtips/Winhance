using System;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for setting items that can be shown or hidden in the UI.
    /// </summary>
    public interface IVisibleSettingItem
    {
        /// <summary>
        /// Gets or sets a value indicating whether this setting item is visible in the UI.
        /// </summary>
        bool IsVisible { get; set; }
    }
}
