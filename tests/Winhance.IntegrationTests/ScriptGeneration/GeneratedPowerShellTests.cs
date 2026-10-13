using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Autounattend.ScriptSections;
using Winhance.Infrastructure.Features.Autounattend.Services;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.Utilities;
using Winhance.IntegrationTests.Helpers;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.IntegrationTests.ScriptGeneration;

// Runs the REAL Windows PowerShell on the generating machine: the whole live-catalog script must parse, the two
// validators must actually fail on bad input, and the registry helpers the old emitters lacked must do to an HKCU
// scratch key what WindowsRegistryService does.
[Trait("Category", "Integration")]
public class GeneratedPowerShellTests
{
    private static PowerShellRunner Runner() => new(new FileSystemService());

    private static WinhancementsScriptBuilder Builder(IPowerShellRunner runner, ILocalizationService? localization = null)
    {
        var version = new Mock<IWindowsVersionService>();
        version.Setup(v => v.GetWindowsBuildNumber()).Returns(26100);
        version.Setup(v => v.GetWindowsBuildRevision()).Returns(4000);
        return new WinhancementsScriptBuilder(
            new Mock<ILogService>().Object,
            runner,
            version.Object,
            localization ?? new Mock<ILocalizationService>().Object,
            OptionProviderFixtures.Registry());
    }

    // One choice per live setting: toggles on, selections at their first option, sliders at a value off the unit grid
    // (600 s / 300 s), so every emission path and every helper goes through the parser.
    private static SelectionSet LiveCatalogChoices(int winhanceCardOption)
    {
        var choices = new List<SettingChoice>();
        foreach (var setting in SettingCatalog.ByFeature.Values.SelectMany(x => x))
        {
            ChoiceValue? value = setting.Control switch
            {
                ControlKind.Toggle or ControlKind.Action => new ChoiceValue.Toggle(true),
                ControlKind.CheckBox => new ChoiceValue.CheckBox(true),
                // The Winhance card's first option writes no script; the other two carry the install script, the shortcut
                // only in a file that installs apps, as this one does.
                ControlKind.Selection => new ChoiceValue.Option(setting.Id == "autounattend-desktop-shortcut" ? winhanceCardOption : 0),
                ControlKind.Slider => new ChoiceValue.AcDcNumber(600, 300),
                ControlKind.KeyedSelection => KeyedChoice(setting.Id),
                // The slideshow album applies through a script, so it needs a folder for that script to be parsed.
                // PowerShell reads the typographic apostrophe as a quote, and a folder name can hold one.
                ControlKind.TextBox => new ChoiceValue.Text("C:\\Users\\Mom\u2019s photos\\Album"),
                _ => null,
            };
            if (value is not null) choices.Add(new SettingChoice(setting.Id, value));
        }
        // An app id lands in the install script inside a single-quoted literal, so one holding an apostrophe must still parse.
        return new SelectionSet(choices, Array.Empty<AppChoice>(), [new AppChoice("mom's-app", "Mom's App", null, null, null, null)]);
    }

    // es-ES is deliberate: its long date is `dddd, d 'de' MMMM 'de' yyyy`, so the emitted -Value has to double
    // those quotes or the whole script stops parsing. The plan name's typographic double quote is unpaired on
    // purpose, so an unescaped one cannot balance out.
    private static ChoiceValue KeyedChoice(string settingId) => settingId switch
    {
        "power-plan-selection" => new ChoiceValue.Keyed("11111111-2222-3333-4444-555555555555", "Dad\u2019s 27\u201D plan"),
        "region-time-zone" => new ChoiceValue.Keyed("South Africa Standard Time", "(UTC+02:00) Harare, Pretoria"),
        "region-format" => new ChoiceValue.Keyed("es-ES", "Spanish (Spain)"),
        "region-home-location" => new ChoiceValue.Keyed("ZA", "South Africa"),
        "region-keyboard-layout" => new ChoiceValue.Keyed("00000409", "US"),
        "region-system-locale" => new ChoiceValue.Keyed("en-US", "English (United States)"),
        "region-display-language" => new ChoiceValue.Keyed("en-GB", "English (United Kingdom)"),
        "autounattend-setup-language" => new ChoiceValue.Keyed("en-GB", "English (United Kingdom)"),
        "theme-wallpaper-picture" => new ChoiceValue.Keyed(@"C:\Windows\Web\Wallpaper\Windows\img0.jpg", "Windows 11 light"),
        "theme-wallpaper-color" => new ChoiceValue.Keyed("#FF8C00", "#FF8C00"),
        "explorer-customization-short-date" => new ChoiceValue.Keyed("dd/MM/yyyy", "dd/MM/yyyy"),
        _ => throw new InvalidOperationException($"{settingId} is keyed but names no live key here, so the parse gate would skip it"),
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task LiveCatalogScript_ParsesUnderWindowsPowerShell(int winhanceCardOption)
    {
        var script = await Builder(Runner()).BuildAsync(LiveCatalogChoices(winhanceCardOption), SettingCatalog.ByFeature);

        script.Should().Contain("function Set-RegistryCompositeValue");
        script.Should().Contain("Set-RegistryCompositeValue -Path").And.Contain("Set-RegistryStringFlag -Path").And.Contain("Lock-RegistryKey -Path");
    }

    [Fact]
    public async Task LiveCatalogScript_ParsesInEveryLanguage()
    {
        var builderRunner = new Mock<IPowerShellRunner>();
        builderRunner
            .Setup(r => r.ValidateScriptSyntaxAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scripts = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(RepoPaths.LocalizationDir(), "*.json"))
        {
            var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(file))!;
            var localization = new Mock<ILocalizationService>();
            localization
                .Setup(l => l.GetString(It.IsAny<string>()))
                .Returns((string key) => strings.TryGetValue(key, out var text) ? text : $"[{key}]");
            localization.MirrorTryGetString();

            scripts[Path.GetFileNameWithoutExtension(file)] = await Builder(builderRunner.Object, localization.Object)
                .BuildAsync(LiveCatalogChoices(1), SettingCatalog.ByFeature);
        }

        scripts.Count.Should().BeGreaterThan(20);
        var errors = await Runner().FindParseErrorsAsync(scripts);
        errors.Should().BeEmpty();
    }

    // Install-Winhance.ps1 is written from a single-quoted here-string, so an app id reaches the file with its quote
    // still doubled, and the file has to parse on its own.
    [Fact]
    public async Task InstallWinhanceScript_ParsesWithAnApostropheInAnAppId()
    {
        var card = SettingCatalog.ByFeature.Values.SelectMany(x => x).Single(s => s.Id == "autounattend-desktop-shortcut");
        var script = card.States[2].Effects.OfType<ScriptEffect>().Single(effect => effect.Run == RunContext.System).Script;
        var start = script.IndexOf("@'", StringComparison.Ordinal) + 2;
        var body = script[start..script.IndexOf("\n'@", start, StringComparison.Ordinal)]
            .Replace("{{external-app-ids}}", "mom''s-app", StringComparison.Ordinal);

        var errors = await Runner().FindParseErrorsAsync(new Dictionary<string, string> { ["Install-Winhance.ps1"] = body });

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateScriptSyntax_RejectsAParseError()
    {
        var act = () => Runner().ValidateScriptSyntaxAsync("function Broken { param($a) \n if ($a) { Write-Host 'missing brace' ");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PARSE_ERROR*");
    }

    [Fact]
    public async Task ValidateXmlSyntax_RejectsMalformedXml()
    {
        var act = () => Runner().ValidateXmlSyntaxAsync("<unattend><settings></unattend>");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*XML_ERROR*");
    }

    [Fact]
    public async Task CompositeAndFlagHelpers_WriteWhatWindowsRegistryServiceWrites()
    {
        var key = $"HKCU:\\Software\\WinhanceTests\\{Guid.NewGuid():N}";
        var calls = new StringBuilder();
        calls.AppendLine($"$k = '{key}'");
        calls.AppendLine("Set-RegistryCompositeValue -Path $k -Name Packed -Key A -SubValue 1 -Description t");
        calls.AppendLine("Set-RegistryCompositeValue -Path $k -Name Packed -Key B -SubValue 2 -Description t");
        calls.AppendLine("Write-Output ('P1=' + (Get-ItemProperty -Path $k -Name Packed).Packed)");
        calls.AppendLine("Set-RegistryCompositeValue -Path $k -Name Packed -Key A -Remove -Description t");
        calls.AppendLine("Write-Output ('P2=' + (Get-ItemProperty -Path $k -Name Packed).Packed)");
        calls.AppendLine("Set-RegistryCompositeValue -Path $k -Name Packed -Key B -SubValue '' -Description t");
        calls.AppendLine("Write-Output ('P3=' + (Get-ItemProperty -Path $k -Name Packed).Packed)");
        calls.AppendLine("Set-RegistryStringFlag -Path $k -Name Flags -FlagMask 4 -AbsentBase 58 -Set $true -Description t");
        calls.AppendLine("Write-Output ('F1=' + (Get-ItemProperty -Path $k -Name Flags).Flags)");
        calls.AppendLine("Set-RegistryStringFlag -Path $k -Name Flags -FlagMask 4 -AbsentBase 58 -Set $false -Description t");
        calls.AppendLine("Write-Output ('F2=' + (Get-ItemProperty -Path $k -Name Flags).Flags)");
        calls.AppendLine("Remove-Item -Path $k -Recurse -Force");

        var sb = new StringBuilder();
        sb.AppendLine("function Write-Log { param($Message, $Level) }");
        ScriptPreambleSection.AppendHelperFunctions(sb, calls.ToString());
        sb.Append(calls);

        var output = await Runner().RunScriptAsync(sb.ToString());

        // WindowsRegistryService.BuildCompositeString: "k=v" joined by ';' with a trailing ';'; null removes, "" writes "k=".
        output.Should().Contain("P1=A=1;B=2;");
        output.Should().Contain("P2=B=2;");
        output.Should().Contain("P3=B=;");
        // WindowsStateWriter.SetRegistryStringFlag: absent -> AbsentBase, then set/clear the mask, stored as a string.
        output.Should().Contain("F1=62");
        output.Should().Contain("F2=58");
    }
}
