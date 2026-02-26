using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Xunit;

namespace Winhance.Core.Tests.Utilities;

public class BloatRemovalScriptGeneratorTests
{
    [Fact]
    public void ScriptVersion_IsNotEmpty()
    {
        BloatRemovalScriptGenerator.ScriptVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateScript_EmptyInputs_ProducesValidScript()
    {
        var script = BloatRemovalScriptGenerator.GenerateScript(
            new List<string>(), new List<string>(),
            new List<string>(), new List<string>());

        script.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateScript_WithPackages_ContainsPackageNames()
    {
        var packages = new List<string> { "Microsoft.BingWeather", "Microsoft.GetHelp" };

        var script = BloatRemovalScriptGenerator.GenerateScript(
            packages, new List<string>(), new List<string>(), new List<string>());

        script.Should().Contain("Microsoft.BingWeather");
        script.Should().Contain("Microsoft.GetHelp");
    }

    [Fact]
    public void GenerateScript_WithCapabilities_ContainsCapabilityNames()
    {
        var capabilities = new List<string> { "App.Support.QuickAssist~~~~0.0.1.0" };

        var script = BloatRemovalScriptGenerator.GenerateScript(
            new List<string>(), capabilities, new List<string>(), new List<string>());

        script.Should().Contain("App.Support.QuickAssist~~~~0.0.1.0");
    }

    [Fact]
    public void GenerateScript_WithOptionalFeatures_ContainsFeatureNames()
    {
        var features = new List<string> { "WindowsMediaPlayer" };

        var script = BloatRemovalScriptGenerator.GenerateScript(
            new List<string>(), new List<string>(), features, new List<string>());

        script.Should().Contain("WindowsMediaPlayer");
    }

    [Fact]
    public void GenerateScript_WithXboxRegistryFix_ContainsRegistryFix()
    {
        var script = BloatRemovalScriptGenerator.GenerateScript(
            new List<string>(), new List<string>(),
            new List<string>(), new List<string>(),
            includeXboxRegistryFix: true);

        // The script should contain Xbox-related registry content
        script.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateScript_WithTeamsProcessKill_ContainsTeamsKill()
    {
        var script = BloatRemovalScriptGenerator.GenerateScript(
            new List<string>(), new List<string>(),
            new List<string>(), new List<string>(),
            includeTeamsProcessKill: true);

        script.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ExtractArrayFromScript_ValidArray_ReturnsItems()
    {
        var script = BloatRemovalScriptGenerator.GenerateScript(
            new List<string> { "App1", "App2" },
            new List<string>(), new List<string>(), new List<string>());

        var extracted = BloatRemovalScriptGenerator.ExtractArrayFromScript(script, "packages");

        extracted.Should().Contain("App1");
        extracted.Should().Contain("App2");
    }

    [Fact]
    public void ExtractArrayFromScript_NonExistentArray_ReturnsEmptyList()
    {
        var result = BloatRemovalScriptGenerator.ExtractArrayFromScript(
            "some content without arrays", "nonexistent");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractArrayFromScript_EmptyContent_ReturnsEmptyList()
    {
        var result = BloatRemovalScriptGenerator.ExtractArrayFromScript("", "packages");

        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateScript_RoundTrip_PreservesPackages()
    {
        var packages = new List<string> { "Microsoft.BingWeather", "Microsoft.GetHelp", "Microsoft.ZuneMusic" };
        var capabilities = new List<string> { "App.Support.QuickAssist~~~~0.0.1.0" };
        var optionalFeatures = new List<string> { "WindowsMediaPlayer" };
        var specialApps = new List<string> { "Edge" };

        var script = BloatRemovalScriptGenerator.GenerateScript(
            packages, capabilities, optionalFeatures, specialApps);

        var extractedPackages = BloatRemovalScriptGenerator.ExtractArrayFromScript(script, "packages");
        var extractedCaps = BloatRemovalScriptGenerator.ExtractArrayFromScript(script, "capabilities");
        var extractedFeatures = BloatRemovalScriptGenerator.ExtractArrayFromScript(script, "optionalFeatures");
        var extractedSpecial = BloatRemovalScriptGenerator.ExtractArrayFromScript(script, "specialApps");

        extractedPackages.Should().BeEquivalentTo(packages);
        extractedCaps.Should().BeEquivalentTo(capabilities);
        extractedFeatures.Should().BeEquivalentTo(optionalFeatures);
        extractedSpecial.Should().BeEquivalentTo(specialApps);
    }

    [Fact]
    public void UpdateScriptTemplate_PreservesDataWithNewTemplate()
    {
        var original = BloatRemovalScriptGenerator.GenerateScript(
            new List<string> { "Microsoft.BingWeather" },
            new List<string> { "TestCap" },
            new List<string> { "TestFeature" },
            new List<string> { "TestApp" });

        var updated = BloatRemovalScriptGenerator.UpdateScriptTemplate(original);

        updated.Should().Contain("Microsoft.BingWeather");
        updated.Should().Contain("TestCap");
        updated.Should().Contain("TestFeature");
        updated.Should().Contain("TestApp");
    }

    [Fact]
    public void UpdateScriptTemplate_WithXboxPackage_AutoEnablesXboxRegistryFix()
    {
        var original = BloatRemovalScriptGenerator.GenerateScript(
            new List<string> { "Microsoft.GamingApp" },
            new List<string>(), new List<string>(), new List<string>(),
            includeXboxRegistryFix: true);

        var updated = BloatRemovalScriptGenerator.UpdateScriptTemplate(original);

        // The updated template should detect Xbox packages and include the fix
        updated.Should().Contain("Microsoft.GamingApp");
    }

    [Fact]
    public void UpdateScriptTemplate_WithMSTeams_AutoEnablesTeamsProcessKill()
    {
        var original = BloatRemovalScriptGenerator.GenerateScript(
            new List<string> { "MSTeams" },
            new List<string>(), new List<string>(), new List<string>(),
            includeTeamsProcessKill: true);

        var updated = BloatRemovalScriptGenerator.UpdateScriptTemplate(original);

        updated.Should().Contain("MSTeams");
    }
}
