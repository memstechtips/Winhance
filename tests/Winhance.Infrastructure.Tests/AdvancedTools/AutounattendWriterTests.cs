using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class AutounattendWriterTests
{
    private const string OutputPath = @"C:\Users\Test\autounattend.xml";

    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<IAutounattendScriptBuilder> _builder = new();
    private readonly Mock<IPowerShellRunner> _ps = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<ILogService> _log = new();
    private string? _writtenPath;
    private string _written = string.Empty;

    public AutounattendWriterTests()
    {
        _registry.Setup(r => r.GetAll(It.IsAny<bool>())).Returns(ParityCatalog.ByFeature);
        _builder.Setup(b => b.BuildAsync(It.IsAny<SelectionSet>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<Setting>>>()))
            .ReturnsAsync("Write-Host 'Winhancements'");
        _ps.Setup(p => p.ValidateXmlSyntaxAsync(It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _files.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Callback<string, string, CancellationToken>((path, contents, _) => { _writtenPath = path; _written = contents; })
            .Returns(Task.CompletedTask);
    }

    private AutounattendWriter Sut() => new(_registry.Object, _builder.Object, _ps.Object, _files.Object, _log.Object);

    [Fact]
    public async Task WriteAsync_InjectsTheScriptIntoTheTemplate_ValidatesAndWrites()
    {
        var result = await Sut().WriteAsync(SelectionSet.Empty, new CatalogScope(IncludeOtherOsVersions: true, IncludeOtherHardware: false), OutputPath);

        result.Should().Be(OutputPath);
        _writtenPath.Should().Be(OutputPath);
        _written.Should().StartWith("<?xml");
        _written.Should().Contain("<![CDATA[Write-Host 'Winhancements']]>");
        _written.Should().NotContain("<!--SCRIPT_PLACEHOLDER-->");
        _registry.Verify(r => r.InitializeAsync(), Times.Once);
        _registry.Verify(r => r.GetAll(true), Times.Once);
        _builder.Verify(b => b.BuildAsync(SelectionSet.Empty, ParityCatalog.ByFeature), Times.Once);
        _ps.Verify(p => p.ValidateXmlSyntaxAsync(It.Is<string>(x => x.Contains("CDATA")), default), Times.Once);
        _log.Verify(l => l.Log(LogLevel.Info, It.Is<string>(m => m.Contains("Generating autounattend.xml")), null), Times.Once);
        _log.Verify(l => l.Log(LogLevel.Info, It.Is<string>(m => m.Contains("generated successfully")), null), Times.Once);
    }

    [Fact]
    public async Task WriteAsync_CurrentMachineScope_AsksTheRegistryForThisOsOnly()
    {
        await Sut().WriteAsync(SelectionSet.Empty, CatalogScope.CurrentMachine, OutputPath);

        _registry.Verify(r => r.GetAll(false), Times.Once);
    }

    [Fact]
    public async Task WriteAsync_XmlValidationFailure_ThrowsAndWritesNothing()
    {
        _ps.Setup(p => p.ValidateXmlSyntaxAsync(It.IsAny<string>(), default)).ThrowsAsync(new InvalidOperationException("bad xml"));

        var act = () => Sut().WriteAsync(SelectionSet.Empty, CatalogScope.CurrentMachine, OutputPath);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _log.Verify(l => l.Log(LogLevel.Error, It.Is<string>(m => m.Contains("failed XML well-formedness validation")), null), Times.Once);
    }
}
