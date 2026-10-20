using System.Text;
using FluentAssertions;
using Winhance.Infrastructure.Features.Autounattend.ScriptSections;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

public class SpecialFeatureScriptSectionTests
{

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
}
