using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Xunit;

namespace Winhance.Core.Tests.Utilities;

public class DedicatedRemovalScriptsTests
{
    private static ItemDefinition Scripted(string id, string scriptName, params string[] alsoRemoves) => new()
    {
        Id = id,
        Name = id,
        Description = "",
        RemovalScript = () => id,
        RemovalScriptName = scriptName,
        AlsoRemoves = alsoRemoves.Length > 0 ? alsoRemoves : null,
    };

    [Fact]
    public void OnePerScript_ItemCoveredByAnotherSelectedItem_GetsNoRunOfItsOwn()
    {
        var edge = Scripted("edge", "EdgeRemoval");
        var webView = Scripted("webview", "EdgeRemoval", "edge");
        var oneDrive = Scripted("onedrive", "OneDriveRemoval");

        DedicatedRemovalScripts.OnePerScript([edge, webView, oneDrive]).Should().Equal(webView, oneDrive);
    }

    [Fact]
    public void OnePerScript_TwoItemsSharingAScript_RunItOnce()
    {
        var first = Scripted("first", "Shared");

        DedicatedRemovalScripts.OnePerScript([first, Scripted("second", "Shared")]).Should().Equal(first);
    }

    [Fact]
    public void OnePerScript_ItemsWithoutAScript_AreLeftOut()
    {
        var plain = new ItemDefinition { Id = "plain", Name = "plain", Description = "" };

        DedicatedRemovalScripts.OnePerScript([plain]).Should().BeEmpty();
    }
}
