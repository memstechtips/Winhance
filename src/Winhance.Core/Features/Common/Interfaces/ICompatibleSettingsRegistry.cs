using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface ICompatibleSettingsRegistry
    {
        Task InitializeAsync();
        IEnumerable<SettingDefinition> GetFilteredSettings(string featureId);
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllFilteredSettings();
        IEnumerable<SettingDefinition> GetBypassedSettings(string featureId);
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllBypassedSettings();
        void SetFilterEnabled(bool enabled);
        bool IsInitialized { get; }
    }
}