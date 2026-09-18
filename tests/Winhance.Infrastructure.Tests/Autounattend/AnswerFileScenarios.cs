using Winhance.Core.Features.Common.Selections;

namespace Winhance.Infrastructure.Tests.Autounattend;

internal sealed record AnswerFileScenario(string Name, SelectionSet Selections);

internal static class AnswerFileScenarios
{
    private const string OwnProductKey = "ABCDE-FGHIJ-KLMNO-PQRST-UVWXY";

    private static readonly ChoiceValue.ListRow[] TwoAccounts =
    [
        new(new Dictionary<string, string>
        {
            ["name"] = "marco",
            ["display-name"] = "Marco",
            ["group"] = "0",
            ["password"] = "hunter2",
            ["obscure"] = "true",
            ["auto-logon"] = "true",
        }),
        new(new Dictionary<string, string>
        {
            ["name"] = "guest",
            ["display-name"] = "Guest",
            ["group"] = "1",
            ["password"] = string.Empty,
            ["obscure"] = "false",
            ["auto-logon"] = "false",
        }),
    ];

    // These cards name what the user sees and the file what Setup suppresses, so false still writes an element.
    private static readonly SettingChoice[] DefaultChoices =
    [
        new("autounattend-architecture-x64", new ChoiceValue.CheckBox(true)),
        new("autounattend-architecture-arm64", new ChoiceValue.CheckBox(true)),
        new("autounattend-architecture-x86", new ChoiceValue.CheckBox(true)),
        new("autounattend-edition", new ChoiceValue.Option(0)),
        new("autounattend-hardware-checks", new ChoiceValue.Toggle(false)),
        new("autounattend-account", new ChoiceValue.Option(0)),
        new("autounattend-licence-page", new ChoiceValue.Toggle(false)),
        new("autounattend-oem-registration-page", new ChoiceValue.Toggle(false)),
        new("autounattend-network-page", new ChoiceValue.Toggle(false)),
        new("autounattend-privacy", new ChoiceValue.Option(0)),
        new("autounattend-netfx3", new ChoiceValue.Toggle(true)),
        new("autounattend-desktop-shortcut", new ChoiceValue.Toggle(true)),
    ];

    public static IReadOnlyList<AnswerFileScenario> All { get; } =
    [
        new("defaults", Set()),

        new(
            "this-pc-own-key",
            Set(
                Choice("autounattend-architecture-arm64", new ChoiceValue.CheckBox(false)),
                Choice("autounattend-architecture-x86", new ChoiceValue.CheckBox(false)),
                Choice("autounattend-edition", new ChoiceValue.Option(14)),
                Choice("autounattend-product-key", new ChoiceValue.Text(OwnProductKey)))),

        new(
            "firmware-everything-shown",
            Set(
                Choice("autounattend-edition", new ChoiceValue.Option(15)),
                Choice("autounattend-hardware-checks", new ChoiceValue.Toggle(true)),
                Choice("autounattend-licence-page", new ChoiceValue.Toggle(true)),
                Choice("autounattend-oem-registration-page", new ChoiceValue.Toggle(true)),
                Choice("autounattend-network-page", new ChoiceValue.Toggle(true)),
                Choice("autounattend-privacy", new ChoiceValue.Option(2)),
                Choice("autounattend-netfx3", new ChoiceValue.Toggle(false)))),

        new(
            "create-now-two-accounts",
            Set(
                Choice("autounattend-account", new ChoiceValue.Option(2)),
                Choice("autounattend-accounts", new ChoiceValue.List(TwoAccounts)))),

        new(
            "microsoft-express-named",
            Set(
                Choice("autounattend-account", new ChoiceValue.Option(1)),
                Choice("autounattend-privacy", new ChoiceValue.Option(1)),
                Choice("autounattend-computer-name", new ChoiceValue.Text("PC-01")))),

        // Nothing typed still writes the ask placeholder, so the empty text has to be in the set.
        new(
            "own-key-empty",
            Set(
                Choice("autounattend-edition", new ChoiceValue.Option(14)),
                Choice("autounattend-product-key", new ChoiceValue.Text(string.Empty)))),

        new("shortcut-off", Set(Choice("autounattend-desktop-shortcut", new ChoiceValue.Toggle(false)))),

        new(
            "generic-pro-n",
            Set(Choice("autounattend-edition", new ChoiceValue.Option(5)))),

        new(
            "embedded-wallpaper",
            Carrying(
                new CarriedFile("theme-wallpaper-picture", @"C:\Windows\Web\Wallpaper\Winhance\holiday.jpg", "QUJD"),
                Choice("theme-wallpaper-picture",
                    new ChoiceValue.Keyed(@"C:\Windows\Web\Wallpaper\Winhance\holiday.jpg", "holiday.jpg")))),

        new(
            "with-region",
            Set(
                Choice("region-format", new ChoiceValue.Keyed("en-GB", "English (United Kingdom)")),
                Choice("region-system-locale", new ChoiceValue.Keyed("en-GB", "English (United Kingdom)")),
                Choice("region-keyboard-layout", new ChoiceValue.Keyed("00000809", "United Kingdom")),
                Choice("region-time-zone", new ChoiceValue.Keyed("GMT Standard Time", "(UTC+00:00) Dublin, Edinburgh, Lisbon, London")))),
    ];

    private static SettingChoice Choice(string id, ChoiceValue value) => new(id, value);

    private static SelectionSet Carrying(CarriedFile file, params SettingChoice[] overrides) =>
        Set(overrides) with { Files = [file] };

    private static SelectionSet Set(params SettingChoice[] overrides)
    {
        var choices = new List<SettingChoice>(DefaultChoices);
        foreach (var choice in overrides)
        {
            var at = choices.FindIndex(existing => existing.SettingId == choice.SettingId);
            if (at < 0)
                choices.Add(choice);
            else
                choices[at] = choice;
        }

        return SelectionSet.Empty with { Settings = choices };
    }
}
