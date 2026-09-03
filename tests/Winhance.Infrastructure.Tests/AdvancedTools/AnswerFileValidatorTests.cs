using System.Text;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class AnswerFileValidatorTests
{
    private const string XmlPath = "C:\\work\\autounattend.xml";
    private const string Header = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
    private const string RootOpen = "<unattend xmlns=\"urn:schemas-microsoft-com:unattend\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\">";
    private const string Extract = "powershell.exe -NoProfile -WindowStyle Hidden -Command \"$xml = [xml]::new(); $xml.Load('C:\\Windows\\Panther\\unattend.xml'); $sb = [scriptblock]::Create( $xml.unattend.Extensions.ExtractScript ); Invoke-Command -ScriptBlock $sb -ArgumentList $xml;\"";
    private const string WinhanceScript = "C:\\ProgramData\\Winhance\\Unattend\\Scripts\\Winhancements.ps1";

    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<IPowerShellRunner> _powerShell = new();
    private IReadOnlyDictionary<string, string>? _sent;

    public AnswerFileValidatorTests()
    {
        _powerShell
            .Setup(p => p.FindParseErrorsAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyDictionary<string, string>, CancellationToken>((scripts, _) => _sent = scripts)
            .ReturnsAsync(new Dictionary<string, string>());
    }

    private Task<AnswerFileReport> ValidateAsync(string xml)
    {
        _files.Setup(f => f.ReadAllBytesAsync(XmlPath, It.IsAny<CancellationToken>())).ReturnsAsync(Encoding.UTF8.GetBytes(xml));
        return new AnswerFileValidator(_files.Object, _powerShell.Object).ValidateAsync(XmlPath);
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

    private static string Component(string name, string architecture, string body) =>
        $"<component name=\"{name}\" processorArchitecture=\"{architecture}\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">{body}</component>";

    private static string Command(string order, string text, string item = "RunSynchronousCommand", string textName = "Path", bool action = true) =>
        $"<{item}{(action ? " wcm:action=\"add\"" : string.Empty)}><Order>{order}</Order><Description>d</Description><{textName}>{Escape(text)}</{textName}></{item}>";

    private static string Command(int order, string text) => Command(order.ToString(System.Globalization.CultureInfo.InvariantCulture), text);

    private static string Specialize(params string[] commands) =>
        "<settings pass=\"specialize\">" + Component("Microsoft-Windows-Deployment", "amd64", "<RunSynchronous>" + string.Concat(commands) + "</RunSynchronous>") + "</settings>";

    private static string Carried(string path, string content) => $"<File path=\"{path}\"><![CDATA[{content}]]></File>";

    private static string WinhanceExtensions(params string[] carried) =>
        "<Extensions xmlns=\"urn:winhance:unattend\"><ExtractScript>param([xml] $Document); # fixture</ExtractScript>" + string.Concat(carried) + "</Extensions>";

    private static string Doc(params string[] parts) => Header + "\n" + RootOpen + "\n" + string.Join("\n", parts) + "\n</unattend>";

    // A specialize pass with the extractor, one carried script and one more command.
    private static string Typical(string secondCommand) =>
        Doc(Specialize(Command(1, Extract), Command(2, secondCommand)), WinhanceExtensions(Carried(WinhanceScript, "Write-Host 'w'")));

    private static string TemplateWithScript() =>
        AutounattendWriter.LoadTemplate().Replace("<!--SCRIPT_PLACEHOLDER-->", "<![CDATA[Write-Host 'generated']]>", StringComparison.Ordinal);

    private static AnswerFileFinding Single(AnswerFileReport report, AnswerFileRule rule) =>
        report.Findings.Should().ContainSingle(f => f.Rule == rule).Subject;

    [Fact]
    public async Task Template_WithAScript_IsClean()
    {
        var report = await ValidateAsync(TemplateWithScript());

        report.Findings.Should().BeEmpty();
        report.Verdict.Should().Be(AnswerFileVerdict.Clean);
        var sent = _sent!;
        sent.Should().HaveCount(14, because: "12 inline payloads, the ExtractScript and the carried script all go to the parser");
        sent.Keys.Should().Contain(k => k.Contains("ExtractScript", StringComparison.Ordinal));
        sent.Keys.Should().ContainSingle(k => k.Contains("File[", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WriterOutput_OverTheTemplate_IsClean()
    {
        var writerFiles = new Mock<IFileSystemService>();
        writerFiles.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        writerFiles.Setup(f => f.DirectoryExists("C:\\work\\sources\\$OEM$\\$$\\Drivers")).Returns(true);
        writerFiles.Setup(f => f.FileExists(XmlPath)).Returns(true);
        writerFiles.Setup(f => f.ReadAllTextAsync(XmlPath, It.IsAny<CancellationToken>())).ReturnsAsync(TemplateWithScript());
        string? written = null;
        writerFiles.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, contents, _) => written = contents)
            .Returns(Task.CompletedTask);
        await new DriverInstallStepWriter(writerFiles.Object, new Mock<ILogService>().Object).EnsureAsync("C:\\work");

        var report = await ValidateAsync(written!);

        report.Findings.Should().BeEmpty();
        _sent!.Should().HaveCount(15);
    }

    [Fact]
    public async Task SchneegansShapedFile_OnlyWarnsAboutVbScript()
    {
        const string wrapped = "cmd.exe /c \"powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \"Get-Content -LiteralPath 'C:\\Windows\\Setup\\Scripts\\wintweaks.ps1' -Raw | Invoke-Expression;\" >>\"C:\\Windows\\Setup\\Scripts\\wintweaks.log\" 2>&1\"";
        const string reg = "Windows Registry Editor Version 5.00\n\n; context menu\n[HKEY_CLASSES_ROOT\\*\\shell\\TakeOwnership]\n@=\"Take Ownership\"\n\"HasLUAShield\"=\"\"\n\"Icon\"=hex(2):25,00,53,00,\\\n  79,00\n\"Flags\"=dword:00000001\n\"Gone\"=-\n\n[-HKEY_CURRENT_USER\\Software\\Old]\n";
        var xml = Doc(
            "<settings pass=\"windowsPE\">" + Component("Microsoft-Windows-Setup", "amd64",
                "<RunSynchronous>" + Command(1, "cmd.exe /c del /f /q X:\\Sources\\ei.cfg") + Command(2, "cmd.exe /c echo [Channel] > X:\\Sources\\ei.cfg") + "</RunSynchronous>") + "</settings>",
            Specialize(
                Command(1, Extract),
                Command(2, "reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\OOBE\" /v X /t REG_DWORD /d 1 /f"),
                Command(3, "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"C:\\Windows\\Setup\\Scripts\\Specialize.ps1\""),
                Command(4, wrapped)),
            "<settings pass=\"oobeSystem\">" + Component("Microsoft-Windows-Shell-Setup", "amd64",
                "<FirstLogonCommands>" + Command("1", "powershell.exe -NoProfile -Command \"Write-Host 'first logon'\"", "SynchronousCommand", "CommandLine") + "</FirstLogonCommands>") + "</settings>",
            "<Extensions xmlns=\"https://schneegans.de/windows/unattend-generator/\"><ExtractScript>param([xml] $Document); foreach($f in $Document.unattend.Extensions.File) { }</ExtractScript>"
                + Carried("C:\\Windows\\Setup\\Scripts\\Specialize.ps1", "Write-Host 'specialize'")
                + Carried("C:\\Windows\\Setup\\Scripts\\wintweaks.ps1", "Write-Host 'tweaks'")
                + Carried("C:\\Windows\\Setup\\Scripts\\Layout.xml", "<LayoutModificationTemplate />")
                + Carried("C:\\Windows\\Setup\\Scripts\\Tweaks.cmd", "@echo off\nreg add HKLM\\Software\\X /f")
                + Carried("C:\\Windows\\Setup\\Scripts\\Context.reg", reg)
                + Carried("C:\\Windows\\Setup\\Scripts\\Unlock.vbs", "WScript.Echo 1")
                + "</Extensions>");

        var report = await ValidateAsync(xml);

        report.Findings.Should().OnlyContain(f => f.Severity == AnswerFileSeverity.Warning);
        report.Findings.Select(f => f.Rule).Should().Equal(AnswerFileRule.VbScriptDeprecated);
        report.Verdict.Should().Be(AnswerFileVerdict.MayFail);
        var sent = _sent!;
        sent.Values.Should().Contain("Get-Content -LiteralPath 'C:\\Windows\\Setup\\Scripts\\wintweaks.ps1' -Raw | Invoke-Expression;");
        sent.Values.Should().Contain("Write-Host 'first logon'");
        sent.Should().HaveCount(6, because: "the extractor payload, the wrapped payload, the first-logon payload, the ExtractScript and two .ps1 files");
    }

    [Fact]
    public async Task NotWellFormed_StopsAtTheParser()
    {
        var report = await ValidateAsync("<unattend><settings></unattend>");

        Single(report, AnswerFileRule.NotWellFormed).Location.Should().StartWith("line ");
        report.Findings.Should().HaveCount(1);
        report.Verdict.Should().Be(AnswerFileVerdict.WillFail);
        _sent.Should().BeNull();
    }

    [Fact]
    public async Task DeclarationThatLiesAboutTheBytes_IsNotWellFormed()
    {
        var report = await ValidateAsync(Typical("cmd.exe /c echo hi").Replace("encoding=\"utf-8\"", "encoding=\"utf-16\"", StringComparison.Ordinal));

        Single(report, AnswerFileRule.NotWellFormed);
    }

    [Fact]
    public async Task CodePageDeclaration_IsAcceptedLikeSetupDoes()
    {
        var xml = Typical("cmd.exe /c echo hi").Replace("encoding=\"utf-8\"", "encoding=\"windows-1252\"", StringComparison.Ordinal);
        _files.Setup(f => f.ReadAllBytesAsync(XmlPath, It.IsAny<CancellationToken>())).ReturnsAsync(Encoding.Latin1.GetBytes(xml));

        var report = await new AnswerFileValidator(_files.Object, _powerShell.Object).ValidateAsync(XmlPath);

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task WrongRoot_IsReported()
    {
        var report = await ValidateAsync(Header + "<answer xmlns=\"urn:schemas-microsoft-com:unattend\" />");

        Single(report, AnswerFileRule.WrongRoot).Detail.Should().Contain("answer");
    }

    [Fact]
    public async Task UnknownPass_IsReported()
    {
        var report = await ValidateAsync(Doc("<settings pass=\"specialise\"></settings>"));

        var finding = Single(report, AnswerFileRule.UnknownPass);
        finding.Detail.Should().Be("specialise");
        finding.Location.Should().MatchRegex(@"^line \d+: settings\[specialise\]$");
    }

    [Fact]
    public async Task ComponentWithoutRequiredAttributes_IsReported()
    {
        var report = await ValidateAsync(Doc("<settings pass=\"specialize\"><component name=\"Microsoft-Windows-Deployment\" processorArchitecture=\"amd64\" language=\"neutral\" versionScope=\"nonSxS\" /></settings>"));

        Single(report, AnswerFileRule.ComponentAttributes).Detail.Should().Be("missing publicKeyToken");
    }

    [Fact]
    public async Task ComponentWithUnknownArchitecture_IsReported()
    {
        var report = await ValidateAsync(Doc("<settings pass=\"specialize\">" + Component("Microsoft-Windows-Deployment", "x64", "") + "</settings>"));

        Single(report, AnswerFileRule.ComponentAttributes).Detail.Should().Be("processorArchitecture x64");
    }

    [Fact]
    public async Task CommandListUnderTheWrongComponent_IsReported()
    {
        var report = await ValidateAsync(Doc("<settings pass=\"specialize\">" + Component("Microsoft-Windows-Shell-Setup", "amd64", "<RunSynchronous>" + Command(1, "cmd.exe /c echo hi") + "</RunSynchronous>") + "</settings>"));

        var finding = Single(report, AnswerFileRule.CommandListPlacement);
        finding.Detail.Should().Contain("Microsoft-Windows-Deployment in specialize/auditUser");
        finding.Location.Should().EndWith("component[Microsoft-Windows-Shell-Setup amd64] / RunSynchronous");
    }

    [Fact]
    public async Task CommandListInWindowsPeUnderSetup_IsFine()
    {
        var report = await ValidateAsync(Doc("<settings pass=\"windowsPE\">" + Component("Microsoft-Windows-Setup", "amd64", "<RunAsynchronous>" + Command("1", "cmd.exe /c echo hi", "RunAsynchronousCommand") + "</RunAsynchronous>") + "</settings>"));

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task RunSynchronousInAuditUser_IsFine()
    {
        var report = await ValidateAsync(Doc("<settings pass=\"auditUser\">" + Component("Microsoft-Windows-Deployment", "amd64", "<RunSynchronous>" + Command(1, "cmd.exe /c echo hi") + "</RunSynchronous>") + "</settings>"));

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingAction_IsNotAFinding()
    {
        var report = await ValidateAsync(Doc(Specialize(Command("1", "cmd.exe /c echo hi", action: false))));

        report.Findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("501")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task OrderOutsideTheDocumentedRange_IsReported(string order)
    {
        var report = await ValidateAsync(Doc(Specialize(Command(order, "cmd.exe /c echo hi"))));

        Single(report, AnswerFileRule.OrderInvalid).Detail.Should().Be(order.Length == 0 ? "(none)" : order);
    }

    [Fact]
    public async Task DuplicateOrder_IsAWarning()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(2, "cmd.exe /c echo one"), Command(2, "cmd.exe /c echo two"))));

        Single(report, AnswerFileRule.OrderDuplicate).Severity.Should().Be(AnswerFileSeverity.Warning);
    }

    [Fact]
    public async Task EmptyCommand_IsReported()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, ""))));

        Single(report, AnswerFileRule.CommandEmpty).Location.Should().MatchRegex(@"^line \d+: settings\[specialize\] / component\[Microsoft-Windows-Deployment amd64\] / RunSynchronous / RunSynchronousCommand\[Order 1\]$");
    }

    [Fact]
    public async Task PathOverTheCap_IsReported()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, "cmd.exe /c " + new string('x', 249)))));

        Single(report, AnswerFileRule.CommandTooLong).Detail.Should().Be("260 characters, limit 259");
    }

    [Fact]
    public async Task FirstLogonCommandLine_HasItsOwnCap()
    {
        var xml = Doc("<settings pass=\"oobeSystem\">" + Component("Microsoft-Windows-Shell-Setup", "amd64",
            "<FirstLogonCommands>" + Command("1", "cmd.exe /c " + new string('x', 1014), "SynchronousCommand", "CommandLine") + "</FirstLogonCommands>") + "</settings>");

        var report = await ValidateAsync(xml);

        Single(report, AnswerFileRule.CommandTooLong).Detail.Should().Be("1025 characters, limit 1024");
    }

    [Fact]
    public async Task LogonCommandLine_HasNoCapToExceed()
    {
        var xml = Doc("<settings pass=\"oobeSystem\">" + Component("Microsoft-Windows-Shell-Setup", "amd64",
            "<LogonCommands>" + Command("1", "cmd.exe /c " + new string('x', 1989), "AsynchronousCommand", "CommandLine") + "</LogonCommands>") + "</settings>");

        var report = await ValidateAsync(xml);

        report.Findings.Should().BeEmpty(because: "LogonCommands has no documented CommandLine limit");
    }

    [Fact]
    public async Task InnerQuoteInABarePayload_IsReportedAndNotParsed()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, "powershell.exe -NoProfile -Command \"Write-Host \"hi\"\""))));

        Single(report, AnswerFileRule.InlineQuote).Detail.Should().Be("Write-Host \"hi\"");
        _sent.Should().BeNull();
    }

    [Fact]
    public async Task EscapedQuotesInABarePayload_AreFine()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, "powershell.exe -NoProfile -Command \"Write-Host \\\"hi\\\"\""))));

        report.Findings.Should().BeEmpty();
        _sent!.Values.Should().Contain("Write-Host \\\"hi\\\"");
    }

    [Fact]
    public async Task PathedCmdWrapper_EndsThePayloadAtTheNextQuote()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, "C:\\Windows\\System32\\cmd.exe /c \"powershell.exe -NoProfile -Command \"Write-Host 'x'\" >>\"C:\\log.txt\" 2>&1\""))));

        report.Findings.Should().BeEmpty();
        _sent!.Values.Should().Contain("Write-Host 'x'");
    }

    [Fact]
    public async Task ParserThatCannotRun_IsAWarningNotAThrow()
    {
        _powerShell
            .Setup(p => p.FindParseErrorsAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var report = await ValidateAsync(Typical("cmd.exe /c echo hi"));

        var finding = Single(report, AnswerFileRule.ParserUnavailable);
        finding.Severity.Should().Be(AnswerFileSeverity.Warning);
        finding.Location.Should().Be(XmlPath);
        finding.Detail.Should().Be("boom");
    }

    [Fact]
    public async Task ParserTimeout_IsAWarningNotAThrow()
    {
        _powerShell
            .Setup(p => p.FindParseErrorsAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var report = await ValidateAsync(Typical("cmd.exe /c echo hi"));

        var finding = Single(report, AnswerFileRule.ParserUnavailable);
        finding.Severity.Should().Be(AnswerFileSeverity.Warning);
        finding.Detail.Should().Contain("did not finish within");
    }

    [Fact]
    public async Task CallerCancellation_PropagatesInsteadOfBecomingAFinding()
    {
        using var cts = new CancellationTokenSource();
        _powerShell
            .Setup(p => p.FindParseErrorsAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyDictionary<string, string>, CancellationToken>((_, _) => cts.Cancel())
            .ThrowsAsync(new OperationCanceledException());
        _files.Setup(f => f.ReadAllBytesAsync(XmlPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(Typical("cmd.exe /c echo hi")));

        var act = () => new AnswerFileValidator(_files.Object, _powerShell.Object).ValidateAsync(XmlPath, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Parser_GetsACancellableTokenEvenWhenTheCallerPassesNone()
    {
        var canBeCancelled = false;
        _powerShell
            .Setup(p => p.FindParseErrorsAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyDictionary<string, string>, CancellationToken>((_, ct) => canBeCancelled = ct.CanBeCanceled)
            .ReturnsAsync(new Dictionary<string, string>());

        await ValidateAsync(Typical("cmd.exe /c echo hi"));

        canBeCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task CommandsSharingALocation_AllReachTheParser()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract) + Command(1, "powershell.exe -Command \"Write-Host 'a'\"") + Command(1, "powershell.exe -Command \"Write-Host 'b'\""))));

        report.Findings.Should().OnlyContain(f => f.Rule == AnswerFileRule.OrderDuplicate);
        _sent!.Values.Should().Contain("Write-Host 'a'").And.Contain("Write-Host 'b'");
    }

    [Fact]
    public async Task UnterminatedPayload_IsReported()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, "powershell.exe -NoProfile -Command \"Write-Host hi"))));

        Single(report, AnswerFileRule.InlineQuote).Detail.Should().Contain("never closes");
    }

    [Fact]
    public async Task ParserErrors_ComeBackAtTheirLocation()
    {
        _powerShell
            .Setup(p => p.FindParseErrorsAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<string, string> scripts, CancellationToken _) =>
                scripts.Keys.Where(k => k.Contains("Order 2", StringComparison.Ordinal)).ToDictionary(k => k, _ => "Missing closing '}' in statement block."));

        var report = await ValidateAsync(Typical("powershell.exe -NoProfile -Command \"if ($a) {\""));

        var finding = Single(report, AnswerFileRule.PowerShellParse);
        finding.Location.Should().Contain("RunSynchronousCommand[Order 2]");
        finding.Detail.Should().Be("Missing closing '}' in statement block.");
    }

    [Fact]
    public async Task FileTargetUnderAnExtractorFolder_MustBeCarried()
    {
        var report = await ValidateAsync(Typical("powershell.exe -NoProfile -File \"C:\\ProgramData\\Winhance\\Unattend\\Scripts\\Missing.ps1\""));

        var finding = Single(report, AnswerFileRule.ScriptNotCarried);
        finding.Detail.Should().Be("C:\\ProgramData\\Winhance\\Unattend\\Scripts\\Missing.ps1");
        finding.Location.Should().EndWith("RunSynchronousCommand[Order 2]");
    }

    [Fact]
    public async Task FileTargetElsewhere_IsAWarning()
    {
        var report = await ValidateAsync(Typical("powershell.exe -NoProfile -File D:\\Tools\\x.ps1"));

        Single(report, AnswerFileRule.ScriptPathUnknown).Severity.Should().Be(AnswerFileSeverity.Warning);
    }

    [Fact]
    public async Task CarriedFileTarget_IsFine()
    {
        var report = await ValidateAsync(Typical("powershell.exe -NoProfile -File \"c:\\programdata\\winhance\\unattend\\scripts\\winhancements.ps1\""));

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task OutFileInsideAPayload_IsNotReadAsAScriptArgument()
    {
        var report = await ValidateAsync(Typical("powershell.exe -NoProfile -Command \"Get-Date | Out-File C:\\ProgramData\\Winhance\\Unattend\\Scripts\\x.log\""));

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task FileSwitchInsideAPayload_IsNotReadAsAScriptArgument()
    {
        var report = await ValidateAsync(Typical("powershell.exe -NoProfile -Command \"Get-ChildItem C:\\ -File | Where-Object Extension -eq '.inf'\""));

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task RegistryCommandWithUnknownRoot_IsReported()
    {
        var report = await ValidateAsync(Typical("reg.exe add \"HKLN\\Software\\X\" /v A /t REG_DWORD /d 1 /f"));

        var finding = Single(report, AnswerFileRule.RegistryRoot);
        finding.Detail.Should().Be("HKLN\\Software\\X");
        finding.Location.Should().EndWith("RunSynchronousCommand[Order 2]");
    }

    [Fact]
    public async Task RegistryCommandWithShortRoot_IsFine()
    {
        var report = await ValidateAsync(Typical("reg add HKCU\\Software\\X /f"));

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task CarriedFilesWithoutAnExtractScript_IsReported()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), "<Extensions xmlns=\"urn:winhance:unattend\">" + Carried(WinhanceScript, "Write-Host 1") + "</Extensions>"));

        var finding = Single(report, AnswerFileRule.ExtractorMissing);
        finding.Detail.Should().Be("no ExtractScript element");
        finding.Location.Should().EndWith("Extensions");
    }

    [Fact]
    public async Task CarriedFilesWithoutAnExtractorCommand_IsReported()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, "cmd.exe /c echo hi")), WinhanceExtensions(Carried(WinhanceScript, "Write-Host 1"))));

        Single(report, AnswerFileRule.ExtractorMissing).Detail.Should().Be("no command runs Extensions.ExtractScript");
    }

    [Fact]
    public async Task RelativeFilePath_IsReported()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), WinhanceExtensions(Carried("Scripts\\x.ps1", "Write-Host 1"))));

        Single(report, AnswerFileRule.FilePathNotAbsolute).Detail.Should().Be("Scripts\\x.ps1");
    }

    [Fact]
    public async Task EnvironmentVariablePath_IsFine()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), WinhanceExtensions(Carried("%WINDIR%\\Setup\\x.ps1", "Write-Host 1"))));

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task CarriedXmlThatIsNotWellFormed_IsReported()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), WinhanceExtensions(Carried("C:\\Windows\\Setup\\Scripts\\Layout.xml", "<a><b></a>"))));

        Single(report, AnswerFileRule.XmlFileNotWellFormed).Location.Should().Contain("File[C:\\Windows\\Setup\\Scripts\\Layout.xml]");
    }

    [Theory]
    [InlineData("REGEDIT5\n\n[HKEY_CURRENT_USER\\Software]\n", "line 1: REGEDIT5")]
    [InlineData("Windows Registry Editor Version 5.00\n\n[HKLN\\Software]\n", "line 3: [HKLN\\Software]")]
    [InlineData("Windows Registry Editor Version 5.00\n\n[HKCU\\Software]\nname=value\n", "line 4: name=value")]
    public async Task RegFileSyntax_IsReportedWithTheLine(string content, string detail)
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), WinhanceExtensions(Carried("C:\\Windows\\Setup\\Scripts\\x.reg", content))));

        var finding = Single(report, AnswerFileRule.RegFileSyntax);
        finding.Detail.Should().Be(detail);
        finding.Location.Should().Contain("File[C:\\Windows\\Setup\\Scripts\\x.reg]");
    }

    [Fact]
    public async Task EmptyCarriedFile_IsReported()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), "<Extensions xmlns=\"urn:winhance:unattend\"><ExtractScript>x</ExtractScript><File path=\"" + WinhanceScript + "\"></File></Extensions>"));

        Single(report, AnswerFileRule.FileEmpty).Location.Should().Contain("File[" + WinhanceScript + "]");
    }

    [Fact]
    public async Task NonAsciiInABatchFile_IsAWarning()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), WinhanceExtensions(Carried("C:\\Windows\\Setup\\Scripts\\x.cmd", "@echo off\necho caf\u00e9"))));

        var finding = Single(report, AnswerFileRule.AnsiLossy);
        finding.Severity.Should().Be(AnswerFileSeverity.Warning);
        finding.Detail.Should().Be("line 2: echo caf\u00e9");
    }

    [Fact]
    public async Task UnknownCarriedFileType_IsAWarning()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), WinhanceExtensions(Carried("C:\\Windows\\Setup\\Scripts\\notes.txt", "hello"))));

        Single(report, AnswerFileRule.UnknownFileType).Severity.Should().Be(AnswerFileSeverity.Warning);
    }

    [Fact]
    public async Task CarriedFileWithNoExtension_IsAWarning()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), WinhanceExtensions(Carried("C:\\Windows\\Setup\\Scripts\\README", "hello"))));

        Single(report, AnswerFileRule.UnknownFileType).Severity.Should().Be(AnswerFileSeverity.Warning);
    }

    [Fact]
    public async Task CarriedJavaScriptFile_IsNotAFinding()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), WinhanceExtensions(Carried("C:\\Windows\\Setup\\Scripts\\Setup.js", "var x = 1;"))));

        report.Findings.Should().BeEmpty();
    }

    // DtdProcessing.Ignore skips the DOCTYPE rather than throwing on it, which Prohibit would.
    [Fact]
    public async Task CarriedXmlWithAnInternalDtd_IsFine()
    {
        var report = await ValidateAsync(Doc(Specialize(Command(1, Extract)), WinhanceExtensions(Carried("C:\\Windows\\Setup\\Scripts\\Layout.xml", "<!DOCTYPE x [<!ELEMENT x ANY>]>\n<x />"))));

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task CarriedScriptContent_IsSentTrimmedWithLfLineEndings()
    {
        await ValidateAsync(Doc(Specialize(Command(1, Extract)), WinhanceExtensions(Carried(WinhanceScript, "\r\n  Write-Host 'a'\r\nWrite-Host 'b'\r\n  "))));

        _sent!.Single(s => s.Key.Contains("File[", StringComparison.Ordinal)).Value.Should().Be("Write-Host 'a'\nWrite-Host 'b'");
    }

    [Fact]
    public async Task UnreadableFile_IsTheOnlyFinding()
    {
        _files.Setup(f => f.ReadAllBytesAsync(XmlPath, It.IsAny<CancellationToken>())).ThrowsAsync(new FileNotFoundException("nope"));

        var report = await new AnswerFileValidator(_files.Object, _powerShell.Object).ValidateAsync(XmlPath);

        var finding = Single(report, AnswerFileRule.FileUnreadable);
        finding.Location.Should().Be(XmlPath);
        finding.Detail.Should().Be("nope");
        report.Verdict.Should().Be(AnswerFileVerdict.WillFail);
    }

    [Fact]
    public void Verdict_FollowsTheWorstSeverity()
    {
        var warning = new AnswerFileFinding(AnswerFileRule.OrderDuplicate, AnswerFileSeverity.Warning, "l", "d");
        var error = new AnswerFileFinding(AnswerFileRule.CommandEmpty, AnswerFileSeverity.Error, "l", "d");

        new AnswerFileReport([]).Verdict.Should().Be(AnswerFileVerdict.Clean);
        new AnswerFileReport([warning]).Verdict.Should().Be(AnswerFileVerdict.MayFail);
        new AnswerFileReport([error]).Verdict.Should().Be(AnswerFileVerdict.WillFail);
        new AnswerFileReport([warning, error]).Verdict.Should().Be(AnswerFileVerdict.WillFail);
    }
}
