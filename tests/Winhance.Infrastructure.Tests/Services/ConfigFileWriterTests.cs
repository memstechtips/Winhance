using FluentAssertions;
using Moq;
using Winhance.Core.Features.Autounattend;
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
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(new Dictionary<string, IReadOnlyList<Setting>>
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
        Array.Empty<AppChoice>());

    private const string TypedPassword = "hunter2";

    private static readonly ChoiceValue.ListRow[] AccountRows =
    [
        new(new Dictionary<string, string>
        {
            ["name"] = "marco",
            ["display-name"] = "Marco",
            ["group"] = "0",
            ["password"] = TypedPassword,
            ["obscure"] = "true",
            ["auto-logon"] = "true",
        }),
    ];

    private void RegisterAccountsSetting() => _registry
        .Setup(r => r.GetAll(It.IsAny<CatalogScope>()))
        .Returns(new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Autounattend] = new[] { ParityFixtures.ListSetting("autounattend-accounts") },
        });

    private static SelectionSet AccountSelections(bool savePasswords) => new(
        [new SettingChoice("autounattend-accounts", new ChoiceValue.List(AccountRows, savePasswords))],
        Array.Empty<AppChoice>(),
        Array.Empty<AppChoice>());

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
        _registry.Verify(r => r.GetAll(new CatalogScope(true, false)), Times.Once);
    }

    [Fact]
    public async Task WriteAsync_AuthoredAccounts_WritesTheObscuredPasswordWhenTheCardSaidSo()
    {
        RegisterAccountsSetting();

        await Sut().WriteAsync(AccountSelections(savePasswords: true), CatalogScope.CurrentMachine, OutputPath);

        _written.Should().NotContain(TypedPassword);
        _written.Should().Contain(AccountPasswords.Obscure(TypedPassword, "Password"));
    }

    [Fact]
    public async Task WriteAsync_AuthoredAccounts_WritesNoPasswordAtAllByDefault()
    {
        RegisterAccountsSetting();

        await Sut().WriteAsync(AccountSelections(savePasswords: false), CatalogScope.CurrentMachine, OutputPath);

        _written.Should().NotContain(TypedPassword);
        _written.Should().NotContain(AccountPasswords.Obscure(TypedPassword, "Password"));
        _written.Should().Contain("\"name\": \"marco\"");
    }

    [Fact]
    public async Task WriteAsync_AuthoredPowerPlan_WritesTheKeyAndLabel()
    {
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Power] = new[] { ParityFixtures.PowerPlanSetting() },
        });
        var set = new SelectionSet(
            [new SettingChoice("power-plan-selection", new ChoiceValue.Keyed("381b4222-f694-41f0-9685-ff5bb260df2e", "Balanced"))],
            Array.Empty<AppChoice>(),
            Array.Empty<AppChoice>());

        await Sut().WriteAsync(set, CatalogScope.CurrentMachine, OutputPath);

        _written.Should().Contain("\"SelectedKey\": \"381b4222-f694-41f0-9685-ff5bb260df2e\"");
        _written.Should().Contain("\"SelectedKeyLabel\": \"Balanced\"");
        _written.Should().Contain("\"PowerPlanGuid\": \"381b4222-f694-41f0-9685-ff5bb260df2e\"");
        _written.Should().NotContain("SelectedIndex");
    }
}
