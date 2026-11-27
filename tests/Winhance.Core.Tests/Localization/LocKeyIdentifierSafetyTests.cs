using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Localization;

// The key generator turns Setting_{id}_{part} into LocKey.Setting.{Pascal(id)}.{Pascal(part)}. The compiler
// enforces none of these: an id with an underscore would silently hand one setting's keys to another.
public class LocKeyIdentifierSafetyTests
{
    private static IEnumerable<string> CatalogIds() => SettingCatalog.All.Select(s => s.Id);

    [Fact]
    public void NoSettingId_ContainsAnUnderscore()
    {
        CatalogIds()
            .Where(id => id.Contains('_'))
            .Should().BeEmpty("the id is one '_'-delimited segment of every key built from it");
    }

    [Fact]
    public void EverySettingId_IsLowercaseKebab()
    {
        CatalogIds()
            .Where(id => !Regex.IsMatch(id, "^[a-z0-9]+(-[a-z0-9]+)*$"))
            .Should().BeEmpty("the generator maps each kebab segment to one PascalCase word, and drops "
                              + "any character that cannot sit in an identifier");
    }

    [Fact]
    public void NoTwoIds_ShareAPascalCaseIdentifier()
    {
        CatalogIds()
            .GroupBy(Pascal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Should().BeEmpty("each id must own its generated class");
    }

    private static string Pascal(string id) =>
        string.Concat(id.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
