using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerSettingsValidationService
{
    Task<IEnumerable<SettingDefinition>> FilterSettingsByExistenceAsync(IEnumerable<SettingDefinition> settings);
}