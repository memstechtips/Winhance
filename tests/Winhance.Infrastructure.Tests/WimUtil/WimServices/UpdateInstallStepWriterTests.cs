using System.Xml.Linq;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Autounattend;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class UpdateInstallStepWriterTests
{
    private const string Work = "C:\\work";
    private const string XmlPath = "C:\\work\\autounattend.xml";
    private const string ExtractPath = "powershell.exe -NoProfile -Command \"$sb = [scriptblock]::Create( $xml.unattend.Extensions.ExtractScript ); Invoke-Command -ScriptBlock $sb -ArgumentList $xml;\"";

    private static readonly string StagedDir = Work + "\\" + ScriptPaths.UnattendUpdatesMediaDirectory;
    private static readonly string TargetRecord = StagedDir + UpdateStaging.TargetRecordSuffix;
    private const string StagedMsu = "windows11.0-kb5043080-x64.msu";
    private static readonly string[] Architectures = ["x86", "arm64", "amd64"];

    private static readonly XNamespace U = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace Wcm = "http://schemas.microsoft.com/WMIConfig/2002/State";
    private static readonly XNamespace X = "urn:winhance:unattend";

    private static readonly string TemplateShapedXml =
        @"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""specialize"">
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""x86"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>1</Order><Description>" + AutounattendScripts.ExtractDescription + @"</Description><Path>" + ExtractPath + @"</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>4</Order><Description>Fourth</Description><Path>cmd.exe /c echo four</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
  </settings>
  <settings pass=""oobeSystem"">
    <component name=""Microsoft-Windows-Shell-Setup"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <OOBE><HideEULAPage>true</HideEULAPage></OOBE>
      <FirstLogonCommands>
        <SynchronousCommand><Order>1</Order><Description>First</Description><CommandLine>cmd.exe /c echo one</CommandLine></SynchronousCommand>
        <SynchronousCommand><Order>5</Order><Description>Fifth</Description><CommandLine>cmd.exe /c echo five</CommandLine></SynchronousCommand>
      </FirstLogonCommands>
    </component>
  </settings>
  <Extensions xmlns=""urn:winhance:unattend"">
    <ExtractScript>param([xml] $Document); # fixture</ExtractScript>
    <File path=""C:\ProgramData\Winhance\Unattend\Scripts\Winhancements.ps1""><![CDATA[Write-Host 'kept']]></File>
  </Extensions>
</unattend>";

    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<ILogService> _log = new();
    private string? _written;

    public UpdateInstallStepWriterTests()
    {
        _files
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));

        _files
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, contents, _) => _written = contents)
            .Returns(Task.CompletedTask);
    }

    private UpdateInstallStepWriter Sut() => new(_files.Object, _log.Object);

    private void StageUpdate()
    {
        _files.Setup(f => f.DirectoryExists(StagedDir)).Returns(true);
        _files.Setup(f => f.GetFiles(StagedDir, "*.msu", It.IsAny<SearchOption>())).Returns(new[] { StagedDir + "\\" + StagedMsu });
        _files.Setup(f => f.FileExists(StagedDir + "\\" + StagedMsu)).Returns(true);
        _files.Setup(f => f.FileExists(TargetRecord)).Returns(true);
        _files.Setup(f => f.ReadAllText(TargetRecord)).Returns(StagedMsu);
    }

    private void MediaXml(string content)
    {
        _files.Setup(f => f.FileExists(XmlPath)).Returns(true);
        _files.Setup(f => f.ReadAllTextAsync(XmlPath, It.IsAny<CancellationToken>())).ReturnsAsync(content);
    }

    private XDocument Written() => XDocument.Parse(_written!);

    private static XElement OobeComponent(XDocument doc, string architecture) =>
        doc.Root!.Elements(U + "settings").Single(s => (string?)s.Attribute("pass") == "oobeSystem")
            .Elements(U + "component").Single(c => (string?)c.Attribute("name") == "Microsoft-Windows-Shell-Setup"
                && (string?)c.Attribute("processorArchitecture") == architecture);

    private static XElement SpecializeComponent(XDocument doc, string architecture) =>
        doc.Root!.Elements(U + "settings").Single(s => (string?)s.Attribute("pass") == "specialize")
            .Elements(U + "component").Single(c => (string?)c.Attribute("processorArchitecture") == architecture);

    private static List<XElement> LogonCommands(XElement component) =>
        component.Element(U + "FirstLogonCommands")!.Elements(U + "SynchronousCommand").ToList();

    private static XElement OursOf(XElement component) =>
        LogonCommands(component).Single(c => (string?)c.Element(U + "Description") == UpdateInstallStepWriter.Marker);

    private static List<XElement> ScriptFiles(XDocument doc) =>
        doc.Root!.Elements().Single(e => e.Name.LocalName == "Extensions").Elements()
            .Where(e => e.Name.LocalName == "File" && (string?)e.Attribute("path") == UpdateInstallStepWriter.ScriptPath)
            .ToList();

    private static int ExtractCommandCount(XElement component) =>
        component.Element(U + "RunSynchronous")!.Elements(U + "RunSynchronousCommand")
            .Count(c => (string?)c.Element(U + "Description") == AutounattendScripts.ExtractDescription);

    [Fact]
    public async Task EnsureAsync_NoMsuStaged_TouchesNothing()
    {
        _files.Setup(f => f.DirectoryExists(StagedDir)).Returns(true);
        _files.Setup(f => f.GetFiles(StagedDir, "*.msu", It.IsAny<SearchOption>())).Returns(Array.Empty<string>());
        _files.Setup(f => f.FileExists(TargetRecord)).Returns(true);
        _files.Setup(f => f.ReadAllText(TargetRecord)).Returns(StagedMsu);
        MediaXml(TemplateShapedXml);

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(UpdateInstallStepResult.NothingStaged);
        _files.Verify(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureAsync_MsuStagedWithNoRecord_TouchesNothing()
    {
        _files.Setup(f => f.DirectoryExists(StagedDir)).Returns(true);
        _files.Setup(f => f.GetFiles(StagedDir, "*.msu", It.IsAny<SearchOption>())).Returns(new[] { StagedDir + "\\" + StagedMsu });
        _files.Setup(f => f.FileExists(StagedDir + "\\" + StagedMsu)).Returns(true);
        MediaXml(TemplateShapedXml);

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(UpdateInstallStepResult.NothingStaged);
        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureAsync_NoStagingFolder_ReportsNothingStaged()
    {
        (await Sut().EnsureAsync(Work)).Should().Be(UpdateInstallStepResult.NothingStaged);
    }

    [Fact]
    public async Task EnsureAsync_MissingXml_CreatesOneWithTheStepForEveryArchitecture()
    {
        StageUpdate();

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(UpdateInstallStepResult.CreatedXml);
        _written.Should().StartWith(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        var doc = Written();

        foreach (var architecture in Architectures)
        {
            ExtractCommandCount(SpecializeComponent(doc, architecture)).Should().Be(1);

            var logon = LogonCommands(OobeComponent(doc, architecture));
            logon.Should().ContainSingle();
            var ours = OursOf(OobeComponent(doc, architecture));
            ours.Attribute(Wcm + "action")!.Value.Should().Be("add");
            ours.Elements().Select(e => e.Name.LocalName).Should().Equal("Order", "Description", "CommandLine");
            ours.Element(U + "Order")!.Value.Should().Be("1");
            ours.Element(U + "CommandLine")!.Value.Should().Be(UpdateInstallStepWriter.InstallCommand);
        }

        var extensions = doc.Root!.Elements().Single(e => e.Name.LocalName == "Extensions");
        extensions.Name.Namespace.Should().Be(X);
        extensions.Element(X + "ExtractScript")!.Value.Should().Be(AutounattendScripts.ExtractScript.ReplaceLineEndings("\n"));
        ScriptFiles(doc).Single().Value.Should().Be(UpdateInstallStepWriter.InstallScript.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task EnsureAsync_ExistingXml_KeepsFirstLogonCommandsAndTakesTheNextOrder()
    {
        StageUpdate();
        MediaXml(TemplateShapedXml);

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(UpdateInstallStepResult.Added);
        _written.Should().Contain("<![CDATA[Write-Host 'kept']]>");
        var doc = Written();

        var amd64 = OobeComponent(doc, "amd64");
        LogonCommands(amd64).Select(c => c.Element(U + "Order")!.Value).Should().Equal("1", "5", "6");
        LogonCommands(amd64).Select(c => (string?)c.Element(U + "Description"))
            .Should().Equal("First", "Fifth", UpdateInstallStepWriter.Marker);
        amd64.Elements().Select(e => e.Name.LocalName).Should().Equal("OOBE", "FirstLogonCommands");

        OursOf(OobeComponent(doc, "x86")).Element(U + "Order")!.Value.Should().Be("1");
        ExtractCommandCount(SpecializeComponent(doc, "x86")).Should().Be(1);
        ExtractCommandCount(SpecializeComponent(doc, "amd64")).Should().Be(1);
        ScriptFiles(doc).Should().ContainSingle();
    }

    [Fact]
    public async Task EnsureAsync_SecondCall_ReportsAlreadyPresentWithoutWriting()
    {
        StageUpdate();
        MediaXml(TemplateShapedXml);
        var sut = Sut();

        (await sut.EnsureAsync(Work)).Should().Be(UpdateInstallStepResult.Added);

        MediaXml(_written!);
        (await sut.EnsureAsync(Work)).Should().Be(UpdateInstallStepResult.AlreadyPresent);

        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureAsync_CarriedScriptThatDiffers_IsRefreshedInPlace()
    {
        StageUpdate();
        MediaXml(TemplateShapedXml);
        var sut = Sut();
        await sut.EnsureAsync(Work);

        var stale = _written!.Replace("Winhance update install started", "OLD update install started");
        stale.Should().NotBe(_written);
        MediaXml(stale);

        (await sut.EnsureAsync(Work)).Should().Be(UpdateInstallStepResult.Added);

        var doc = Written();
        ScriptFiles(doc).Single().Value.Should().Be(UpdateInstallStepWriter.InstallScript.ReplaceLineEndings("\n"));
        OobeComponent(doc, "amd64").Element(U + "FirstLogonCommands")!.Elements().Should().HaveCount(3);
    }

    [Fact]
    public async Task EnsureAsync_NeverPlacesTheInstallInSpecialize()
    {
        StageUpdate();

        await Sut().EnsureAsync(Work);

        var specialize = Written().Root!.Elements(U + "settings").Single(s => (string?)s.Attribute("pass") == "specialize");
        specialize.ToString().Should().NotContain("Winhance-UpdateInstall");
        specialize.ToString().Should().NotContain(UpdateInstallStepWriter.Marker);
    }

    [Fact]
    public void InstallScript_GivesDismOnlyTheTargetAndNeverRestartsIt()
    {
        var script = UpdateInstallStepWriter.InstallScript;

        script.Split("/PackagePath:").Length.Should().Be(2);
        script.Should().Contain("/NoRestart");
        script.Should().Contain("-notin 0, 3010");
        script.Should().Contain("Winhance-UpdateInstall-dism.log");
        script.Should().Contain("shutdown.exe /r /t 10");
        script.TrimEnd().Should().EndWith("exit 0");
        UpdateInstallStepWriter.ScriptPath.Should().StartWith(@"C:\ProgramData\Winhance\Unattend\Scripts\");
    }

    [Fact]
    public async Task RemoveAsync_AfterEnsure_LeavesNoMarkerAndNoScript()
    {
        StageUpdate();
        MediaXml(TemplateShapedXml);
        var sut = Sut();
        await sut.EnsureAsync(Work);
        MediaXml(_written!);

        var removed = await sut.RemoveAsync(Work);

        removed.Should().BeTrue();
        var doc = Written();
        doc.Descendants(U + "Description").Should().NotContain(d => d.Value == UpdateInstallStepWriter.Marker);
        ScriptFiles(doc).Should().BeEmpty();
        _written.Should().Contain("<![CDATA[Write-Host 'kept']]>");
        LogonCommands(OobeComponent(doc, "amd64")).Select(c => c.Element(U + "Order")!.Value).Should().Equal("1", "5");
        ExtractCommandCount(SpecializeComponent(doc, "amd64")).Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_AfterEnsure_DropsTheListAndComponentItCreated()
    {
        StageUpdate();
        MediaXml(TemplateShapedXml);
        var sut = Sut();
        await sut.EnsureAsync(Work);
        MediaXml(_written!);

        await sut.RemoveAsync(Work);

        var oobe = Written().Root!.Elements(U + "settings").Single(s => (string?)s.Attribute("pass") == "oobeSystem");
        oobe.Elements(U + "component").Should().ContainSingle()
            .Which.Attribute("processorArchitecture")!.Value.Should().Be("amd64");
        oobe.Descendants(U + "FirstLogonCommands").Should().NotContain(list => !list.HasElements);
    }

    [Fact]
    public async Task RemoveAsync_LastScript_RemovesTheExtractorAndItsCommands()
    {
        StageUpdate();
        var noOtherScripts = XDocument.Parse(TemplateShapedXml);
        noOtherScripts.Descendants(X + "File").Remove();
        MediaXml(noOtherScripts.ToString());
        var sut = Sut();
        await sut.EnsureAsync(Work);
        MediaXml(_written!);

        await sut.RemoveAsync(Work);

        var doc = Written();
        doc.Descendants().Should().NotContain(e => e.Name.LocalName == "ExtractScript");
        doc.Descendants(U + "Path").Should().NotContain(p => p.Value.Contains("Extensions.ExtractScript"));
        SpecializeComponent(doc, "amd64").Descendants(U + "Description").Select(d => d.Value).Should().Equal("Fourth");
        doc.Root!.Elements(U + "settings").Single(s => (string?)s.Attribute("pass") == "specialize")
            .Elements(U + "component").Should().NotContain(c => (string?)c.Attribute("processorArchitecture") == "x86");
        doc.Root!.Elements().Should().NotContain(e => e.Name.LocalName == "Extensions");
    }

    [Fact]
    public async Task RemoveAsync_LastScript_KeepsExtensionsThatStillHoldWinhanceData()
    {
        StageUpdate();
        var winhanceFile = XDocument.Parse(TemplateShapedXml);
        winhanceFile.Descendants(X + "File").Remove();
        winhanceFile.Root!.Element(X + "Extensions")!.Add(new XElement(X + "Generator", new XAttribute("version", "1.0")));
        MediaXml(winhanceFile.ToString());
        var sut = Sut();
        await sut.EnsureAsync(Work);
        MediaXml(_written!);

        await sut.RemoveAsync(Work);

        Written().Root!.Element(X + "Extensions")!.Elements().Select(e => e.Name.LocalName).Should().Equal("Generator");
    }

    [Fact]
    public async Task RemoveAsync_NoStep_ReturnsFalseAndDoesNotSave()
    {
        MediaXml(TemplateShapedXml);

        var removed = await Sut().RemoveAsync(Work);

        removed.Should().BeFalse();
        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void InstallScript_WatchesTheFolderTheStagerInstallsInto()
    {
        UpdateInstallStepWriter.InstallScript.Should().Contain($"$updates = '{ScriptPaths.UnattendUpdatesDirectory}'");
    }

    [Fact]
    public void InstallScript_TakesTheRecordedTargetAndStillRestartsWithoutOne()
    {
        var script = UpdateInstallStepWriter.InstallScript;

        UpdateStaging.InstalledTargetRecord.Should().Be(ScriptPaths.UnattendUpdatesDirectory + UpdateStaging.TargetRecordSuffix);
        script.Should().Contain($"$record = '{UpdateStaging.InstalledTargetRecord}'");
        script.Should().Contain("Where-Object Name -eq $name");
        script.Should().NotContain("Sort-Object");

        var noTarget = script[script.IndexOf("if (-not $target) {", StringComparison.Ordinal)..];
        noTarget = noTarget[..(noTarget.IndexOf('}') + 1)];
        noTarget.Should().Contain("Remove-Item $updates, $record").And.Contain("shutdown.exe /r").And.Contain("exit 0").And.NotContain("dism.exe");
    }
}
