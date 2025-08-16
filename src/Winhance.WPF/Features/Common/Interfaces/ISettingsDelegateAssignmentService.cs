using System;
using System.Threading.Tasks;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Interfaces
{
    /// <summary>
    /// Service responsible for assigning event delegates to SettingUIItem controls.
    /// Follows SRP by handling only delegate assignment logic.
    /// </summary>
    public interface ISettingsDelegateAssignmentService
    {
        /// <summary>
        /// Assigns appropriate delegates to a SettingUIItem based on its control type.
        /// </summary>
        /// <param name="item">The SettingUIItem to assign delegates to</param>
        /// <param name="onToggleChange">Handler for binary toggle changes</param>
        /// <param name="onValueChange">Handler for value changes (ComboBox, NumericUpDown, Slider)</param>
        void AssignDelegates(
            SettingUIItem item, 
            Func<string, bool, Task> onToggleChange, 
            Func<string, object?, Task> onValueChange);

        /// <summary>
        /// Clears all delegates from a SettingUIItem.
        /// </summary>
        /// <param name="item">The SettingUIItem to clear delegates from</param>
        void ClearDelegates(SettingUIItem item);
    }
}
