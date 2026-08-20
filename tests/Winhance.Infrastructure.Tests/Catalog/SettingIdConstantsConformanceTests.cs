using System.Reflection;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// Rename a catalog id and a SettingIds constant is silently orphaned - the service compares against a value
// nothing matches. Enumerated by reflection so a new constant is covered automatically.
public class SettingIdConstantsConformanceTests
{
    private static IReadOnlyList<FieldInfo> SettingIdConstants() =>
        typeof(SettingIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .OrderBy(f => f.Name)
            .ToArray();

    [Fact]
    public void Every_SettingIds_constant_resolves_to_a_catalog_setting()
    {
        var constants = SettingIdConstants();

        Assert.True(
            constants.Count >= 6,
            $"Expected at least 6 public const string fields on SettingIds, found {constants.Count} -- "
                + "the reflection query is broken, so this test proves nothing.");

        var catalogIds = SettingCatalog.All.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var field in constants)
        {
            var value = (string)field.GetRawConstantValue()!;
            Assert.True(
                catalogIds.Contains(value),
                $"SettingIds.{field.Name} = '{value}' does not match any catalog setting id");
        }
    }
}
