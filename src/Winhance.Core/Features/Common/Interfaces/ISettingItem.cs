using System.Collections.Generic;
using System.Windows.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Defines the common properties that all setting items should have.
    /// </summary>
    public interface ISettingItem
    {
        /// <summary>
        /// Gets or sets the unique identifier for the setting.
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the setting.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the setting.
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the setting is selected.
        /// </summary>
        bool IsSelected { get; set; }

        /// <summary>
        /// Gets or sets the group name that this setting belongs to.
        /// </summary>
        string GroupName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the setting is visible in the UI.
        /// </summary>
        bool IsVisible { get; set; }

        /// <summary>
        /// Gets or sets the type of control used for this setting.
        /// </summary>
        ControlType ControlType { get; set; }

        /// <summary>
        /// Gets or sets the selected value for ComboBox controls.
        /// </summary>
        object? SelectedValue { get; set; }

        /// <summary>
        /// Gets or sets the dependencies for this setting.
        /// </summary>
        List<SettingDependency> Dependencies { get; set; }

        /// <summary>
        /// Gets the command to apply the setting.
        /// </summary>
        ICommand ApplySettingCommand { get; }
    }
}
