using System.Xml.Linq;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Autounattend;
using Winhance.Infrastructure.Features.Autounattend.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

public class UserScriptFilesTests
{
    private const string SystemCard = "autounattend-user-script-system";
    private const string UserCard = "autounattend-user-script-user";

    private static ChoiceValue.ListRow Row(int type, string script) =>
        new(new Dictionary<string, string> { ["type"] = type.ToString(), ["script"] = script });

    private static SelectionSet SetOf(params (string Card, ChoiceValue.ListRow[] Rows)[] cards) =>
        new(cards.Select(c => new SettingChoice(c.Card, new ChoiceValue.List(c.Rows))).ToList(),
            Array.Empty<AppChoice>(), Array.Empty<AppChoice>());

    [Fact]
    public void From_NumbersEachSectionAndTakesTheExtensionFromTheType()
    {
        var set = SetOf(
            (SystemCard, new[] { Row(0, "Write-Host 'a'"), Row(3, "Windows Registry Editor Version 5.00"), Row(1, "echo c") }),
            (UserCard, new[] { Row(2, "echo d") }));

        UserScriptFiles.From(set).Select(s => (s.Run, s.Name)).Should().Equal(
            (RunContext.System, "System-1.ps1"),
            (RunContext.System, "System-2.reg"),
            (RunContext.System, "System-3.cmd"),
            (RunContext.User, "User-1.bat"));
    }

    [Fact]
    public void From_ARowWithNoScript_TakesNoNumber()
    {
        var set = SetOf((SystemCard, new[] { Row(0, "   "), Row(1, "echo b") }));

        UserScriptFiles.From(set).Should().ContainSingle()
            .Which.Path.Should().Be(ScriptPaths.UnattendUserScriptsDirectory + @"\System-1.cmd");
    }

    [Theory]
    [InlineData(SystemCard)]
    [InlineData(UserCard)]
    public void Extensions_MatchTheTypeOptionsOnTheCard(string card)
    {
        var type = SettingCatalog.ById[card].List!.Fields.Single(f => f.Key == "type");

        type.Options.Should().HaveCount(UserScriptFiles.Extensions.Length);
    }

    // The script is data in its own element, so nothing in it can end the text around it.
    [Fact]
    public void Build_CarriesEachScriptAsTypedInAFileOfItsOwn()
    {
        const string script = "$text = @'\nkept\n'@\n# ]]> is fine too";
        var set = SetOf((SystemCard, new[] { Row(0, script) }), (UserCard, new[] { Row(3, "Windows Registry Editor Version 5.00") }));

        var xml = AutounattendDocumentBuilder.Serialize(
            AutounattendDocumentBuilder.Build(new AutounattendRenderContext("Write-Host 'x'", "0.0.0", set)));
        var files = XDocument.Parse(xml).Descendants(AutounattendDocument.WinhanceExtensions + "File")
            .ToDictionary(f => (string)f.Attribute("path")!, f => f.Value);

        files[ScriptPaths.UnattendUserScriptsDirectory + @"\System-1.ps1"].Should().Be(script);
        files[ScriptPaths.UnattendUserScriptsDirectory + @"\User-1.reg"].Should().Be("Windows Registry Editor Version 5.00");
    }
}
