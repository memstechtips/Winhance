using System.Xml.Linq;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

// Setup documents RunSynchronousCommand\Path as "a string with a maximum length of 259
// characters", and what happens past it is undocumented and silent either way. The template's
// longest command sits at exactly 258 on purpose; everything the writer emits must respect the
// same cap.
public class AnswerFileCommandLengthTests
{
    private const int PathCap = 259;

    private static readonly XNamespace U = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace X = "urn:winhance:unattend";

    private static List<string> CommandValues(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Descendants(U + "Path").Select(p => p.Value)
            .Concat(doc.Descendants(U + "CommandLine").Select(c => c.Value))
            .ToList();
    }

    [Fact]
    public void TemplateCommands_StayUnderTheDocumentedCap()
    {
        var commands = CommandValues(AutounattendWriter.LoadTemplate());

        commands.Should().HaveCountGreaterThan(20);
        commands.Where(c => c.Length > PathCap).Should().BeEmpty();
    }

    [Fact]
    public void WriterCommands_StayUnderTheDocumentedCap()
    {
        DriverInstallStepWriter.InstallCommand.Length.Should().BeLessThanOrEqualTo(PathCap);
    }

    [Fact]
    public async Task WriterOverTheRealTemplate_ProducesOnlyCapCompliantCommands()
    {
        var files = new Mock<IFileSystemService>();
        files.Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));
        files.Setup(f => f.DirectoryExists("C:\\work\\sources\\$OEM$\\$$\\Drivers")).Returns(true);
        files.Setup(f => f.FileExists("C:\\work\\autounattend.xml")).Returns(true);
        files.Setup(f => f.ReadAllTextAsync("C:\\work\\autounattend.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutounattendWriter.LoadTemplate());
        string? written = null;
        files.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, contents, _) => written = contents)
            .Returns(Task.CompletedTask);
        var writer = new DriverInstallStepWriter(files.Object, new Mock<ILogService>().Object);

        var result = await writer.EnsureAsync("C:\\work");

        result.Should().Be(DriverInstallStepResult.Added);
        var commands = CommandValues(written!);
        commands.Where(c => c.Length > PathCap).Should().BeEmpty();

        // The real template loads scripts at Order 1 and disables adapters at Order 3 in all three
        // specialize components: the install lands at 5 and the disable moves to 6, gap closed.
        var doc = XDocument.Parse(written!);
        foreach (var architecture in new[] { "x86", "arm64", "amd64" })
        {
            var commandsByOrder = doc.Root!.Elements(U + "settings")
                .Single(s => (string?)s.Attribute("pass") == "specialize")
                .Elements(U + "component")
                .Single(c => (string?)c.Attribute("processorArchitecture") == architecture)
                .Element(U + "RunSynchronous")!
                .Elements(U + "RunSynchronousCommand")
                .ToDictionary(c => c.Element(U + "Order")!.Value, c => c);

            commandsByOrder.Keys.Should().BeEquivalentTo("1", "2", "3", "4", "5", "6");
            commandsByOrder["3"].Element(U + "Description")!.Value.Should().Contain(".NET Framework 3.5");
            commandsByOrder["5"].Element(U + "Description")!.Value.Should().Be(DriverInstallStepWriter.Marker);
            commandsByOrder["5"].Element(U + "Path")!.Value.Should().Be(DriverInstallStepWriter.InstallCommand);
            commandsByOrder["6"].Element(U + "Path")!.Value.Should().Contain("Disable-NetAdapter");
            commandsByOrder.Values.Count(c => c.Element(U + "Path")!.Value.Contains("Disable-NetAdapter", StringComparison.Ordinal)).Should().Be(1);
        }

        // The command points at the file the same XML carries, and the extractor is not duplicated.
        var extensions = doc.Root!.Element(X + "Extensions")!;
        extensions.Elements(X + "ExtractScript").Should().HaveCount(1);
        var file = extensions.Elements(X + "File").Single(f => (string?)f.Attribute("path") == DriverInstallStepWriter.ScriptPath);
        file.Value.Should().Be(DriverInstallStepWriter.InstallScript.ReplaceLineEndings("\n"));
        var path = file.Attribute("path")!.Value;
        DriverInstallStepWriter.InstallCommand.Should().EndWith("-File \"" + path + "\"");
    }
}
