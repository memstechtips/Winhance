using FluentAssertions;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Xunit;

namespace Winhance.Core.Tests.Utilities;

public class AppRemovalPlanTests
{
    private static AppChoice Choice(string id, string[]? packages = null, string? capability = null, string? feature = null) =>
        new(id, id, packages, capability, feature, null);

    [Fact]
    public void For_DedicatedAppAlsoCarryingACapabilityAndPackage_GoesToDedicatedOnly()
    {
        var plan = AppRemovalPlan.For([Choice("windows-app-onedrive", ["Microsoft.OneDriveSync"], "Some.Capability~~~~", "SomeFeature")]);

        plan.Dedicated.Should().ContainSingle().Which.RemovalScriptName.Should().Be(OneDriveRemovalScript.ScriptName);
        plan.Packages.Should().BeEmpty();
        plan.Capabilities.Should().BeEmpty();
        plan.Features.Should().BeEmpty();
    }

    [Fact]
    public void For_CapabilityFeatureAndPackageApps_SplitInThatPrecedence()
    {
        var plan = AppRemovalPlan.For([
            Choice("a", ["Pkg.A"], capability: "Cap.A", feature: "Feat.A"),
            Choice("b", ["Pkg.B"], feature: "Feat.B"),
            Choice("c", ["Pkg.C1", "Pkg.C2"])]);

        plan.Capabilities.Should().Equal("Cap.A");
        plan.Features.Should().Equal("Feat.B");
        plan.Packages.Should().Equal("Pkg.C1", "Pkg.C2");
    }

    [Fact]
    public void For_OneNotePackage_IsSpecialOnce()
    {
        var plan = AppRemovalPlan.For([
            Choice("windows-app-onenote", ["Microsoft.Office.OneNote"]),
            Choice("other-onenote", ["Microsoft.OneNote.Other"])]);

        plan.SpecialApps.Should().Equal("OneNote");
        plan.Packages.Should().Equal("Microsoft.Office.OneNote", "Microsoft.OneNote.Other");
    }

    [Fact]
    public void For_NoOneNote_HasNoSpecialApps()
    {
        AppRemovalPlan.For([Choice("a", ["Pkg.A"])]).SpecialApps.Should().BeEmpty();
    }

    [Fact]
    public void For_EdgeAndItsWebViewVariant_RunTheScriptOnceButKeepEdgesPackage()
    {
        var plan = AppRemovalPlan.For([Choice("windows-app-edge"), Choice("windows-app-edge-webview")]);

        plan.Dedicated.Should().ContainSingle().Which.Id.Should().Be("windows-app-edge-webview");
        plan.DedicatedPackages.Should().Equal("Microsoft.MicrosoftEdge.Stable");
        plan.Packages.Should().BeEmpty();
    }

    [Fact]
    public void For_DedicatedAppsSelectedOutOfOrder_FollowCatalogOrder()
    {
        var plan = AppRemovalPlan.For([Choice("windows-app-onedrive"), Choice("windows-app-edge")]);

        plan.Dedicated.Select(d => d.RemovalScriptName).Should().Equal(EdgeRemovalScript.ScriptName, OneDriveRemovalScript.ScriptName);
    }

    [Fact]
    public void For_NoApps_IsEmpty()
    {
        var plan = AppRemovalPlan.For([]);

        plan.Packages.Should().BeEmpty();
        plan.Capabilities.Should().BeEmpty();
        plan.Features.Should().BeEmpty();
        plan.SpecialApps.Should().BeEmpty();
        plan.Dedicated.Should().BeEmpty();
        plan.DedicatedPackages.Should().BeEmpty();
    }
}
