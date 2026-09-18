using System.Text;
using System.Xml;
using System.Xml.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Infrastructure.Features.Autounattend.Writers;

namespace Winhance.Infrastructure.Features.Autounattend;

// Build's order is the RunSynchronous order in the file: the extractor first so the carried scripts exist before
// anything runs them, the Winhancements script after every card's commands, the architecture clone last so every
// component gets its copies.
internal static class AutounattendDocumentBuilder
{
    private static readonly ProductKeyWriter ProductKey = new();
    private static readonly AccountWriter Account = new();
    private static readonly ArchitectureWriter Architecture = new();
    private static readonly TimeRegionLanguageWriter TimeRegionLanguage = new();

    internal static IReadOnlyList<IAutounattendElementWriter> ShapeWriters { get; } = [ProductKey, Account, Architecture];

    internal static AutounattendElementWriter Elements { get; } =
        new(ShapeWriters.SelectMany(writer => writer.Handles).ToHashSet(StringComparer.Ordinal));

    public static XDocument Build(AutounattendRenderContext context)
    {
        var doc = AutounattendDocument.NewSkeleton();
        ProductKey.Write(doc, context);
        WriteScriptExtractor(doc);
        Account.Write(doc, context);
        Elements.Write(doc, context);
        WriteWinhancementsScript(doc, context);
        WriteEmbeddedPictures(doc, context);
        TimeRegionLanguage.Write(doc, context);
        Architecture.Write(doc, context);
        doc.Extensions().AddFirst(new XElement(AutounattendDocument.WinhanceExtensions + "Generator", new XAttribute("version", context.AppVersion)));
        return doc;
    }

    private static void WriteScriptExtractor(XDocument doc)
    {
        doc.Pass("specialize").Component("Microsoft-Windows-Deployment")
            .AddRunSynchronousCommand(AutounattendScripts.ExtractCommand, AutounattendScripts.ExtractDescription);
        doc.Extensions().Add(new XElement(AutounattendDocument.WinhanceExtensions + "ExtractScript", AutounattendScripts.ExtractScript));
    }

    private static void WriteWinhancementsScript(XDocument doc, AutounattendRenderContext context)
    {
        doc.Pass("specialize").Component("Microsoft-Windows-Deployment").AddRunSynchronousCommand(
            $"powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{ScriptPaths.AutounattendScriptPath}\"",
            "Runs Winhance Script System Wide Operations like App Uninstalls, HKLM Registry entries etc.");
        doc.Extensions().Add(new XElement(AutounattendDocument.WinhanceExtensions + "File",
            new XAttribute("path", ScriptPaths.AutounattendScriptPath),
            new XCData(context.WinhancementsScript)));
    }

    private static void WriteEmbeddedPictures(XDocument doc, AutounattendRenderContext context)
    {
        foreach (var (destination, base64) in context.EmbeddedPictures)
        {
            doc.Extensions().Add(new XElement(AutounattendDocument.WinhanceExtensions + "File",
                new XAttribute("path", destination),
                new XAttribute("encoding", "base64"),
                base64));
        }
    }

    // Windows Setup wants UTF-8 without a BOM and a declaration that says so; a StringWriter would declare utf-16.
    public static string Serialize(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\r\n",
            Encoding = new UTF8Encoding(false),
        };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
            doc.Save(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
