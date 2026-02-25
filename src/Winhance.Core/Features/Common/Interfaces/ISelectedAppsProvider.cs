using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Provides access to the currently selected Windows apps without coupling
/// the WIM feature directly to the SoftwareApps feature's ViewModel.
/// </summary>
public interface ISelectedAppsProvider
{
    Task<IReadOnlyList<ConfigurationItem>> GetSelectedWindowsAppsAsync();
}
