using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Models
{
    /// <summary>
    /// Base implementation of feature descriptor.
    /// Provides common functionality for all feature descriptors.
    /// </summary>
    public abstract class BaseFeatureDescriptor : IFeatureDescriptor
    {
        protected BaseFeatureDescriptor(
            string moduleId,
            string displayName,
            string category,
            int sortOrder,
            Type domainServiceType,
            string description = "")
        {
            ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Category = category ?? throw new ArgumentNullException(nameof(category));
            SortOrder = sortOrder;
            DomainServiceType = domainServiceType ?? throw new ArgumentNullException(nameof(domainServiceType));
            Description = description ?? string.Empty;
        }

        /// <summary>
        /// Gets the unique identifier for this feature module.
        /// </summary>
        public string ModuleId { get; }

        /// <summary>
        /// Gets the display name for this feature module.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the category this feature belongs to.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the sort order for displaying this feature module.
        /// </summary>
        public int SortOrder { get; }

        /// <summary>
        /// Gets the type of the domain service that handles this feature's business logic.
        /// </summary>
        public Type DomainServiceType { get; }

        /// <summary>
        /// Gets an optional description of what this feature does.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Determines if this feature module is available on the current system.
        /// Default implementation returns true. Override for specific availability checks.
        /// </summary>
        /// <returns>True if the feature is available; otherwise, false.</returns>
        public virtual Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(true);
        }
    }
}
