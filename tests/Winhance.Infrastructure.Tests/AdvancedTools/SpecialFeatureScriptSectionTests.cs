using System.Text;
using FluentAssertions;
using Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class SpecialFeatureScriptSectionTests
{
    // ---------------------------------------------------------------
    // AppendUserCustomizationsScheduledTask
    // ---------------------------------------------------------------

    [Fact]
    public void AppendUserCustomizationsScheduledTask_ContainsSectionHeader()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendUserCustomizationsScheduledTask(sb, "    ");

        var output = sb.ToString();
        output.Should().Contain("USER CUSTOMIZATIONS SCHEDULED TASK");
    }

    [Fact]
    public void AppendUserCustomizationsScheduledTask_ContainsTaskRegistration()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendUserCustomizationsScheduledTask(sb, "    ");

        var output = sb.ToString();
        output.Should().Contain("Register-ScheduledTask");
        output.Should().Contain("WinhanceUserCustomizations");
    }

    [Fact]
    public void AppendUserCustomizationsScheduledTask_ContainsScriptPath()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendUserCustomizationsScheduledTask(sb, "    ");

        sb.ToString().Should().Contain("Winhancements.ps1");
    }

    [Fact]
    public void AppendUserCustomizationsScheduledTask_ContainsErrorHandling()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendUserCustomizationsScheduledTask(sb, "    ");

        var output = sb.ToString();
        output.Should().Contain("try {");
        output.Should().Contain("} catch {");
    }

    [Fact]
    public void AppendUserCustomizationsScheduledTask_UsesCorrectIndent()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendUserCustomizationsScheduledTask(sb, "        ");

        var output = sb.ToString();
        output.Should().Contain("        Write-Log");
        output.Should().Contain("        try {");
    }

    // ---------------------------------------------------------------
    // AppendCleanStartMenuSection
    // ---------------------------------------------------------------

    [Fact]
    public void AppendCleanStartMenuSection_ContainsSectionHeader()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendCleanStartMenuSection(sb, "    ");

        sb.ToString().Should().Contain("START MENU LAYOUT");
    }

    [Fact]
    public void AppendCleanStartMenuSection_ContainsBuildNumberDetection()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendCleanStartMenuSection(sb, "    ");

        var output = sb.ToString();
        output.Should().Contain("$buildNumber");
        output.Should().Contain("OSVersion.Version.Build");
    }

    [Fact]
    public void AppendCleanStartMenuSection_ContainsWindows11Branch()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendCleanStartMenuSection(sb, "    ");

        var output = sb.ToString();
        output.Should().Contain("$buildNumber -ge 22000");
        output.Should().Contain("ConfigureStartPins");
        output.Should().Contain("{\"pinnedList\":[]}");
    }

    [Fact]
    public void AppendCleanStartMenuSection_ContainsWindows10Branch()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendCleanStartMenuSection(sb, "    ");

        var output = sb.ToString();
        output.Should().Contain("LayoutModification.xml");
        output.Should().Contain("LayoutModificationTemplate");
    }

    [Fact]
    public void AppendCleanStartMenuSection_ContainsXmlContent()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendCleanStartMenuSection(sb, "");

        var output = sb.ToString();
        output.Should().Contain("<?xml version=\"1.0\"");
        output.Should().Contain("StartLayoutCollection");
    }

    [Fact]
    public void AppendCleanStartMenuSection_UsesProvidedIndent()
    {
        var sb = new StringBuilder();

        SpecialFeatureScriptSection.AppendCleanStartMenuSection(sb, "INDENT");

        var output = sb.ToString();
        output.Should().Contain("INDENTWrite-Log");
    }
}
