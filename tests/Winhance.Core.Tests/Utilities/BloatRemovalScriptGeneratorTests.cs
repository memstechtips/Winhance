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

        script.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateScript_WithXboxRegistryFix_RedirectsGameBarLinksAndLeavesGameDvrAlone()
    {
        var script = BloatRemovalScriptGenerator.GenerateScript(
            new List<string> { "Microsoft.XboxGamingOverlay" }, new List<string>(),
            new List<string>(), new List<string>(),
            includeXboxRegistryFix: true);

        script.Should().Contain(@"ms-gamebarservices").And.Contain(@"$key\shell\open\command");
        script.Should().NotContain("GameDVR");
        script.Should().NotContain("AppCaptureEnabled");
    }

    [Fact]
    public void GenerateScript_TakesTheImageAndSoftwareRootWithLiveDefaults()
    {
        var script = BloatRemovalScriptGenerator.GenerateScript(new List<string>(), new List<string>(), new List<string>(), new List<string>(), includeXboxRegistryFix: true);

        script.Should().Contain("param([string]$Image = '',");
        script.Should().Contain("[string]$SoftwareRoot = 'HKEY_LOCAL_MACHINE\\SOFTWARE'");
        script.Should().Contain("$classes = if ($Offline) { \"$SoftwareRoot\\Classes\" } else { 'HKCR' }");
    }

    [Fact]
    public void GenerateScript_OnAnImage_RunsOnlyTheXboxRedirect()
    {
        var script = BloatRemovalScriptGenerator.GenerateScript(
            new List<string> { "Microsoft.XboxGamingOverlay" }, new List<string>(), new List<string>(), new List<string> { "OneNote" },
            includeXboxRegistryFix: true);

        script.IndexOf("if (-not $Offline) {", StringComparison.Ordinal).Should().BePositive()
            .And.BeLessThan(script.IndexOf("ms-gamebarservices", StringComparison.Ordinal));
        script.Should().Contain("if ($Offline) { Write-Host $Message; return }");
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

    [Fact]
    public void UpdateScriptTemplate_LegacyPhotosSelection_AddsCompanionAndOwnerContextCleanup()
    {
        var original = "$packages = @(\n    'Microsoft.Windows.Photos'\n)\n";
        var updated = BloatRemovalScriptGenerator.UpdateScriptTemplate(original);
        BloatRemovalScriptGenerator.ExtractArrayFromScript(updated, "packages")
            .Should().Equal("Microsoft.Windows.Photos", "Microsoft.OneDrive.Photos");
        updated.Should().Contain("-LogonType Interactive -RunLevel Limited");
        updated.Should().Contain("Global\\WinhanceOneDriveRemoval");
        updated.Should().Contain("ODPhotosOneDriveSetup.exe");
        var encoded = System.Text.RegularExpressions.Regex.Match(updated, @"\$userAction = '([^']+)'").Groups[1].Value;
        var ownerScript = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
        ownerScript.Should().Contain("/partialodphotosremoval");
        ownerScript.Should().NotContain("/removeonedrive");
        ownerScript.Should().Contain("OneDrive Photos installer missing while OneDrive is still installed.");
        ownerScript.IndexOf("Orphan OneDrive Photos package could not be removed.", StringComparison.Ordinal)
            .Should().BeLessThan(ownerScript.IndexOf("Remove-Item -LiteralPath $key", StringComparison.Ordinal));
        updated.IndexOf("-LogonType Interactive -RunLevel Limited", StringComparison.Ordinal)
            .Should().BeLessThan(updated.IndexOf("# Discover all packages upfront", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Microsoft.Copilot")]
    [InlineData("Microsoft.MicrosoftOfficeHub")]
    public void GenerateScript_CopilotSelection_ChecksManifestAndReportsResidualDesktopInstallation(string package)
    {
        var script = BloatRemovalScriptGenerator.GenerateScript([package], [], [], []);
        script.Should().Contain("copilotapp_proxy.exe").And.Contain("Microsoft Copilot");
        script.Should().Contain("Get-AuthenticodeSignature").And.Contain("--mscopilot");
        script.Should().Contain("$retryClock = [Diagnostics.Stopwatch]::StartNew()");
        script.Should().Contain("while ((Test-Path -LiteralPath $key) -and $retryClock.Elapsed.TotalSeconds -lt 120)");
        script.Should().Contain("if ($process.ExitCode -ne 60 -or -not (Test-Path -LiteralPath $key)) { break }");
        script.Should().Contain("Start-Sleep -Seconds ([Math]::Min(15, $remaining))");
        script.IndexOf("$entry = Get-ItemProperty -LiteralPath $key", StringComparison.Ordinal)
            .Should().BeGreaterThan(script.IndexOf("while ((Test-Path -LiteralPath $key)", StringComparison.Ordinal));
        script.Should().Contain("The desktop Copilot installation remains");
        script.Should().NotContain("Remove-Item -LiteralPath $key");
    }

    [Fact]
    public void GenerateScript_NameWithAQuote_IsEscaped()
    {
        var script = BloatRemovalScriptGenerator.GenerateScript(
            new List<string> { "Evil'; calc; '" }, new List<string>(), new List<string>(), new List<string>());

        script.Should().Contain("    'Evil''; calc; '''");
        script.Should().NotContain("    'Evil'; calc");
    }

    [Fact]
    public void UpdateScriptTemplate_EscapedName_RoundTripsUnchanged()
    {
        var original = BloatRemovalScriptGenerator.GenerateScript(
            new List<string> { "Evil'; calc; '", "Microsoft.BingWeather" }, new List<string>(), new List<string>(), new List<string>());

        var updated = BloatRemovalScriptGenerator.UpdateScriptTemplate(original);

        BloatRemovalScriptGenerator.ExtractArrayFromScript(updated, BloatRemovalScriptGenerator.PackagesArray)
            .Should().Equal("Evil'; calc; '", "Microsoft.BingWeather");
        updated.Should().Contain("    'Evil''; calc; '''");
    }
}
