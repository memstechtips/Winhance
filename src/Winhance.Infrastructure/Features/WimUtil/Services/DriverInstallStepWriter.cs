using System.Xml.Linq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Autounattend;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class DriverInstallStepWriter(IFileSystemService files, ILogService log) : IDriverInstallStepWriter
{
    private static readonly XNamespace Unattend = AutounattendDocument.Unattend;

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
        if (!DriverStaging.IsStaged(files, workingDirectory))
        {
            return DriverInstallStepResult.NoDriversStaged;
        }

        var (doc, root, existed) = await AnswerFileSteps.LoadAsync(files, xmlPath, cancellationToken).ConfigureAwait(false);

        var added = AnswerFileSteps.EnsureScriptFile(root, ScriptPath, InstallScript);
        foreach (var component in AnswerFileSteps.Components(root, "specialize", "Microsoft-Windows-Deployment"))
        {
            added |= EnsureCommands(component);
        }

        if (!added)
        {
            return DriverInstallStepResult.AlreadyPresent;
        }

        await AnswerFileSteps.SaveAsync(files, xmlPath, doc, cancellationToken).ConfigureAwait(false);
        if (existed)
        {
            log.LogInformation($"Added the driver install step to {xmlPath}");
            return DriverInstallStepResult.Added;
        }

        log.LogInformation($"No autounattend.xml on the media; created a driver-install-only one at {xmlPath}");
        return DriverInstallStepResult.CreatedXml;
    }

    public async Task<bool> RemoveAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        var xmlPath = files.CombinePath(workingDirectory, "autounattend.xml");
        if (!files.FileExists(xmlPath))
        {
            return false;
        }

        var (doc, root, _) = await AnswerFileSteps.LoadAsync(files, xmlPath, cancellationToken).ConfigureAwait(false);
        var removedCommands = AnswerFileSteps.RemoveCommands(root, Unattend + "RunSynchronousCommand", Marker);
        var removedScript = AnswerFileSteps.RemoveScriptFile(root, ScriptPath);
        if (removedCommands == 0 && !removedScript)
        {
            return false;
        }

        await AnswerFileSteps.SaveAsync(files, xmlPath, doc, cancellationToken).ConfigureAwait(false);
        log.LogInformation($"Removed the step '{Marker}' from {xmlPath}");
        return true;
    }

    // An answer file that disables network adapters in specialize does it to keep OOBE offline
    // for the local-account flow, and only adapters that exist at that moment get disabled. A NIC
    // driver installed afterwards brings its adapter up enabled, so every such command moves
    // behind the install in its original order, and the orders they vacate close up.
    private static bool EnsureCommands(XElement component)
    {
        var runSynchronous = component.Child("RunSynchronous");
        var commands = runSynchronous.Elements(Unattend + "RunSynchronousCommand").ToList();
        if (commands.Any(c => (string?)c.Element(Unattend + "Description") == Marker))
        {
            return false;
        }

        var disables = commands.Where(c => AnswerFileSteps.PathOf(c).Contains("Disable-NetAdapter", StringComparison.Ordinal)).ToList();
        if (disables.Count > 0)
        {
            var vacated = new List<int>();
            foreach (var disable in disables)
            {
                disable.Remove();
                commands.Remove(disable);
                if (int.TryParse((string?)disable.Element(Unattend + "Order"), out var vacatedOrder))
                {
                    vacated.Add(vacatedOrder);
                }
            }

            foreach (var order in commands.Elements(Unattend + "Order"))
            {
                if (int.TryParse(order.Value, out var value))
                {
                    order.SetValue(value - vacated.Count(v => v < value));
                }
            }
        }

        var next = AnswerFileSteps.NextOrder(commands);
        AnswerFileSteps.AddExtractCommandIfMissing(runSynchronous, commands, ref next);
        runSynchronous.Add(AnswerFileSteps.BuildCommand(next++, Marker, InstallCommand));
        foreach (var disable in disables)
        {
            disable.SetElementValue(Unattend + "Order", next++);
            runSynchronous.Add(disable);
        }

        return true;
    }
}
