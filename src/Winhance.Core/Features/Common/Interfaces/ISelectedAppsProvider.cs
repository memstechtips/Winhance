using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

// Keeps the WIM feature from coupling to the SoftwareApps ViewModel.
public interface ISelectedAppsProvider
{
    Task<IReadOnlyList<ConfigurationItem>> GetSelectedWindowsAppsAsync();
}
