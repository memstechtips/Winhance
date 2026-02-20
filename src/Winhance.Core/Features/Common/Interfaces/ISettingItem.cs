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
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string? GroupName { get; }
        InputType InputType { get; }
        List<SettingDependency> Dependencies { get; }

    }
}
