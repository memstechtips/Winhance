using System.Xml.Linq;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class DriverInstallStepWriterTests
{
    private const string Work = "C:\\work";
    private const string XmlPath = "C:\\work\\autounattend.xml";
    private const string StagedDir = "C:\\work\\sources\\$OEM$\\$$\\Drivers";
    private const string ExtractPath = "powershell.exe -NoProfile -Command \"$sb = [scriptblock]::Create( $xml.unattend.Extensions.ExtractScript ); Invoke-Command -ScriptBlock $sb -ArgumentList $xml;\"";
    private const string DisablePath = "powershell.exe -NoProfile -Command \"Get-NetAdapter | Disable-NetAdapter -Confirm:$false\"";
    private const string DisableDescription = "Disable All Network Adapters Temporarily so Windows Doesn't Update During OOBE and to Allow Local Account Creation";
    private const string DisableAdapterA = "powershell.exe -NoProfile -Command \"Disable-NetAdapter -Name 'A' -Confirm:$false\"";
    private const string DisableAdapterB = "powershell.exe -NoProfile -Command \"Disable-NetAdapter -Name 'B' -Confirm:$false\"";

    private static readonly XNamespace U = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace Wcm = "http://schemas.microsoft.com/WMIConfig/2002/State";
    private static readonly XNamespace X = "urn:winhance:unattend";

    // Shaped like the real template: every component loads the scripts at Order 1, x86 and amd64
    // disable network adapters mid-sequence with the real Description text, arm64 (unlike the
    // real file) deliberately does not, to cover both branches.
    private static readonly string TemplateShapedXml =
        @"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""specialize"">
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""x86"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>1</Order><Description>" + DriverInstallStepWriter.ExtractDescription + @"</Description><Path>" + ExtractPath + @"</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>3</Order><Description>" + DisableDescription + @"</Description><Path>" + DisablePath + @"</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>5</Order><Description>Fifth</Description><Path>cmd.exe /c echo five</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""arm64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>1</Order><Description>" + DriverInstallStepWriter.ExtractDescription + @"</Description><Path>" + ExtractPath + @"</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>2</Order><Description>Second</Description><Path>cmd.exe /c echo two</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>1</Order><Description>" + DriverInstallStepWriter.ExtractDescription + @"</Description><Path>" + ExtractPath + @"</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>7</Order><Description>" + DisableDescription + @"</Description><Path>" + DisablePath + @"</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
  </settings>
  <settings pass=""oobeSystem""></settings>
  <Extensions xmlns=""urn:winhance:unattend"">
    <ExtractScript>param([xml] $Document); # fixture</ExtractScript>
    <File path=""C:\ProgramData\Winhance\Unattend\Scripts\Winhancements.ps1""><![CDATA[Write-Host 'kept']]></File>
  </Extensions>
</unattend>";

    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<ILogService> _log = new();
    private string? _writtenPath;
    private string? _written;

    public DriverInstallStepWriterTests()
    {
        _files
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));

        _files
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((path, contents, _) => { _writtenPath = path; _written = contents; })
            .Returns(Task.CompletedTask);
    }

    private DriverInstallStepWriter Sut() => new(_files.Object, _log.Object);

    private void StageDrivers() => _files.Setup(f => f.DirectoryExists(StagedDir)).Returns(true);

    private void MediaXml(string content)
    {
        _files.Setup(f => f.FileExists(XmlPath)).Returns(true);
        _files.Setup(f => f.ReadAllTextAsync(XmlPath, It.IsAny<CancellationToken>())).ReturnsAsync(content);
    }

    private XDocument Written() => XDocument.Parse(_written!);

    private static XDocument Template() => XDocument.Parse(AutounattendWriter.LoadTemplate());

    // XML parsing normalizes the CDATA's CRLF to LF; the extractor's own load does the same on the target.
    private static string CarriedScript() => DriverInstallStepWriter.InstallScript.ReplaceLineEndings("\n");

    private static string TemplateExtractPath() =>
        Template().Descendants(U + "Path").Select(p => p.Value).First(p => p.Contains("Extensions.ExtractScript", StringComparison.Ordinal));

    private static string TemplateExtractScript() =>
        Template().Root!.Element(X + "Extensions")!.Element(X + "ExtractScript")!.Value;

    private static XElement SpecializeComponent(XDocument doc, string architecture) =>
        doc.Root!.Elements(U + "settings").Single(s => (string?)s.Attribute("pass") == "specialize")
            .Elements(U + "component").Single(c => (string?)c.Attribute("processorArchitecture") == architecture);

    private static IEnumerable<XElement> Commands(XElement component) =>
        component.Element(U + "RunSynchronous")!.Elements(U + "RunSynchronousCommand");

    private static XElement Described(XElement component, string description) =>
        Commands(component).Single(c => (string?)c.Element(U + "Description") == description);

    private static int CountDescribed(XElement component, string description) =>
        Commands(component).Count(c => (string?)c.Element(U + "Description") == description);

    private static XElement MarkerCommand(XElement component) => Described(component, DriverInstallStepWriter.Marker);

    private static string OrderOf(XElement command) => command.Element(U + "Order")!.Value;

    private static string PathOf(XElement command) => command.Element(U + "Path")!.Value;

    private static XElement ExtensionsOf(XDocument doc) =>
        doc.Root!.Elements().Single(e => e.Name.LocalName == "Extensions");

    private static List<XElement> ScriptFiles(XDocument doc) =>
        ExtensionsOf(doc).Elements()
            .Where(e => e.Name.LocalName == "File" && (string?)e.Attribute("path") == DriverInstallStepWriter.ScriptPath)
            .ToList();

    [Fact]
    public async Task EnsureAsync_NoStagedDrivers_TouchesNothing()
    {
        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.NoDriversStaged);
        _files.Verify(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void InstallCommand_RunsTheScriptFileTheAnswerFileCarries()
    {
        DriverInstallStepWriter.InstallCommand.Should().Be(
            @"powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File ""C:\ProgramData\Winhance\Unattend\Scripts\Winhance-DriverInstall.ps1""");

        // The template's extractor creates exactly this folder and no other.
        DriverInstallStepWriter.ScriptPath.Should().StartWith(@"C:\ProgramData\Winhance\Unattend\Scripts\");

        var script = DriverInstallStepWriter.InstallScript;
        script.Should().Contain("$drivers = 'C:\\Windows\\Drivers'");
        script.Should().Contain("pnputil /add-driver $inf.FullName /install");
        script.Should().Contain("-notin 0, 3010, 259");
        script.Should().Contain("Remove-Item $dir -Recurse -Force");
        script.Should().Contain("C:\\ProgramData\\Winhance\\Unattend\\Logs\\Winhance-DriverInstall.log");
        script.Should().NotContain("Test-Path");
        script.Should().NotContain("-Filter");
        script.TrimEnd().Should().EndWith("exit 0");

        // One header comment, then code: nothing below the header starts with '#'.
        var lines = script.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines[0].Should().StartWith("# ");
        lines.SkipWhile(l => l.StartsWith('#')).Should().NotContain(l => l.TrimStart().StartsWith('#'));
    }

    [Fact]
    public async Task EnsureAsync_StagedDriversAndNoXml_WritesAMinimalXmlCarryingTheScript()
    {
        StageDrivers();

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.CreatedXml);
        _writtenPath.Should().Be(XmlPath);
        _written.Should().StartWith(@"<?xml version=""1.0"" encoding=""utf-8""?>");

        var doc = Written();
        doc.Root!.Name.Should().Be(U + "unattend");
        doc.Root.Elements().Select(e => e.Name.LocalName).Should().Equal("settings", "Extensions");

        foreach (var architecture in new[] { "x86", "arm64", "amd64" })
        {
            var component = SpecializeComponent(doc, architecture);
            var extract = Described(component, DriverInstallStepWriter.ExtractDescription);
            OrderOf(extract).Should().Be("1");
            PathOf(extract).Should().Be(TemplateExtractPath());

            var install = MarkerCommand(component);
            install.Attribute(Wcm + "action")!.Value.Should().Be("add");
            OrderOf(install).Should().Be("2");
            PathOf(install).Should().Be(DriverInstallStepWriter.InstallCommand);
            Commands(component).Should().HaveCount(2);
        }

        var extensions = ExtensionsOf(doc);
        extensions.Name.Namespace.Should().Be(X);
        extensions.Element(X + "ExtractScript")!.Value.Should().Be(TemplateExtractScript());
        var file = ScriptFiles(doc).Single();
        file.Value.Should().Be(CarriedScript());
        _written.Should().Contain("<![CDATA[# Installs the driver packages");
    }

    [Fact]
    public async Task EnsureAsync_TemplateShapedXml_AppendsAfterTheHighestOrderPerComponent()
    {
        StageDrivers();
        MediaXml(TemplateShapedXml);

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        _written.Should().StartWith("<?xml");
        _written.Should().Contain("<![CDATA[Write-Host 'kept']]>");

        _files.Verify(f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _files.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);

        var doc = Written();
        OrderOf(MarkerCommand(SpecializeComponent(doc, "x86"))).Should().Be("5");
        OrderOf(MarkerCommand(SpecializeComponent(doc, "arm64"))).Should().Be("3");
        OrderOf(MarkerCommand(SpecializeComponent(doc, "amd64"))).Should().Be("2");
        foreach (var architecture in new[] { "x86", "arm64", "amd64" })
            CountDescribed(SpecializeComponent(doc, architecture), DriverInstallStepWriter.ExtractDescription).Should().Be(1);

        var extensions = ExtensionsOf(doc);
        extensions.Elements(X + "ExtractScript").Should().HaveCount(1);
        extensions.Element(X + "ExtractScript")!.Value.Should().Be("param([xml] $Document); # fixture");
        extensions.Elements(X + "File").Should().HaveCount(2);
        extensions.Elements(X + "File").Last().Should().BeSameAs(ScriptFiles(doc).Single());
        ScriptFiles(doc).Single().Value.Should().Be(CarriedScript());
    }

    [Fact]
    public async Task EnsureAsync_ComponentDisablingNetAdapters_MovesThatCommandBehindTheInstall()
    {
        StageDrivers();
        MediaXml(TemplateShapedXml);

        await Sut().EnsureAsync(Work);

        var doc = Written();

        var x86 = SpecializeComponent(doc, "x86");
        CountDescribed(x86, DisableDescription).Should().Be(1);
        var x86Disable = Commands(x86).Last();
        x86Disable.Element(U + "Description")!.Value.Should().Be(DisableDescription);
        PathOf(x86Disable).Should().Be(DisablePath);
        OrderOf(x86Disable).Should().Be("6");
        OrderOf(Described(x86, "Fifth")).Should().Be("4");
        Commands(x86).Select(OrderOf).Should().Equal("1", "4", "5", "6");

        var amd64 = SpecializeComponent(doc, "amd64");
        CountDescribed(amd64, DisableDescription).Should().Be(1);
        Commands(amd64).Select(OrderOf).Should().Equal("1", "2", "3");
        PathOf(Commands(amd64).Last()).Should().Be(DisablePath);

        CountDescribed(SpecializeComponent(doc, "arm64"), DisableDescription).Should().Be(0);
        PathOf(MarkerCommand(x86)).Should().NotContain("Disable-NetAdapter");
    }

    [Fact]
    public async Task EnsureAsync_XmlAlreadyEnsured_ReportsAlreadyPresentWithoutWriting()
    {
        StageDrivers();
        MediaXml(TemplateShapedXml);
        var sut = Sut();

        (await sut.EnsureAsync(Work)).Should().Be(DriverInstallStepResult.Added);

        MediaXml(_written!);
        (await sut.EnsureAsync(Work)).Should().Be(DriverInstallStepResult.AlreadyPresent);

        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureAsync_XmlWithoutAnExtractor_AddsTheTemplateExtractorBeforeTheInstall()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""specialize"">
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>1</Order><Description>Bypass</Description><Path>reg.exe add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
  </settings>
</unattend>");

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        var doc = Written();

        var amd64 = SpecializeComponent(doc, "amd64");
        var extract = Described(amd64, DriverInstallStepWriter.ExtractDescription);
        OrderOf(extract).Should().Be("2");
        PathOf(extract).Should().Be(TemplateExtractPath());
        OrderOf(MarkerCommand(amd64)).Should().Be("3");

        var x86 = SpecializeComponent(doc, "x86");
        OrderOf(Described(x86, DriverInstallStepWriter.ExtractDescription)).Should().Be("1");
        OrderOf(MarkerCommand(x86)).Should().Be("2");

        doc.Root!.Elements().Last().Name.Should().Be(X + "Extensions");
        ExtensionsOf(doc).Element(X + "ExtractScript")!.Value.Should().Be(TemplateExtractScript());
        ScriptFiles(doc).Should().HaveCount(1);
    }

    [Fact]
    public async Task EnsureAsync_ForeignExtensionsBlock_ReusesItsNamespaceAndExtractor()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""specialize"">
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>1</Order><Description>Extract script files</Description><Path>" + ExtractPath + @"</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
  </settings>
  <Extensions xmlns=""https://schneegans.de/windows/unattend-generator/"">
    <ExtractScript>
param( [xml] $Document );
foreach( $file in $Document.unattend.Extensions.File ) {
    $path = [System.Environment]::ExpandEnvironmentVariables( $file.GetAttribute( 'path' ) );
    mkdir -Path( $path | Split-Path -Parent ) -ErrorAction 'SilentlyContinue';
    [System.IO.File]::WriteAllBytes( $path, [System.Text.Encoding]::UTF8.GetBytes( $file.InnerText.Trim() ) );
}
    </ExtractScript>
    <File path=""C:\Windows\Setup\Scripts\unattend-01.ps1"">Write-Host 'theirs'</File>
  </Extensions>
</unattend>");

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        var doc = Written();
        XNamespace schneegans = "https://schneegans.de/windows/unattend-generator/";

        doc.Root!.Elements().Count(e => e.Name.LocalName == "Extensions").Should().Be(1);
        var extensions = ExtensionsOf(doc);
        extensions.Elements(schneegans + "ExtractScript").Single().Value.Should().Contain("Split-Path -Parent");
        extensions.Elements(schneegans + "File").Should().HaveCount(2);
        ScriptFiles(doc).Single().Name.Namespace.Should().Be(schneegans);

        var amd64 = SpecializeComponent(doc, "amd64");
        CountDescribed(amd64, DriverInstallStepWriter.ExtractDescription).Should().Be(0);
        OrderOf(MarkerCommand(amd64)).Should().Be("2");
    }

    [Fact]
    public async Task EnsureAsync_NoExtractorAndDisablesNetAdapters_ChainsExtractInstallThenTheMovedDisable()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""specialize"">
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>1</Order><Description>Bypass</Description><Path>reg.exe add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>2</Order><Description>" + DisableDescription + @"</Description><Path>" + DisablePath + @"</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
  </settings>
</unattend>");

        await Sut().EnsureAsync(Work);

        var amd64 = SpecializeComponent(Written(), "amd64");
        OrderOf(Described(amd64, DriverInstallStepWriter.ExtractDescription)).Should().Be("2");
        OrderOf(MarkerCommand(amd64)).Should().Be("3");
        CountDescribed(amd64, DisableDescription).Should().Be(1);
        OrderOf(Described(amd64, DisableDescription)).Should().Be("4");
        Commands(amd64).Last().Should().BeSameAs(Described(amd64, DisableDescription));
    }

    [Fact]
    public async Task EnsureAsync_StaleScriptAlreadyCarried_IsRefreshedInPlace()
    {
        StageDrivers();
        MediaXml(TemplateShapedXml.Replace(
            @"<File path=""C:\ProgramData\Winhance\Unattend\Scripts\Winhancements.ps1"">",
            @"<File path=""" + DriverInstallStepWriter.ScriptPath + @""">stale</File><File path=""C:\ProgramData\Winhance\Unattend\Scripts\Winhancements.ps1"">",
            StringComparison.Ordinal));

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        var doc = Written();
        ScriptFiles(doc).Single().Value.Should().Be(CarriedScript());
        OrderOf(MarkerCommand(SpecializeComponent(doc, "amd64"))).Should().Be("2");
    }

    [Fact]
    public async Task EnsureAsync_XmlWithoutSpecializeSettings_CreatesTheWholeChain()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""oobeSystem""></settings>
</unattend>");

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        var doc = Written();
        foreach (var architecture in new[] { "x86", "arm64", "amd64" })
        {
            var component = SpecializeComponent(doc, architecture);
            OrderOf(Described(component, DriverInstallStepWriter.ExtractDescription)).Should().Be("1");
            OrderOf(MarkerCommand(component)).Should().Be("2");
        }
    }

    [Fact]
    public async Task EnsureAsync_XmlEndingInExtensions_PutsTheNewSettingsBeforeThem()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""windowsPE""></settings>
  <Extensions xmlns=""urn:custom:extensions""><Data>kept</Data></Extensions>
</unattend>");

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        var doc = Written();
        var children = doc.Root!.Elements().ToList();
        children.Select(c => c.Name.LocalName).Should().Equal("settings", "settings", "Extensions");
        children[0].Attribute("pass")!.Value.Should().Be("windowsPE");
        children[1].Attribute("pass")!.Value.Should().Be("specialize");
        children[2].Elements().Select(e => e.Name.LocalName).Should().Equal("ExtractScript", "Data", "File");
    }

    [Fact]
    public async Task EnsureAsync_XmlWithoutWcmNamespace_DeclaresIt()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"">
  <settings pass=""specialize""></settings>
</unattend>");

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        _written.Should().Contain("xmlns:wcm=");
        MarkerCommand(SpecializeComponent(Written(), "amd64")).Attribute(Wcm + "action")!.Value.Should().Be("add");
    }

    [Fact]
    public async Task EnsureAsync_ForeignEncodingDeclaration_NormalizesToUtf8()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""windows-1252""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""specialize""></settings>
</unattend>");

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        _written.Should().StartWith(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        _written.Should().NotContain("windows-1252");
    }

    [Fact]
    public async Task EnsureAsync_NonNumericOrder_IsIgnoredForTheNextOrder()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""specialize"">
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>1</Order><Description>Loads</Description><Path>" + ExtractPath + @"</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>abc</Order><Description>Odd</Description><Path>cmd.exe /c echo odd</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>3</Order><Description>Third</Description><Path>cmd.exe /c echo three</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
  </settings>
</unattend>");

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        OrderOf(MarkerCommand(SpecializeComponent(Written(), "amd64"))).Should().Be("4");
    }

    [Fact]
    public async Task EnsureAsync_DisableComesFirst_MovesItBehindTheInstall()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""specialize"">
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>1</Order><Description>" + DisableDescription + @"</Description><Path>" + DisablePath + @"</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>2</Order><Description>" + DriverInstallStepWriter.ExtractDescription + @"</Description><Path>" + ExtractPath + @"</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>3</Order><Description>Bypass</Description><Path>reg.exe add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
  </settings>
</unattend>");

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        var amd64 = SpecializeComponent(Written(), "amd64");
        OrderOf(Described(amd64, DriverInstallStepWriter.ExtractDescription)).Should().Be("1");
        OrderOf(Described(amd64, "Bypass")).Should().Be("2");
        OrderOf(MarkerCommand(amd64)).Should().Be("3");
        OrderOf(Described(amd64, DisableDescription)).Should().Be("4");
        Commands(amd64).Last().Should().BeSameAs(Described(amd64, DisableDescription));
    }

    [Fact]
    public async Task EnsureAsync_TwoDisables_MoveBothBehindTheInstallInOrder()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <settings pass=""specialize"">
    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add""><Order>1</Order><Description>" + DriverInstallStepWriter.ExtractDescription + @"</Description><Path>" + ExtractPath + @"</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>2</Order><Description>Disable adapter A</Description><Path>" + DisableAdapterA + @"</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>3</Order><Description>Bypass</Description><Path>reg.exe add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>4</Order><Description>Disable adapter B</Description><Path>" + DisableAdapterB + @"</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add""><Order>5</Order><Description>Fifth</Description><Path>cmd.exe /c echo five</Path></RunSynchronousCommand>
      </RunSynchronous>
    </component>
  </settings>
</unattend>");

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        var amd64 = SpecializeComponent(Written(), "amd64");
        Commands(amd64).Select(OrderOf).Should().Equal("1", "2", "3", "4", "5", "6");
        OrderOf(Described(amd64, "Bypass")).Should().Be("2");
        OrderOf(Described(amd64, "Fifth")).Should().Be("3");
        OrderOf(MarkerCommand(amd64)).Should().Be("4");
        Commands(amd64).TakeLast(2).Select(PathOf).Should().Equal(DisableAdapterA, DisableAdapterB);
    }

    [Fact]
    public async Task EnsureAsync_RootWithNoSettingsAtAll_PutsTheNewSettingsBeforeTheExtensions()
    {
        StageDrivers();
        MediaXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <Extensions xmlns=""urn:custom:extensions""><Data>kept</Data></Extensions>
</unattend>");

        var result = await Sut().EnsureAsync(Work);

        result.Should().Be(DriverInstallStepResult.Added);
        var doc = Written();
        doc.Root!.Elements().Select(e => e.Name.LocalName).Should().Equal("settings", "Extensions");
        foreach (var architecture in new[] { "x86", "arm64", "amd64" })
        {
            var component = SpecializeComponent(doc, architecture);
            OrderOf(Described(component, DriverInstallStepWriter.ExtractDescription)).Should().Be("1");
            OrderOf(MarkerCommand(component)).Should().Be("2");
        }
    }
}
