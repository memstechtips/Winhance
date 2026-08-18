using System.Reflection;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>SettingIds exists so service logic checks setting identity through a named constant instead of a raw
/// string literal. A constant only earns that job while its value still names a real catalog setting, so this pins
/// every constant to SettingCatalog.All: rename a catalog id and the constant is silently orphaned, leaving the
/// service comparing against a value nothing matches -- a live bug no compiler can catch. The constants are
/// enumerated by reflection so a newly added one is covered without anyone remembering to update this test.</summary>
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

        // Non-vacuity guard: a broken reflection query must not pass by discovering nothing.
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
