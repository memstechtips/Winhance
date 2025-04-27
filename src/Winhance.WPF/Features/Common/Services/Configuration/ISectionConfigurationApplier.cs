using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Interface for services that apply configuration to specific sections.
    /// </summary>
    public interface ISectionConfigurationApplier
    {
        /// <summary>
        /// Gets the section name that this applier handles.
        /// </summary>
        string SectionName { get; }

        /// <summary>
        /// Applies the configuration to the section.
        /// </summary>
        /// <param name="configFile">The configuration file to apply.</param>
        /// <returns>True if any items were updated, false otherwise.</returns>
        Task<bool> ApplyConfigAsync(ConfigurationFile configFile);
    }
}