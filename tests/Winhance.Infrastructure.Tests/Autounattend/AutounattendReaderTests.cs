using System.Text.RegularExpressions;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Autounattend.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Autounattend.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

public class AutounattendReaderTests
{
    private const string FilePath = @"C:\Media\autounattend.xml";

    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<IWinhancementsScriptBuilder> _scripts = new();
    private readonly Mock<IPowerShellRunner> _ps = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<IVersionService> _version = new();
    private readonly Mock<IConfigMigrationService> _migration = new();
    private readonly Mock<ILogService> _log = new();
    private string _written = string.Empty;

    public AutounattendReaderTests()
    {
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(ParityCatalog.ByFeature);
        _scripts.Setup(b => b.BuildAsync(It.IsAny<SelectionSet>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<Setting>>>(), It.IsAny<WinBuild?>()))
            .ReturnsAsync("Write-Host 'Winhancements'");
        _ps.Setup(p => p.ValidateXmlSyntaxAsync(It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _version.Setup(v => v.GetCurrentVersion()).Returns(new VersionInfo { Version = "26.09.23" });
        _files.Setup(f => f.WriteAllTextAsync(FilePath, It.IsAny<string>(), default))
            .Callback<string, string, CancellationToken>((_, contents, _) => _written = contents)
            .Returns(Task.CompletedTask);
        _files.Setup(f => f.ReadAllTextAsync(FilePath, default)).ReturnsAsync(() => _written);
    }

    private AutounattendReader Sut() => new(_files.Object, _migration.Object, _log.Object);

    private Task WriteAsync(SelectionSet set) =>
        new AutounattendWriter(_registry.Object, _scripts.Object, _ps.Object, _files.Object, _version.Object, _log.Object)
            .WriteAsync(set, CatalogScope.CurrentMachine, FilePath);

    [Fact]
    public async Task A_file_Winhance_wrote_reads_back_as_the_choices_it_was_written_from()
    {
        var choice = new SettingChoice("parity-toggle-hklm", new ChoiceValue.Toggle(false));
        await WriteAsync(new SelectionSet([choice], [new AppChoice("app", "App", null, "App.Capability", null, null)], []));

        var read = await Sut().ReadAsync(FilePath);

        read!.Version.Should().Be("26.09.23");
        var set = ConfigFileMapper.FromFile(read.Config!, ParityCatalog.ByFeature);
        set.Settings.Should().Equal(choice);
        set.WindowsApps.Select(a => a.Id).Should().Equal("app");
        _migration.Verify(m => m.MigrateConfig(read.Config!), Times.Once);
    }

    // A choice the reopen drops is lost on the next Generate, so every answer-file card has to come back as written.
    [Fact]
    public async Task Every_answer_file_card_reads_back_as_the_choice_it_was_written_from()
    {
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(SettingCatalog.ByFeature);
        var written = SettingCatalog.All.Where(s => s.IsAnswerFileOnly)
            .Select(s => new SettingChoice(s.Id, SampleFor(s)))
            .ToList();
        await WriteAsync(new SelectionSet(written, [], []));

        var read = await Sut().ReadAsync(FilePath);

        var back = ConfigFileMapper.FromFile(read!.Config!, SettingCatalog.ByFeature).Settings
            .ToDictionary(c => c.SettingId, c => c.Value);
        foreach (var choice in written)
        {
            // Setup reads the passwords from the same file, so the reopen copy keeps them too.
            var expected = choice.Value is ChoiceValue.List list ? list with { SavePasswords = true } : choice.Value;
            back.Should().ContainKey(choice.SettingId).WhoseValue.Should().Be(expected, choice.SettingId);
        }
    }

    [Fact]
    public async Task A_carried_picture_reads_back_with_its_bytes()
    {
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(SettingCatalog.ByFeature);
        var picture = SettingCatalog.All.First(s => s.Options?.Source == OptionSource.Pictures);
        const string destination = @"C:\Windows\Web\Wallpaper\Winhance\beach.jpg";
        await WriteAsync(new SelectionSet([new SettingChoice(picture.Id, new ChoiceValue.Keyed(destination, "beach"))], [], [])
        {
            Files = [new CarriedFile(picture.Id, destination, "AAAA")],
        });

        var read = await Sut().ReadAsync(FilePath);

        ConfigFileMapper.FromFile(read!.Config!, SettingCatalog.ByFeature).Files
            .Should().Equal(new CarriedFile(picture.Id, destination, "AAAA"));
    }

    private static ChoiceValue SampleFor(Setting setting) => setting.Control switch
    {
        ControlKind.Toggle or ControlKind.Action => new ChoiceValue.Toggle(true),
        ControlKind.CheckBox => new ChoiceValue.CheckBox(true),
        ControlKind.Selection => new ChoiceValue.Option(Math.Max(0, setting.States.Count - 1)),
        ControlKind.TextBox => new ChoiceValue.Text("round-trip"),
        ControlKind.KeyedSelection => new ChoiceValue.Keyed("key", "Label"),
        ControlKind.CheckList => new ChoiceValue.Keys(["Windows 11 Pro", "Windows 11 Home"]),
        ControlKind.List => new ChoiceValue.List([new ChoiceValue.ListRow(setting.List!.Fields.ToDictionary(
            f => f.Key,
            f => f.Kind switch
            {
                FieldKind.Selection => "0",
                FieldKind.CheckBox => "true",
                FieldKind.Password => "secret",
                _ => "value",
            }))]),
        _ => throw new InvalidOperationException($"{setting.Id} is a {setting.Control}, which no answer-file card uses."),
    };

    [Fact]
    public async Task A_file_from_before_the_choices_were_carried_reads_as_Winhance_with_no_choices()
    {
        await WriteAsync(SelectionSet.Empty);
        _written = Regex.Replace(_written, @"\s*<Config>[\s\S]*?</Config>", string.Empty);

        var read = await Sut().ReadAsync(FilePath);

        read.Should().Be(new WinhanceAnswerFile("26.09.23", null));
    }

    [Fact]
    public async Task A_file_whose_carried_choices_have_a_null_section_reads_as_Winhance_with_no_choices()
    {
        await WriteAsync(SelectionSet.Empty);
        _written = Regex.Replace(_written, @"<Config>[\s\S]*?</Config>", "<Config>{\"Optimize\":null}</Config>");

        var read = await Sut().ReadAsync(FilePath);

        read.Should().Be(new WinhanceAnswerFile("26.09.23", null));
    }

    [Fact]
    public async Task A_file_Winhance_did_not_write_reads_as_null()
    {
        _written = "<?xml version=\"1.0\"?><unattend xmlns=\"urn:schemas-microsoft-com:unattend\"><settings pass=\"specialize\" /></unattend>";

        (await Sut().ReadAsync(FilePath)).Should().BeNull();
    }

    [Fact]
    public async Task A_file_that_is_not_xml_reads_as_null()
    {
        _written = "not xml";

        (await Sut().ReadAsync(FilePath)).Should().BeNull();
    }
}
