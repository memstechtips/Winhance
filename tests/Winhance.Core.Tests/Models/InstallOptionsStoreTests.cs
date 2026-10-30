using System.Text;
using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class InstallOptionsStoreTests
{
    private static InstallOptionsStore Deserialize(string json) => JsonSerializer.Deserialize<InstallOptionsStore>(json)!;

    private static string Argument(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Deserialize_NullApps_FallsBackToNoPerAppOptions()
    {
        var store = Deserialize("""{"Apps":null}""");

        store.Apps.Should().BeEmpty();
        store.For(["any"]).Should().BeNull();
    }

    [Fact]
    public void Deserialize_NullDefaults_FallsBackToTheInstallerDefaults()
    {
        var store = Deserialize("""{"Defaults":null}""");

        store.Defaults.Should().Be(InstallOptions.Default);
    }

    [Fact]
    public void Deserialize_NullDefaultsAndApps_KeepsWhatWasWritten()
    {
        var store = Deserialize("""{"Defaults":null,"Apps":{"a":{"Scope":2}}}""");

        store.Defaults.Should().Be(InstallOptions.Default);
        store.Apps["a"].Scope.Should().Be(InstallScope.Machine);
    }

    [Fact]
    public void FromArgument_NullCollections_ReturnsAUsableStore()
    {
        var store = InstallOptionsStore.FromArgument(Argument("""{"Defaults":null,"Apps":null}"""));

        store.Should().NotBeNull();
        store!.Defaults.Should().Be(InstallOptions.Default);
        store.Apps.Should().BeEmpty();
    }

    [Fact]
    public void With_NullApps_StaysUsable()
    {
        var store = new InstallOptionsStore() with { Apps = null! };

        store.Apps.Should().BeEmpty();
    }

    [Fact]
    public void ToArgument_ThenFromArgument_RoundTrips()
    {
        var store = new InstallOptionsStore
        {
            Defaults = new InstallOptions { Scope = InstallScope.Machine },
            Apps = new(StringComparer.Ordinal) { ["a"] = new InstallOptions { Interactive = true } },
        };

        var back = InstallOptionsStore.FromArgument(store.ToArgument());

        back!.Defaults.Should().Be(store.Defaults);
        back.Apps.Should().BeEquivalentTo(store.Apps);
    }
}
