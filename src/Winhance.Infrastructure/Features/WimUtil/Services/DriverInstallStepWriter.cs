using System.Xml.Linq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Autounattend;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class DriverInstallStepWriter(IFileSystemService files, ILogService log) : IDriverInstallStepWriter
{
    private static readonly XNamespace Unattend = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace Wcm = "http://schemas.microsoft.com/WMIConfig/2002/State";
    private static readonly XNamespace WinhanceExtensions = "urn:winhance:unattend";
    private static readonly string[] Architectures = ["x86", "arm64", "amd64"];

    // The bytes always go out as UTF-8 without a BOM (what Windows Setup expects), so the
    // declaration must say utf-8 regardless of what the source file claimed - echoing a utf-16
    // or windows-1252 declaration over UTF-8 bytes makes Setup reject or mis-decode the file.
    private const string Declaration = @"<?xml version=""1.0"" encoding=""utf-8""?>";

    // Doubles as the idempotency marker: a RunSynchronousCommand carrying this Description is ours.
    internal const string Marker = "Install staged drivers and automatically clean up on success";

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

    public Task<DriverInstallStepResult> EnsureAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
        EnsureAsync(files.CombinePath(workingDirectory, "autounattend.xml"), workingDirectory, cancellationToken);

    public async Task<DriverInstallStepResult> EnsureAsync(string xmlPath, string workingDirectory, CancellationToken cancellationToken = default)
    {
        if (!files.DirectoryExists(files.CombinePath(workingDirectory, "sources", "$OEM$", "$$", "Drivers")))
            return DriverInstallStepResult.NoDriversStaged;

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
    // the namespace, and both create our folder (Winhance's by name, the other by mkdir
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
            extensions.AddFirst(new XElement(ns + "ExtractScript", AutounattendScripts.ExtractScript));
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
            runSynchronous.Add(BuildCommand(next++, AutounattendScripts.ExtractDescription, AutounattendScripts.ExtractCommand));
        runSynchronous.Add(BuildCommand(next++, Marker, InstallCommand));
        foreach (var disable in disables)
        {
            disable.SetElementValue(Unattend + "Order", next++);
            runSynchronous.Add(disable);
        }

        return true;
    }

    // A file naming no architecture gets the fixed trio: Setup only reads the component matching the machine.
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

        var carried = root.Descendants(Unattend + "component")
            .Select(c => (string?)c.Attribute("processorArchitecture"))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (carried.Count == 0)
            carried.AddRange(Architectures);

        foreach (var architecture in carried)
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
