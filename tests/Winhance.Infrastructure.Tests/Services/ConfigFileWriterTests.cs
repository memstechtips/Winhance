using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ConfigFileWriterTests
{
    private const string OutputPath = @"C:\Users\Test\Winhance_Config.winhance";

    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<ILogService> _log = new();
    private string _written = string.Empty;

    public ConfigFileWriterTests()
    {
        _registry.Setup(r => r.GetAll(It.IsAny<bool>())).Returns(new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Privacy] = new[] { ParityFixtures.Toggle("t") },
            [FeatureIds.ExplorerCustomization] = new[] { ParityFixtures.Selection("s") },
        });

        _files
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Callback<string, string, CancellationToken>((_, contents, _) => _written = contents)
            .Returns(Task.CompletedTask);
    }

    private ConfigFileWriter Sut() => new(_registry.Object, _files.Object, _log.Object);

    private static SelectionSet Selections() => new(
        [new SettingChoice("t", new ChoiceValue.Toggle(true)), new SettingChoice("s", new ChoiceValue.Option(1))],
        Array.Empty<AppChoice>(),
        Array.Empty<AppChoice>(),
        AutounattendChoices.None);

    [Fact]
    public async Task WriteAsync_SerializesWithTheFrozenOptions()
    {
        await Sut().WriteAsync(Selections(), CatalogScope.CurrentMachine, OutputPath);

        _written.Should().Contain("\"Version\": \"2.0\"");
        _written.Should().Contain("\n  ", "the frozen JsonOptions write indented JSON");
        _written.Should().Contain("\"Id\": \"t\"").And.Contain("\"IsSelected\": true");
        _written.Should().Contain("\"Id\": \"s\"").And.Contain("\"SelectedIndex\": 1");
        _written.Should().NotContain("PowerSettings", "the frozen JsonOptions omit null members");
    }

    [Fact]
    public async Task WriteAsync_ForwardsTheScopeToTheRegistry()
    {
        await Sut().WriteAsync(Selections(), new CatalogScope(IncludeOtherOsVersions: true, IncludeOtherHardware: false), OutputPath);

        _registry.Verify(r => r.InitializeAsync(), Times.Once);
        _registry.Verify(r => r.GetAll(true), Times.Once);
    }
}
