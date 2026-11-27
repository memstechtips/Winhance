using System.Xml.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Infrastructure.Features.Autounattend;

internal sealed record AutounattendRenderContext(
    string WinhancementsScript,
    string AppVersion,
    SelectionSet Selections)
{
    // The .winhance form of Selections, so Winhance can reopen the file; null leaves it out.
    public string? Config { get; init; }

    public ChoiceValue? Value(string settingId) => Selections.Value(settingId);

    public IReadOnlyList<(string Destination, string Base64)> EmbeddedPictures =>
        Selections.Files
            .Where(file => file.Base64.Length > 0)
            .Select(file => (file.Destination, file.Base64))
            .ToList();

    // An empty key answers null: Setup reads an empty locale element as a request for a blank locale.
    public string? Key(string settingId) =>
        Value(settingId) is ChoiceValue.Keyed { Key.Length: > 0 } keyed ? keyed.Key : null;

    public SettingState? StateOf(string settingId) => Selections.StateOf(settingId);

    public bool? Checked(string settingId) =>
        SettingCatalog.ById.TryGetValue(settingId, out var setting) && StateOf(settingId) is { } state
            ? state.Label == TwoState.OnLabel(setting.Control)
            : null;

    public string? Text(string settingId)
    {
        if (Value(settingId) is ChoiceValue.Text typed)
            return typed.Value;

        return SettingCatalog.ById.TryGetValue(settingId, out var setting) ? setting.TextBox?.Default : null;
    }
}

internal interface IAutounattendElementWriter
{
    void Write(XDocument doc, AutounattendRenderContext context);

    // Setting ids the generic AutounattendElementWriter leaves to this writer.
    IReadOnlyCollection<string> Handles => [];
}
