using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

// Everything an answer file can be checked for before Windows Setup sees it. Schema validity is
// out of reach (Windows SIM, a GUI that needs the image's catalog, is the only validator), so the
// rules cover the structure every known generator emits and the scripts the file carries. The
// report gates nothing; it only says how confident the verdict is.
internal sealed class AnswerFileValidator(IFileSystemService files, IPowerShellRunner powerShell) : IAnswerFileValidator
{
    private static readonly XNamespace Unattend = "urn:schemas-microsoft-com:unattend";

    // Documented in the unattend reference: Path caps at 259, FirstLogonCommands CommandLine at
    // 1024, Order is an integer from 1 through 500. Nothing documents duplicate Orders, so that one
    // stays a warning. wcm:action is not checked: the docs never require it and the shipped
    // template omits it on FirstLogonCommands and works.
    private const int PathCap = 259;
    private const int CommandLineCap = 1024;
    private const int OrderMax = 500;

    // No progress task runs during the check, so there is no Cancel button; without this bound a
    // hung powershell.exe would hang the check forever. The runner kills the tree on cancellation.
    private const int ParseTimeoutSeconds = 30;

    private static readonly string[] Passes = ["windowsPE", "offlineServicing", "generalize", "specialize", "auditSystem", "auditUser", "oobeSystem"];
    private static readonly string[] Architectures = ["x86", "amd64", "arm64"];
    private static readonly string[] ComponentAttributes = ["name", "processorArchitecture", "publicKeyToken", "language", "versionScope"];
    private static readonly string[] RegistryRoots = ["HKLM", "HKCU", "HKCR", "HKU", "HKCC", "HKEY_LOCAL_MACHINE", "HKEY_CURRENT_USER", "HKEY_CLASSES_ROOT", "HKEY_USERS", "HKEY_CURRENT_CONFIG"];

    // Folders only an extractor creates: a -File target under one must be carried by the same XML.
    private static readonly string[] ExtractorFolders = [@"C:\ProgramData\Winhance\Unattend\Scripts\", @"C:\Windows\Setup\Scripts\"];

    private static readonly (string Owner, string[] Passes)[] DeploymentAndSetup =
    [
        ("Microsoft-Windows-Deployment", new[] { "specialize", "auditUser" }),
        ("Microsoft-Windows-Setup", new[] { "windowsPE" }),
    ];

    private static readonly (string Owner, string[] Passes)[] ShellSetup =
    [
        ("Microsoft-Windows-Shell-Setup", new[] { "oobeSystem" }),
    ];

    private static readonly Dictionary<string, CommandList> CommandLists = new(StringComparer.Ordinal)
    {
        ["RunSynchronous"] = new("RunSynchronousCommand", "Path", PathCap, DeploymentAndSetup),
        ["RunAsynchronous"] = new("RunAsynchronousCommand", "Path", PathCap, DeploymentAndSetup),
        ["FirstLogonCommands"] = new("SynchronousCommand", "CommandLine", CommandLineCap, ShellSetup),
        ["LogonCommands"] = new("AsynchronousCommand", "CommandLine", 0, ShellSetup),
    };

    // Setup's own reader accepts code-page declarations such as windows-1252; .NET only knows
    // them once the provider is registered.
    static AnswerFileValidator() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static readonly Regex CmdWrapper = new(
        @"^""?(?:[^""\s]*[\\/])?cmd(?:\.exe)?""?\s",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FileArgument = new(
        @"-File\s+(?:""(?<path>[^""]+)""|(?<path>\S+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RegistryCommand = new(
        @"^reg(?:\.exe)?\s+(?:add|delete)\s+""?(?<key>[^""\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RegValueLine = new(
        @"^(?:@|""(?:[^""\\]|\\.)*"")=(?:-|""(?:[^""\\]|\\.)*""|dword:[0-9A-Fa-f]{1,8}|qword:[0-9A-Fa-f]{1,16}|hex(?:\([0-9A-Fa-f]+\))?:.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Cap 0 means no documented limit.
    private sealed record CommandList(string ItemName, string TextName, int Cap, (string Owner, string[] Passes)[] Homes);

    private sealed class Findings
    {
        public List<AnswerFileFinding> Items { get; } = [];

        public void Error(AnswerFileRule rule, string location, string detail) =>
            Items.Add(new AnswerFileFinding(rule, AnswerFileSeverity.Error, location, detail));

        public void Warning(AnswerFileRule rule, string location, string detail) =>
            Items.Add(new AnswerFileFinding(rule, AnswerFileSeverity.Warning, location, detail));
    }

    public async Task<AnswerFileReport> ValidateAsync(string xmlPath, CancellationToken cancellationToken = default)
    {
        var findings = new Findings();

        byte[] bytes;
        try
        {
            bytes = await files.ReadAllBytesAsync(xmlPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            findings.Error(AnswerFileRule.FileUnreadable, xmlPath, ex.Message);
            return new AnswerFileReport(findings.Items);
        }

        XDocument doc;
        try
        {
            doc = Parse(bytes);
        }
        catch (XmlException ex)
        {
            findings.Error(AnswerFileRule.NotWellFormed, "line " + ex.LineNumber.ToString(CultureInfo.InvariantCulture), ex.Message);
            return new AnswerFileReport(findings.Items);
        }

        var root = doc.Root!;
        if (root.Name != Unattend + "unattend")
        {
            findings.Error(AnswerFileRule.WrongRoot, Location(root), root.Name.ToString());
            return new AnswerFileReport(findings.Items);
        }

        var scripts = new Dictionary<string, string>(StringComparer.Ordinal);
        var carriedPaths = CarriedFiles(root).Select(f => (string?)f.Attribute("path") ?? string.Empty).ToList();
        CheckSettings(root, findings, scripts, carriedPaths);
        CheckExtensions(root, findings, scripts);

        if (scripts.Count > 0)
        {
            using var parseWindow = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            parseWindow.CancelAfter(TimeSpan.FromSeconds(ParseTimeoutSeconds));
            try
            {
                var errors = await powerShell.FindParseErrorsAsync(scripts, parseWindow.Token).ConfigureAwait(false);
                foreach (var (location, message) in errors)
                    findings.Error(AnswerFileRule.PowerShellParse, location, message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var detail = ex is OperationCanceledException
                    ? "powershell.exe did not finish within " + ParseTimeoutSeconds.ToString(CultureInfo.InvariantCulture) + " seconds"
                    : ex.Message;
                findings.Warning(AnswerFileRule.ParserUnavailable, xmlPath, detail);
            }
        }

        return new AnswerFileReport(findings.Items);
    }

    // Parsed from the raw bytes on purpose: a declaration that disagrees with the bytes (utf-16
    // over UTF-8, or the reverse) fails here, exactly as it would for Setup's own reader.
    private static XDocument Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    private static void CheckSettings(XElement root, Findings findings, Dictionary<string, string> scripts, List<string> carriedPaths)
    {
        foreach (var settings in root.Elements(Unattend + "settings"))
        {
            var pass = (string?)settings.Attribute("pass") ?? string.Empty;
            if (!Passes.Contains(pass, StringComparer.Ordinal))
                findings.Error(AnswerFileRule.UnknownPass, Location(settings), pass);

            foreach (var component in settings.Elements(Unattend + "component"))
            {
                var missing = ComponentAttributes.Where(a => string.IsNullOrEmpty((string?)component.Attribute(a))).ToList();
                var architecture = (string?)component.Attribute("processorArchitecture") ?? string.Empty;
                if (missing.Count > 0)
                    findings.Error(AnswerFileRule.ComponentAttributes, Location(component), "missing " + string.Join(", ", missing));
                else if (!Architectures.Contains(architecture, StringComparer.Ordinal))
                    findings.Error(AnswerFileRule.ComponentAttributes, Location(component), "processorArchitecture " + architecture);

                var name = (string?)component.Attribute("name") ?? string.Empty;
                foreach (var list in component.Elements().Where(e => e.Name.Namespace == Unattend && CommandLists.ContainsKey(e.Name.LocalName)))
                {
                    var shape = CommandLists[list.Name.LocalName];
                    if (!shape.Homes.Any(h => h.Owner == name && h.Passes.Contains(pass, StringComparer.Ordinal)))
                    {
                        var homes = string.Join("; ", shape.Homes.Select(h => h.Owner + " in " + string.Join("/", h.Passes)));
                        findings.Error(AnswerFileRule.CommandListPlacement, Location(list), list.Name.LocalName + " belongs to " + homes);
                    }

                    CheckCommandList(list, shape, findings, scripts, carriedPaths);
                }
            }
        }
    }

    private static void CheckCommandList(XElement list, CommandList shape, Findings findings, Dictionary<string, string> scripts, List<string> carriedPaths)
    {
        var seenOrders = new HashSet<int>();
        foreach (var command in list.Elements(Unattend + shape.ItemName))
        {
            var location = Location(command);
            var orderText = (string?)command.Element(Unattend + "Order") ?? string.Empty;
            if (!int.TryParse(orderText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var order) || order < 1 || order > OrderMax)
                findings.Error(AnswerFileRule.OrderInvalid, location, orderText.Length == 0 ? "(none)" : orderText);
            else if (!seenOrders.Add(order))
                findings.Warning(AnswerFileRule.OrderDuplicate, location, orderText);

            var text = (string?)command.Element(Unattend + shape.TextName) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                findings.Error(AnswerFileRule.CommandEmpty, location, shape.TextName);
                continue;
            }

            if (shape.Cap > 0 && text.Length > shape.Cap)
                findings.Error(AnswerFileRule.CommandTooLong, location, text.Length.ToString(CultureInfo.InvariantCulture) + " characters, limit " + shape.Cap.ToString(CultureInfo.InvariantCulture));

            CheckCommandText(text, location, findings, scripts, carriedPaths);
        }
    }

    // Setup hands the text to CreateProcess, so a bare powershell.exe payload runs to the LAST
    // quote and any quote inside it is eaten by argument splitting. Inside a cmd.exe /c wrapper
    // the payload ends at the NEXT quote and the rest of the line belongs to cmd (redirections).
    private static void CheckCommandText(string text, string location, Findings findings, Dictionary<string, string> scripts, List<string> carriedPaths)
    {
        var command = text.TrimStart();
        var wrappedByCmd = CmdWrapper.IsMatch(command);

        const string inline = "-Command \"";
        var inlineIndex = command.IndexOf(inline, StringComparison.OrdinalIgnoreCase);
        if (inlineIndex >= 0)
        {
            var start = inlineIndex + inline.Length;
            var end = wrappedByCmd ? command.IndexOf('"', start) : command.LastIndexOf('"');
            if (end < start)
            {
                findings.Error(AnswerFileRule.InlineQuote, location, "the -Command payload never closes its quote");
            }
            else
            {
                // A backslash-escaped quote survives argument splitting, so only bare quotes count.
                var payload = command[start..end];
                if (!wrappedByCmd && payload.Replace("\\\"", string.Empty, StringComparison.Ordinal).Contains('"'))
                    findings.Error(AnswerFileRule.InlineQuote, location, payload);
                else
                    AddScript(scripts, location, payload);
            }
        }

        var fileArgument = FileArgument.Match(command);
        if (fileArgument.Success)
        {
            var path = fileArgument.Groups["path"].Value;
            if (!carriedPaths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
            {
                if (ExtractorFolders.Any(f => path.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
                    findings.Error(AnswerFileRule.ScriptNotCarried, location, path);
                else
                    findings.Warning(AnswerFileRule.ScriptPathUnknown, location, path);
            }
        }

        var registry = RegistryCommand.Match(command);
        if (registry.Success)
        {
            var key = registry.Groups["key"].Value;
            if (!RegistryRoots.Contains(key.Split('\\', 2)[0], StringComparer.OrdinalIgnoreCase))
                findings.Error(AnswerFileRule.RegistryRoot, location, key);
        }
    }

    private static IEnumerable<XElement> CarriedFiles(XElement root) =>
        root.Elements().Where(e => e.Name.LocalName == "Extensions")
            .SelectMany(e => e.Elements().Where(f => f.Name.LocalName == "File"));

    // What the extractor writes is InnerText.Trim() with the XML parser's LF line endings, so the
    // carried content is checked in that form.
    private static void CheckExtensions(XElement root, Findings findings, Dictionary<string, string> scripts)
    {
        var extensions = root.Elements().Where(e => e.Name.LocalName == "Extensions").ToList();
        var carried = CarriedFiles(root).ToList();
        if (carried.Count == 0)
            return;

        var extractScript = extensions.SelectMany(e => e.Elements().Where(s => s.Name.LocalName == "ExtractScript")).FirstOrDefault();
        var extractorRuns = root.Descendants()
            .Any(e => (e.Name == Unattend + "Path" || e.Name == Unattend + "CommandLine")
                && e.Value.Contains("Extensions.ExtractScript", StringComparison.Ordinal));
        if (extractScript is null)
            findings.Error(AnswerFileRule.ExtractorMissing, Location(extensions[0]), "no ExtractScript element");
        else if (!extractorRuns)
            findings.Error(AnswerFileRule.ExtractorMissing, Location(extensions[0]), "no command runs Extensions.ExtractScript");
        else
            AddScript(scripts, Location(extractScript), extractScript.Value);

        foreach (var file in carried)
        {
            var path = (string?)file.Attribute("path") ?? string.Empty;
            var location = Location(file);
            if (!IsAbsolute(path))
                findings.Error(AnswerFileRule.FilePathNotAbsolute, location, path);

            var content = file.Value.ReplaceLineEndings("\n").Trim();
            if (content.Length == 0)
            {
                findings.Error(AnswerFileRule.FileEmpty, location, path);
                continue;
            }

            switch (Extension(path))
            {
                case ".ps1":
                    AddScript(scripts, location, content);
                    break;
                case ".xml":
                    CheckCarriedXml(content, location, findings);
                    break;
                case ".reg":
                    CheckRegFile(content, location, findings);
                    break;
                case ".cmd" or ".bat":
                    var lossy = content.Split('\n').Select((line, i) => (line, i)).FirstOrDefault(l => l.line.Any(c => c > 127));
                    if (lossy.line is not null)
                        findings.Warning(AnswerFileRule.AnsiLossy, location, "line " + (lossy.i + 1).ToString(CultureInfo.InvariantCulture) + ": " + lossy.line);
                    break;
                case ".vbs":
                    findings.Warning(AnswerFileRule.VbScriptDeprecated, location, path);
                    break;
                case ".js":
                    break;
                default:
                    findings.Warning(AnswerFileRule.UnknownFileType, location, path);
                    break;
            }
        }
    }

    private static void CheckCarriedXml(string content, string location, Findings findings)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            while (reader.Read())
            {
            }
        }
        catch (XmlException ex)
        {
            findings.Error(AnswerFileRule.XmlFileNotWellFormed, location, ex.Message);
        }
    }

    // The 5.00 format regedit imports: the header line, blank line, [KEY] sections under a known
    // root, "name"=value lines, hex byte lists continued with a trailing backslash, ; comments.
    // Anything else is reported with its line; the grammar is deliberately no stricter than that.
    private static void CheckRegFile(string content, string location, Findings findings)
    {
        var lines = content.Split('\n');
        var header = lines[0].Trim();
        if (header != "Windows Registry Editor Version 5.00" && header != "REGEDIT4")
        {
            findings.Error(AnswerFileRule.RegFileSyntax, location, "line 1: " + header);
            return;
        }

        var continued = false;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (continued)
            {
                continued = line.EndsWith('\\');
                continue;
            }

            if (line.Length == 0 || line.StartsWith(';'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var key = line[1..^1].TrimStart('-');
                if (!RegistryRoots.Contains(key.Split('\\', 2)[0], StringComparer.OrdinalIgnoreCase))
                    findings.Error(AnswerFileRule.RegFileSyntax, location, "line " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + line);
                continue;
            }

            if (RegValueLine.IsMatch(line))
            {
                continued = line.EndsWith('\\');
                continue;
            }

            findings.Error(AnswerFileRule.RegFileSyntax, location, "line " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + line);
        }
    }

    // Two elements on one line with the same Order or path would share a location; every script
    // must still reach the parser.
    private static void AddScript(Dictionary<string, string> scripts, string location, string content)
    {
        var key = location;
        for (var n = 2; scripts.ContainsKey(key); n++)
            key = location + " #" + n.ToString(CultureInfo.InvariantCulture);
        scripts[key] = content;
    }

    private static bool IsAbsolute(string path) =>
        path.StartsWith('%') || (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\');

    private static string Extension(string path)
    {
        var name = path[(path.LastIndexOf('\\') + 1)..];
        var dot = name.LastIndexOf('.');
        return dot < 0 ? string.Empty : name[dot..].ToLowerInvariant();
    }

    private static string Location(XElement element)
    {
        var parts = new List<string>();
        for (XElement? e = element; e is not null && e.Parent is not null; e = e.Parent)
            parts.Add(Describe(e));
        parts.Reverse();

        var path = parts.Count == 0 ? element.Name.LocalName : string.Join(" / ", parts);
        return element is IXmlLineInfo info && info.HasLineInfo()
            ? "line " + info.LineNumber.ToString(CultureInfo.InvariantCulture) + ": " + path
            : path;
    }

    private static string Describe(XElement e) => e.Name.LocalName switch
    {
        "settings" => "settings[" + (string?)e.Attribute("pass") + "]",
        "component" => "component[" + (string?)e.Attribute("name") + " " + (string?)e.Attribute("processorArchitecture") + "]",
        "RunSynchronousCommand" or "RunAsynchronousCommand" or "SynchronousCommand" or "AsynchronousCommand" =>
            e.Name.LocalName + "[Order " + (string?)e.Element(Unattend + "Order") + "]",
        "File" => "File[" + (string?)e.Attribute("path") + "]",
        _ => e.Name.LocalName,
    };
}
