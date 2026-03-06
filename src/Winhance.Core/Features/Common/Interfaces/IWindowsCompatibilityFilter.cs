using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IWindowsCompatibilityFilter
{
    IEnumerable<SettingDefinition> FilterSettingsByWindowsVersion(
        IEnumerable<SettingDefinition> settings
    );

    IEnumerable<SettingDefinition> FilterSettingsByWindowsVersion(
        IEnumerable<SettingDefinition> settings,
        bool applyFilter
    );
}
