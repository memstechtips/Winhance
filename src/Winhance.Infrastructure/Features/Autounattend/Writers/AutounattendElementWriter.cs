using System.Xml.Linq;
using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Infrastructure.Features.Autounattend.Writers;

// A RunSynchronous Order is the command's position in its component's list, so two settings naming one component
// are numbered in SettingCatalog.All order.
internal sealed class AutounattendElementWriter(IReadOnlySet<string> claimed) : IAutounattendElementWriter
{
    internal IReadOnlySet<string> Claimed => claimed;

    public void Write(XDocument doc, AutounattendRenderContext context)
    {
        // Created even when empty: in Microsoft-account mode no other writer creates Shell-Setup.
        doc.Pass("oobeSystem").Component("Microsoft-Windows-Shell-Setup");

        foreach (var setting in SettingCatalog.All)
        {
            if (claimed.Contains(setting.Id))
                continue;

            // An answer-file-only setting may render through commands alone; a live one needs an element.
            if (!setting.IsAnswerFileOnly && !setting.Targets.OfType<AutounattendElement>().Any())
                continue;

            switch (setting.Control)
            {
                case ControlKind.TextBox:
                    WriteValue(doc, setting, context.Text(setting.Id));
                    break;

                case ControlKind.KeyedSelection:
                    WriteValue(doc, setting, context.Key(setting.Id));
                    break;

                default:
                    if (context.StateOf(setting.Id) is { } state)
                        WriteState(doc, setting, state);
                    break;
            }
        }
    }

    // Absent and DeleteOnWrite carry no payload: the element is left out, which Setup reads as "ask me".
    internal static void WriteState(XDocument doc, Setting setting, SettingState state)
    {
        foreach (var target in setting.Targets.OfType<AutounattendElement>())
            if (state.Set.TryGetValue(target.Key, out var value) && value.WritePayload is string text)
                doc.Pass(target.Pass).Component(target.Component).ElementPath(target.Path).Value = text;

        foreach (var command in state.Effects.OfType<AutounattendCommand>())
            doc.Pass(command.Pass).Component(command.Component)
                .AddRunSynchronousCommand(command.Command, command.Description);

        foreach (var command in state.Effects.OfType<AutounattendFirstLogonCommand>())
            doc.Pass("oobeSystem").Component("Microsoft-Windows-Shell-Setup")
                .AddFirstLogonCommand(command.Command, command.Description);
    }

    // Setup wants no element rather than an empty one.
    private static void WriteValue(XDocument doc, Setting setting, string? chosen)
    {
        if (string.IsNullOrEmpty(chosen))
            return;

        foreach (var target in setting.Targets.OfType<AutounattendElement>())
            doc.Pass(target.Pass).Component(target.Component).ElementPath(target.Path).Value = chosen;
    }
}
