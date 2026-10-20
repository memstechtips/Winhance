using System.Xml.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Infrastructure.Features.Autounattend.Writers;

internal sealed class ArchitectureWriter : IAutounattendElementWriter
{
    // Setup reads the first component's architecture as the primary one, so this is the order the file lists them in.
    private static readonly string[] IdOrder =
    [
        "autounattend-architecture-x86",
        "autounattend-architecture-arm64",
        "autounattend-architecture-x64",
    ];

    // Single() on purpose: a card that lost its target would leave the file naming fewer architectures than it
    // offers, which shows up only as a refusal on real hardware.
    private static readonly (string Id, string Name)[] Architectures =
    [
        .. IdOrder.Select(id =>
            (Id: id, Name: SettingCatalog.ById[id].Targets.OfType<AutounattendArchitecture>().Single().Architecture)),
    ];

    public IReadOnlyCollection<string> Handles => [.. Architectures.Select(a => a.Id)];

    internal static IReadOnlyList<string> SelectedArchitectures(SelectionSet set)
    {
        var selected = Architectures.Where(a => set.StateOf(a.Id)?.Label == LocKey.Common.Checked).Select(a => a.Name).ToList();
        // A file that covers every architecture installs on any of them, so unticking all three is not an error.
        return selected.Count > 0 ? selected : [.. Architectures.Select(a => a.Name)];
    }

    public void Write(XDocument doc, AutounattendRenderContext context)
    {
        var selected = SelectedArchitectures(context.Selections);

        foreach (var component in doc.Descendants(AutounattendDocument.Unattend + "component").ToList())
        {
            component.SetAttributeValue("processorArchitecture", selected[0]);
            var previous = component;
            foreach (var architecture in selected.Skip(1))
            {
                var clone = new XElement(component);
                clone.SetAttributeValue("processorArchitecture", architecture);
                previous.AddAfterSelf(clone);
                previous = clone;
            }
        }
    }
}
