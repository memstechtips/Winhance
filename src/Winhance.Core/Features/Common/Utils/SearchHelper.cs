using System;
using System.Linq;

namespace Winhance.Core.Features.Common.Utils;

public static class SearchHelper
{
    public static bool MatchesSearchTerm(string searchTerm, params string[] searchableFields)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return true;

        var searchTerms = searchTerm.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return searchTerms.All(term =>
            searchableFields.Any(field =>
                field?.ToLowerInvariant().Contains(term) == true));
    }
}
