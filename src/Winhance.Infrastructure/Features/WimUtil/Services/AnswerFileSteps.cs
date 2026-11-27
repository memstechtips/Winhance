using System.Xml.Linq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Autounattend;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal static class AnswerFileSteps
{
    private static readonly XNamespace Unattend = AutounattendDocument.Unattend;
    private static readonly XNamespace Wcm = AutounattendDocument.Wcm;
    private static readonly string[] Architectures = ["x86", "arm64", "amd64"];
    private const string ExtractScriptReference = "Extensions.ExtractScript";

    // The bytes always go out as UTF-8 without a BOM (what Windows Setup expects), so the
    // declaration must say utf-8 regardless of what the source file claimed - echoing a utf-16
    // or windows-1252 declaration over UTF-8 bytes makes Setup reject or mis-decode the file.
    private const string Declaration = @"<?xml version=""1.0"" encoding=""utf-8""?>";

    internal static async Task<(XDocument Doc, XElement Root, bool Existed)> LoadAsync(
        IFileSystemService files, string xmlPath, CancellationToken cancellationToken)
    {
        var existed = files.FileExists(xmlPath);
        var doc = existed
            ? XDocument.Parse(await files.ReadAllTextAsync(xmlPath, cancellationToken).ConfigureAwait(false))
            : new XDocument(new XElement(Unattend + "unattend"));
        var root = doc.Root ?? throw new InvalidOperationException("autounattend.xml has no root element");
        if (root.Attribute(XNamespace.Xmlns + "wcm") is null)
        {
            root.Add(new XAttribute(XNamespace.Xmlns + "wcm", Wcm.NamespaceName));
        }

        return (doc, root, existed);
    }

    internal static Task SaveAsync(IFileSystemService files, string xmlPath, XDocument doc, CancellationToken cancellationToken)
    {
        // An answer file that came off ISO media is read-only and refuses the overwrite.
        if (files.FileExists(xmlPath))
        {
            files.SetFileAttributes(xmlPath, FileAttributes.Normal);
        }

        return files.WriteAllTextAsync(xmlPath, Declaration + Environment.NewLine + doc, cancellationToken);
    }

    // A foreign block keeps its own extractor: both known ones walk every File element whatever
    // the namespace, and both create our folder (Winhance's by name, the other by mkdir
    // on each file's parent), so the script lands the same way.
    internal static bool EnsureScriptFile(XElement root, string scriptPath, string script)
    {
        var extensions = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Extensions");
        if (extensions is null)
        {
            extensions = new XElement(AutounattendDocument.WinhanceExtensions + "Extensions");
            root.Add(extensions);
        }

        var ns = extensions.Name.Namespace;
        var added = false;
        if (extensions.Element(ns + "ExtractScript") is null)
        {
            extensions.AddFirst(new XElement(ns + "ExtractScript", AutounattendScripts.ExtractScript));
            added = true;
        }

        // Refreshed in place so media reused across Winhance versions carries the current script.
        // XML parsing already turned the carried copy's line endings into LF.
        var file = extensions.Elements(ns + "File").FirstOrDefault(f => (string?)f.Attribute("path") == scriptPath);
        if (file is null)
        {
            extensions.Add(new XElement(ns + "File", new XAttribute("path", scriptPath), new XCData(script)));
            added = true;
        }
        else if (file.Value.ReplaceLineEndings("\n") != script.ReplaceLineEndings("\n"))
        {
            file.ReplaceNodes(new XCData(script));
            added = true;
        }

        return added;
    }

    // A file naming no architecture gets the fixed trio: Setup only reads the component matching the machine.
    internal static IEnumerable<XElement> Components(XElement root, string pass, string name)
    {
        var settings = root.Elements(Unattend + "settings")
            .FirstOrDefault(s => (string?)s.Attribute("pass") == pass);
        if (settings is null)
        {
            settings = new XElement(Unattend + "settings", new XAttribute("pass", pass));

            // The unattend schema puts settings before extension content, and both shipped XMLs
            // end with an Extensions block - a new settings element belongs with its siblings.
            var lastSettings = root.Elements(Unattend + "settings").LastOrDefault();
            if (lastSettings is not null)
            {
                lastSettings.AddAfterSelf(settings);
            }
            else
            {
                root.AddFirst(settings);
            }
        }

        var carried = root.Descendants(Unattend + "component")
            .Select(c => (string?)c.Attribute("processorArchitecture"))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (carried.Count == 0)
        {
            carried.AddRange(Architectures);
        }

        foreach (var architecture in carried)
        {
            var component = settings.Elements(Unattend + "component")
                .FirstOrDefault(c => (string?)c.Attribute("name") == name
                    && (string?)c.Attribute("processorArchitecture") == architecture);
            if (component is null)
            {
                component = new XElement(Unattend + "component",
                    new XAttribute("name", name),
                    new XAttribute("processorArchitecture", architecture),
                    new XAttribute("publicKeyToken", "31bf3856ad364e35"),
                    new XAttribute("language", "neutral"),
                    new XAttribute("versionScope", "nonSxS"));
                settings.Add(component);
            }

            yield return component;
        }
    }

    internal static void AddExtractCommandIfMissing(XElement runSynchronous, List<XElement> commands, ref int next)
    {
        if (!commands.Any(RunsExtractScript))
        {
            runSynchronous.Add(BuildCommand(next++, AutounattendScripts.ExtractDescription, AutounattendScripts.ExtractCommand));
        }
    }

    // Each removal closes the gap it leaves, as the driver step does for the orders its move vacates.
    internal static int RemoveCommands(XElement root, XName commandName, string description) =>
        RemoveCommands(root, commandName, c => (string?)c.Element(Unattend + "Description") == description);

    private static int RemoveCommands(XElement root, XName commandName, Func<XElement, bool> match)
    {
        var removed = 0;
        foreach (var command in root.Descendants(commandName).Where(match).ToList())
        {
            var emptied = command.Parent!;
            var siblings = emptied.Elements(commandName).Where(c => c != command).ToList();
            command.Remove();
            removed++;

            // Ensure creates the list, and for a missing architecture its component, so an emptied one goes with the step.
            while (emptied is { HasElements: false, Parent: { } parent })
            {
                emptied.Remove();
                emptied = parent;
            }

            if (!int.TryParse((string?)command.Element(Unattend + "Order"), out var vacated))
            {
                continue;
            }

            foreach (var order in siblings.Elements(Unattend + "Order"))
            {
                if (int.TryParse(order.Value, out var value) && value > vacated)
                {
                    order.SetValue(value - 1);
                }
            }
        }

        return removed;
    }

    // The extractor and its command stay while another step still carries a script through them.
    internal static bool RemoveScriptFile(XElement root, string scriptPath)
    {
        var extensions = root.Elements().Where(e => e.Name.LocalName == "Extensions").ToList();
        var scripts = extensions.Elements()
            .Where(e => e.Name.LocalName == "File" && (string?)e.Attribute("path") == scriptPath)
            .ToList();
        scripts.Remove();

        if (scripts.Count > 0 && !extensions.Elements().Any(e => e.Name.LocalName == "File"))
        {
            extensions.Elements().Where(e => e.Name.LocalName == "ExtractScript").Remove();
            RemoveCommands(root, Unattend + "RunSynchronousCommand", RunsExtractScript);
            extensions.Where(e => !e.HasElements).Remove();
        }

        return scripts.Count > 0;
    }

    private static bool RunsExtractScript(XElement command) =>
        PathOf(command).Contains(ExtractScriptReference, StringComparison.Ordinal);

    internal static string PathOf(XElement command) =>
        (string?)command.Element(Unattend + "Path") ?? string.Empty;

    internal static int NextOrder(IEnumerable<XElement> commands)
    {
        var max = 0;
        foreach (var order in commands.Elements(Unattend + "Order"))
        {
            if (int.TryParse(order.Value, out var value) && value > max)
            {
                max = value;
            }
        }

        return max + 1;
    }

    internal static XElement BuildCommand(int order, string description, string path) =>
        new(Unattend + "RunSynchronousCommand",
            new XAttribute(Wcm + "action", "add"),
            new XElement(Unattend + "Order", order),
            new XElement(Unattend + "Description", description),
            new XElement(Unattend + "Path", path));
}
