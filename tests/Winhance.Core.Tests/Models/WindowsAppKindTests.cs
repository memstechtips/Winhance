using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class WindowsAppKindTests
{
    public static TheoryData<string> WindowsItemIds()
    {
        var data = new TheoryData<string>();
        foreach (var item in WindowsItems())
            data.Add(item.Id);
        return data;
    }

    private static IEnumerable<ItemDefinition> WindowsItems() =>
        WindowsAppDefinitions.GetWindowsApps().Items
            .Concat(CapabilityDefinitions.GetWindowsCapabilities().Items)
            .Concat(OptionalFeatureDefinitions.GetWindowsOptionalFeatures().Items);

    [Theory]
    [MemberData(nameof(WindowsItemIds))]
    public void Kind_MatchesWhatTheItemIsRemovedAs(string id)
    {
        var item = WindowsItems().Single(i => i.Id == id);
        WindowsAppKind expected =
            !string.IsNullOrEmpty(item.CapabilityName) ? WindowsAppKind.Capability
            : !string.IsNullOrEmpty(item.OptionalFeatureName) ? WindowsAppKind.OptionalFeature
            : item.AppxPackageName?.Length > 0 ? WindowsAppKind.AppxPackage
            : WindowsAppKind.DesktopApp;

        item.Kind.Should().Be(expected);
    }
}
