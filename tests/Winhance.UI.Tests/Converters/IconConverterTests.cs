using FluentAssertions;
using Winhance.UI.Features.Common.Converters;
using Xunit;

namespace Winhance.UI.Tests.Converters;

public class IconConverterTests
{
    private sealed class Item
    {
        public string? Icon { get; init; }
        public string? IconPack { get; init; }
    }

    private sealed class Other
    {
        public string Icon => "Cog";
    }

    [Fact]
    public void Resolve_EachType_ReadsThroughItsOwnProperties()
    {
        IconConverter.Resolve(new Item { Icon = "Shield", IconPack = "Fluent" }, null!).Should().Be(("Shield", "Fluent"));
        IconConverter.Resolve(new Other(), null!).Should().Be(("Cog", "Material"));
        IconConverter.Resolve(new Item { Icon = "Lock" }, null!).Should().Be(("Lock", "Material"));
    }

    [Fact]
    public void Resolve_ItemWithoutAnIcon_ReturnsNull()
    {
        IconConverter.Resolve(new Item(), null!).Should().BeNull();
        IconConverter.Resolve(new object(), null!).Should().BeNull();
    }

    [Fact]
    public void MaterialPathData_Always_MatchesAnyCasingAndSkipsUnknownNames()
    {
        var data = IconConverter.MaterialPathData("Cog");

        data.Should().NotBeNullOrEmpty();
        IconConverter.MaterialPathData("cog").Should().Be(data);
        IconConverter.MaterialPathData("NoSuchIcon").Should().BeNull();
    }
}
