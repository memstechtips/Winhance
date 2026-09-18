using System.Xml.Linq;
using Winhance.Core.Features.Common.Catalog;

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
    private readonly (string Id, string Name)[] _architectures =
    [
        .. IdOrder.Select(id =>
            (Id: id, Name: SettingCatalog.ById[id].Targets.OfType<AutounattendArchitecture>().Single().Architecture)),
    ];

    public IReadOnlyCollection<string> Handles => [.. _architectures.Select(a => a.Id)];

    public void Write(XDocument doc, AutounattendRenderContext context)
    {
        var selected = _architectures.Where(a => context.Checked(a.Id) == true).Select(a => a.Name).ToList();
        // A file that covers every architecture installs on any of them, so unticking all three is not an error.
        if (selected.Count == 0)
            selected = [.. _architectures.Select(a => a.Name)];

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
