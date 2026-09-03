using System.Xml.Linq;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class DriverInstallStepWriter(IFileSystemService files, ILogService log) : IDriverInstallStepWriter
{
    private static readonly XNamespace Unattend = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace Wcm = "http://schemas.microsoft.com/WMIConfig/2002/State";
    private static readonly XNamespace WinhanceExtensions = "urn:winhance:unattend";
    private static readonly string[] Architectures = ["x86", "arm64", "amd64"];

    // The template's own extractor, verbatim: an answer file without one (hand-written, or none
    // at all) gets the same command and script the Winhance XML runs, so File elements land the
    // same way everywhere.
    private static readonly Lazy<(string Command, string Script)> Extractor = new(LoadExtractor);

    // The bytes always go out as UTF-8 without a BOM (what Windows Setup expects), so the
    // declaration must say utf-8 regardless of what the source file claimed - echoing a utf-16
    // or windows-1252 declaration over UTF-8 bytes makes Setup reject or mis-decode the file.
    private const string Declaration = @"<?xml version=""1.0"" encoding=""utf-8""?>";

    // Doubles as the idempotency marker: a RunSynchronousCommand carrying this Description is ours.
    internal const string Marker = "Install staged drivers and automatically clean up on success";

    // Same wording as the command UnattendedWinstall and the template already carry.
    internal const string ExtractDescription = "Loads Scripts in this XML File";

    internal const string ScriptPath = @"C:\ProgramData\Winhance\Unattend\Scripts\Winhance-DriverInstall.ps1";

    // Specialize pass: system context, first boot of the installed OS, a built-in restart before
    // OOBE - no logon needed, and SetupComplete.cmd's OEM-product-key disablement never applies.
    internal const string InstallCommand =
        @"powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File """ + ScriptPath + @"""";

    internal const string InstallScript =
        @"# Installs the driver packages Winhance staged in C:\Windows\Drivers, from the specialize pass of
# autounattend.xml. A package folder is removed once its driver is in; a failed one stays behind.
# pnputil exit codes: 0 installed, 3010 installed and reboot pending (Setup restarts anyway),
# 259 no matching device or a newer driver already present. Anything else is a failure.
# Always exits 0 so a driver problem never stops Windows Setup.
$log = 'C:\ProgramData\Winhance\Unattend\Logs\Winhance-DriverInstall.log'
$null = New-Item -Path (Split-Path $log) -ItemType Directory -Force
$drivers = 'C:\Windows\Drivers'
$infs = @(Get-ChildItem $drivers -Recurse -File -Force | Where-Object Extension -eq '.inf')
$packages = @($infs | ForEach-Object DirectoryName | Sort-Object -Unique)
""Winhance driver install started $(Get-Date) - $($packages.Count) driver package(s), $($infs.Count) INF(s)"" | Out-File $log

$failed = @()
foreach ($inf in $infs) {
    pnputil /add-driver $inf.FullName /install 2>&1 | Out-File $log -Append
    ""ExitCode $LASTEXITCODE for $($inf.Name)"" | Out-File $log -Append
    if ($LASTEXITCODE -notin 0, 3010, 259) { $failed += $inf.DirectoryName }
}

foreach ($dir in $packages + $drivers | Sort-Object -Unique -Descending) {
    if ($failed | Where-Object { ""$_\"".StartsWith(""$dir\"") }) { continue }
    Remove-Item $dir -Recurse -Force 2>&1 | Out-File $log -Append
}
""Done - $(@($failed | Sort-Object -Unique).Count) failed package(s) kept"" | Out-File $log -Append
exit 0
";

    public async Task<DriverInstallStepResult> EnsureAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        if (!files.DirectoryExists(files.CombinePath(workingDirectory, "sources", "$OEM$", "$$", "Drivers")))
            return DriverInstallStepResult.NoDriversStaged;

        var xmlPath = files.CombinePath(workingDirectory, "autounattend.xml");
        var existed = files.FileExists(xmlPath);
        var doc = existed
            ? XDocument.Parse(await files.ReadAllTextAsync(xmlPath, cancellationToken).ConfigureAwait(false))
            : new XDocument(new XElement(Unattend + "unattend"));
        var root = doc.Root ?? throw new InvalidOperationException("autounattend.xml has no root element");
        if (root.Attribute(XNamespace.Xmlns + "wcm") is null)
            root.Add(new XAttribute(XNamespace.Xmlns + "wcm", Wcm.NamespaceName));

        var added = EnsureScriptFile(root);
        foreach (var component in DeploymentComponents(root))
            added |= EnsureCommands(component);

        if (!added)
            return DriverInstallStepResult.AlreadyPresent;

        await files.WriteAllTextAsync(xmlPath, Declaration + Environment.NewLine + doc, cancellationToken).ConfigureAwait(false);
        if (existed)
        {
            log.LogInformation($"Added the driver install step to {xmlPath}");
            return DriverInstallStepResult.Added;
        }

        log.LogInformation($"No autounattend.xml on the media; created a driver-install-only one at {xmlPath}");
        return DriverInstallStepResult.CreatedXml;
    }

    // A foreign block keeps its own extractor: both known ones walk every File element whatever
    // the namespace, and both create our folder (the template's by name, Schneegans's by mkdir
    // on each file's parent), so the script lands the same way.
    private static bool EnsureScriptFile(XElement root)
    {
        var extensions = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Extensions");
        if (extensions is null)
        {
            extensions = new XElement(WinhanceExtensions + "Extensions");
            root.Add(extensions);
        }

        var ns = extensions.Name.Namespace;
        var added = false;
        if (extensions.Element(ns + "ExtractScript") is null)
        {
            extensions.AddFirst(new XElement(ns + "ExtractScript", Extractor.Value.Script));
            added = true;
        }

        // Refreshed in place so media reused across Winhance versions carries the current script.
        // XML parsing already turned the carried copy's line endings into LF.
        var file = extensions.Elements(ns + "File").FirstOrDefault(f => (string?)f.Attribute("path") == ScriptPath);
        if (file is null)
        {
            extensions.Add(new XElement(ns + "File", new XAttribute("path", ScriptPath), new XCData(InstallScript)));
            added = true;
        }
        else if (file.Value.ReplaceLineEndings("\n") != InstallScript.ReplaceLineEndings("\n"))
        {
            file.ReplaceNodes(new XCData(InstallScript));
            added = true;
        }

        return added;
    }

    // An answer file that disables network adapters in specialize does it to keep OOBE offline
    // for the local-account flow, and only adapters that exist at that moment get disabled. A NIC
    // driver installed afterwards brings its adapter up enabled, so every such command moves
    // behind the install in its original order, and the orders they vacate close up.
    private static bool EnsureCommands(XElement component)
    {
        var runSynchronous = component.Element(Unattend + "RunSynchronous");
        if (runSynchronous is null)
        {
            runSynchronous = new XElement(Unattend + "RunSynchronous");
            component.Add(runSynchronous);
        }

        var commands = runSynchronous.Elements(Unattend + "RunSynchronousCommand").ToList();
        if (commands.Any(c => (string?)c.Element(Unattend + "Description") == Marker))
            return false;

        var disables = commands.Where(c => PathOf(c).Contains("Disable-NetAdapter", StringComparison.Ordinal)).ToList();
        if (disables.Count > 0)
        {
            var vacated = new List<int>();
            foreach (var disable in disables)
            {
                disable.Remove();
                commands.Remove(disable);
                if (int.TryParse((string?)disable.Element(Unattend + "Order"), out var vacatedOrder))
                    vacated.Add(vacatedOrder);
            }

            foreach (var order in commands.Elements(Unattend + "Order"))
            {
                if (int.TryParse(order.Value, out var value))
                    order.SetValue(value - vacated.Count(v => v < value));
            }
        }

        var next = NextOrder(commands);
        if (!commands.Any(c => IsExtractCommand(PathOf(c))))
            runSynchronous.Add(BuildCommand(next++, ExtractDescription, Extractor.Value.Command));
        runSynchronous.Add(BuildCommand(next++, Marker, InstallCommand));
        foreach (var disable in disables)
        {
            disable.SetElementValue(Unattend + "Order", next++);
            runSynchronous.Add(disable);
        }

        return true;
    }

    // The template ships every pass with x86, arm64 and amd64 components; a user-supplied file
    // gets the same trio - Setup only reads the one matching the machine.
    private static IEnumerable<XElement> DeploymentComponents(XElement root)
    {
        var specialize = root.Elements(Unattend + "settings")
            .FirstOrDefault(s => (string?)s.Attribute("pass") == "specialize");
        if (specialize is null)
        {
            specialize = new XElement(Unattend + "settings", new XAttribute("pass", "specialize"));

            // The unattend schema puts settings before extension content, and both shipped XMLs
            // end with an Extensions block - a new settings element belongs with its siblings.
            var lastSettings = root.Elements(Unattend + "settings").LastOrDefault();
            if (lastSettings is not null)
                lastSettings.AddAfterSelf(specialize);
            else
                root.AddFirst(specialize);
        }

        foreach (var architecture in Architectures)
        {
            var component = specialize.Elements(Unattend + "component")
                .FirstOrDefault(c => (string?)c.Attribute("name") == "Microsoft-Windows-Deployment"
                    && (string?)c.Attribute("processorArchitecture") == architecture);
            if (component is null)
            {
                component = new XElement(Unattend + "component",
                    new XAttribute("name", "Microsoft-Windows-Deployment"),
                    new XAttribute("processorArchitecture", architecture),
                    new XAttribute("publicKeyToken", "31bf3856ad364e35"),
                    new XAttribute("language", "neutral"),
                    new XAttribute("versionScope", "nonSxS"));
                specialize.Add(component);
            }

            yield return component;
        }
    }

    private static (string Command, string Script) LoadExtractor()
    {
        var template = XDocument.Parse(AutounattendWriter.LoadTemplate());
        var script = template.Root!.Element(WinhanceExtensions + "Extensions")!.Element(WinhanceExtensions + "ExtractScript")!.Value;
        var command = template.Descendants(Unattend + "Path").Select(p => p.Value).First(IsExtractCommand);
        return (command, script);
    }

    private static bool IsExtractCommand(string path) =>
        path.Contains("Extensions.ExtractScript", StringComparison.Ordinal);

    private static string PathOf(XElement command) =>
        (string?)command.Element(Unattend + "Path") ?? string.Empty;

    private static int NextOrder(List<XElement> commands)
    {
        var max = 0;
        foreach (var order in commands.Elements(Unattend + "Order"))
        {
            if (int.TryParse(order.Value, out var value) && value > max)
                max = value;
        }

        return max + 1;
    }

    private static XElement BuildCommand(int order, string description, string path) =>
        new(Unattend + "RunSynchronousCommand",
            new XAttribute(Wcm + "action", "add"),
            new XElement(Unattend + "Order", order),
            new XElement(Unattend + "Description", description),
            new XElement(Unattend + "Path", path));
}
