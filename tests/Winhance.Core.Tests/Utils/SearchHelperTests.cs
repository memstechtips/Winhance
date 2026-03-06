using FluentAssertions;
using Winhance.Core.Features.Common.Utils;
using Xunit;

namespace Winhance.Core.Tests.Utils;

public class SearchHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void MatchesSearchTerm_NullOrWhitespaceSearch_ReturnsTrue(string? searchTerm)
    {
        SearchHelper.MatchesSearchTerm(searchTerm!, "anything").Should().BeTrue();
    }

    [Fact]
    public void MatchesSearchTerm_SingleWordMatch_ReturnsTrue()
    {
        SearchHelper.MatchesSearchTerm("power", "Power Settings", "General")
            .Should().BeTrue();
    }

    [Fact]
    public void MatchesSearchTerm_CaseInsensitive_ReturnsTrue()
    {
        SearchHelper.MatchesSearchTerm("POWER", "power settings")
            .Should().BeTrue();
    }

    [Fact]
    public void MatchesSearchTerm_PartialMatch_ReturnsTrue()
    {
        SearchHelper.MatchesSearchTerm("pow", "Power Settings")
            .Should().BeTrue();
    }

    [Fact]
    public void MatchesSearchTerm_MultiWordSearch_AllTermsMustMatch()
    {
        // Both "power" and "plan" must appear in at least one field each
        SearchHelper.MatchesSearchTerm("power plan", "Power Settings", "Active Plan")
            .Should().BeTrue();
    }

    [Fact]
    public void MatchesSearchTerm_MultiWordSearch_OneTermMissing_ReturnsFalse()
    {
        // "xyz" does not appear in any field
        SearchHelper.MatchesSearchTerm("power xyz", "Power Settings", "General")
            .Should().BeFalse();
    }

    [Fact]
    public void MatchesSearchTerm_NoMatchingFields_ReturnsFalse()
    {
        SearchHelper.MatchesSearchTerm("bluetooth", "Power Settings", "General")
            .Should().BeFalse();
    }

    [Fact]
    public void MatchesSearchTerm_NullFieldsInArray_HandledGracefully()
    {
        SearchHelper.MatchesSearchTerm("test", null!, "test value", null!)
            .Should().BeTrue();
    }

    [Fact]
    public void MatchesSearchTerm_AllNullFields_ReturnsFalse()
    {
        SearchHelper.MatchesSearchTerm("test", null!, null!)
            .Should().BeFalse();
    }

    [Fact]
    public void MatchesSearchTerm_EmptyFieldsArray_ReturnsFalse()
    {
        SearchHelper.MatchesSearchTerm("test")
            .Should().BeFalse();
    }

    [Fact]
    public void MatchesSearchTerm_MultipleSpacesInSearch_TreatedAsSingleSeparator()
    {
        SearchHelper.MatchesSearchTerm("power   plan", "Power Plan Settings")
            .Should().BeTrue();
    }

    [Fact]
    public void MatchesSearchTerm_TermInDifferentFields_ReturnsTrue()
    {
        // "dark" in first field, "theme" in second field
        SearchHelper.MatchesSearchTerm("dark theme", "Dark Mode", "Windows Theme")
            .Should().BeTrue();
    }
}
