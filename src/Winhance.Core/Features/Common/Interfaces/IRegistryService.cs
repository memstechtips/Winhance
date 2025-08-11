using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Facade interface kept for backward compatibility. It now composes focused interfaces
    /// and only exposes high-level orchestration methods used by callers.
    /// </summary>
    public interface IRegistryService : IRegistryReader, IRegistryWriter, IRegistryStatus
    {
        // High-level apply operations preserved for domain callers
        Task<bool> ApplySettingAsync(RegistrySetting setting, bool enable);
        Task<bool> ApplyLinkedSettingsAsync(LinkedRegistrySettings linkedSettings, bool enable);
    }
}
