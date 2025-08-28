using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Defines the common properties that all setting items should have.
    /// Represents the data model contract without UI state.
    /// </summary>
    public interface ISettingItem
    {
        /// <summary>
        /// Gets the unique identifier for the setting.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the name of the setting.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the setting.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the group name that this setting belongs to.
        /// </summary>
        string GroupName { get; }

        /// <summary>
        /// Gets the type of input used for this setting.
        /// </summary>
        SettingInputType InputType { get; }

        /// <summary>
        /// Gets the dependencies for this setting.
        /// </summary>
        List<SettingDependency> Dependencies { get; }

    }
}
