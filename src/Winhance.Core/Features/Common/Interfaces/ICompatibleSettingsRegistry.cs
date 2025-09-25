using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface ICompatibleSettingsRegistry
    {
        Task InitializeAsync();
        IEnumerable<SettingDefinition> GetFilteredSettings(string featureId);
        bool IsInitialized { get; }
    }
}