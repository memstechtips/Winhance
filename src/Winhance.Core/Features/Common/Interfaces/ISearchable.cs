using System;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for objects that can be searched.
    /// </summary>
    public interface ISearchable
    {
        /// <summary>
        /// Determines if the object matches the given search term.
        /// </summary>
        /// <param name="searchTerm">The search term to match against.</param>
        /// <returns>True if the object matches the search term, false otherwise.</returns>
        bool MatchesSearch(string searchTerm);

        /// <summary>
        /// Gets the searchable properties of the object.
        /// </summary>
        /// <returns>An array of property names that should be searched.</returns>
        string[] GetSearchableProperties();
    }
}
