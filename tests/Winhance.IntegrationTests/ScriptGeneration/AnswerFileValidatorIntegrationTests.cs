using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.IntegrationTests.ScriptGeneration;

// The validator with the REAL Windows PowerShell parser behind it: every -Command payload and
// carried script in the template and in the driver writer's output goes through the parser Setup
// will use, in one powershell.exe process.
[Trait("Category", "Integration")]
public class AnswerFileValidatorIntegrationTests
{
    private const string Extract = "powershell.exe -NoProfile -WindowStyle Hidden -Command \"$xml = [xml]::new(); $xml.Load('C:\\Windows\\Panther\\unattend.xml'); $sb = [scriptblock]::Create( $xml.unattend.Extensions.ExtractScript ); Invoke-Command -ScriptBlock $sb -ArgumentList $xml;\"";

    private static PowerShellRunner Runner() => new(new FileSystemService());

    private static AnswerFileValidator Validator() => new(new FileSystemService(), Runner());

    private static string TemplateWithScript() =>
        AutounattendWriter.LoadTemplate().Replace("<!--SCRIPT_PLACEHOLDER-->", "<![CDATA[Write-Host 'generated']]>", StringComparison.Ordinal);

    private static string Fixture(string secondCommand, string carriedScript) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<unattend xmlns=\"urn:schemas-microsoft-com:unattend\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\">\n"
        + "<settings pass=\"specialize\"><component name=\"Microsoft-Windows-Deployment\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\"><RunSynchronous>"
        + "<RunSynchronousCommand wcm:action=\"add\"><Order>1</Order><Description>d</Description><Path>" + Extract.Replace("&", "&amp;", StringComparison.Ordinal) + "</Path></RunSynchronousCommand>"
        + "<RunSynchronousCommand wcm:action=\"add\"><Order>2</Order><Description>d</Description><Path>" + secondCommand.Replace("&", "&amp;", StringComparison.Ordinal) + "</Path></RunSynchronousCommand>"
        + "</RunSynchronous></component></settings>\n"
        + "<Extensions xmlns=\"urn:winhance:unattend\"><ExtractScript>param([xml] $Document); # fixture</ExtractScript><File path=\"C:\\ProgramData\\Winhance\\Unattend\\Scripts\\Winhancements.ps1\"><![CDATA[" + carriedScript + "]]></File></Extensions>\n</unattend>";

    private static async Task<AnswerFileReport> ValidateTextAsync(string xml)
    {
        var path = Path.Combine(Path.GetTempPath(), "winhance-answer-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            await File.WriteAllTextAsync(path, xml);
            return await Validator().ValidateAsync(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FindParseErrors_ReportsOnlyTheBrokenScript()
    {
        var errors = await Runner().FindParseErrorsAsync(new Dictionary<string, string>
        {
            ["ok"] = "Write-Host 'fine'",
            ["bad"] = "function Broken { param($a)\nif ($a) { Write-Host 'missing brace' ",
            ["also ok"] = "Get-ChildItem C:\\ | Where-Object Extension -eq '.inf'",
        });

        errors.Keys.Should().Equal("bad");
        errors["bad"].Should().StartWith("line ").And.Contain("Missing closing").And.NotContain("winhance_parse_");
    }

    [Fact]
    public async Task TemplateWithAScript_IsClean()
    {
        var report = await ValidateTextAsync(TemplateWithScript());

        report.Findings.Should().BeEmpty();
        report.Verdict.Should().Be(AnswerFileVerdict.Clean);
    }

    [Fact]
    public async Task DriverWriterOutput_OverTheTemplate_IsClean()
    {
        var work = Path.Combine(Path.GetTempPath(), "winhance-work-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(work, "sources", "$OEM$", "$$", "Drivers"));
            var xmlPath = Path.Combine(work, "autounattend.xml");
            await File.WriteAllTextAsync(xmlPath, TemplateWithScript());
            (await new DriverInstallStepWriter(new FileSystemService(), Mock.Of<ILogService>()).EnsureAsync(work)).Should().Be(DriverInstallStepResult.Added);

            var report = await Validator().ValidateAsync(xmlPath);

            report.Findings.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [Fact]
    public async Task BrokenInlinePayload_IsReportedAtItsCommand()
    {
        var report = await ValidateTextAsync(Fixture("powershell.exe -NoProfile -Command \"if ($a) {\"", "Write-Host 'fine'"));

        var finding = report.Findings.Should().ContainSingle().Subject;
        finding.Rule.Should().Be(AnswerFileRule.PowerShellParse);
        finding.Location.Should().Contain("RunSynchronousCommand[Order 2]");
        finding.Detail.Should().StartWith("line 1: ").And.Contain("Missing closing").And.NotContain("winhance_parse_");
    }

    [Fact]
    public async Task BrokenCarriedScript_IsReportedAtItsFile()
    {
        var report = await ValidateTextAsync(Fixture("cmd.exe /c echo hi", "function Broken {"));

        var finding = report.Findings.Should().ContainSingle().Subject;
        finding.Rule.Should().Be(AnswerFileRule.PowerShellParse);
        finding.Location.Should().Contain("File[C:\\ProgramData\\Winhance\\Unattend\\Scripts\\Winhancements.ps1]");
    }
}
