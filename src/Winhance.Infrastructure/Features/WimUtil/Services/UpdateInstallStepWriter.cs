using System.Xml.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Autounattend;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class UpdateInstallStepWriter(IFileSystemService files, ILogService log) : IUpdateInstallStepWriter
{
    private static readonly XNamespace Unattend = AutounattendDocument.Unattend;
    private static readonly XNamespace Wcm = AutounattendDocument.Wcm;

    // Doubles as the idempotency marker: a SynchronousCommand carrying this Description is ours.
    internal const string Marker = "Install the staged Windows update and restart";

    internal const string ScriptPath = @"C:\ProgramData\Winhance\Unattend\Scripts\Winhance-UpdateInstall.ps1";

    // Never specialize: installing there hangs Setup's restart after specialize and the update rolls back (VM-tested).
    internal const string InstallCommand =
        @"powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File """ + ScriptPath + @"""";

    internal const string InstallScript =
        @"# Installs the Windows update Winhance staged in C:\ProgramData\Winhance\Unattend\Updates, from the first logon
# commands of autounattend.xml. The target .msu is the one named in the record beside the folder; the others
# are its checkpoints, which DISM finds in the same folder, so only the target is passed.
# DISM exit codes: 0 installed, 3010 installed and restart pending. Anything else is logged.
# The Updates folder is removed and the PC restarts either way: Winhance's user customizations wait
# while that folder exists and run after this restart, so it restarts even with no target. Always exits 0.
$logs = 'C:\ProgramData\Winhance\Unattend\Logs'
$log = ""$logs\Winhance-UpdateInstall.log""
$updates = '" + ScriptPaths.UnattendUpdatesDirectory + @"'
$record = '" + UpdateStaging.InstalledTargetRecord + @"'
$null = New-Item -Path $logs -ItemType Directory -Force
$name = ""$(Get-Content $record -Raw -ErrorAction SilentlyContinue)"".Trim()
$target = Get-ChildItem $updates -Filter *.msu -ErrorAction SilentlyContinue | Where-Object Name -eq $name
if (-not $target) {
    ""No staged update to install $(Get-Date)"" | Out-File $log
    Remove-Item $updates, $record -Recurse -Force -ErrorAction SilentlyContinue
    shutdown.exe /r /t 10
    exit 0
}
""Winhance update install started $(Get-Date) - $($target.Name)"" | Out-File $log

# The child hides its own console: Start-Process -WindowStyle Hidden would also hide the form, since Windows
# applies the caller's show state to a process's first window (VM-tested).
$window = Start-Process powershell.exe -PassThru -ArgumentList '-NoProfile', '-WindowStyle', 'Hidden', '-EncodedCommand', ([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes(@'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Winhance'
$form.TopMost = $true
$form.ControlBox = $false
$form.FormBorderStyle = 'FixedDialog'
$form.StartPosition = 'CenterScreen'
$form.ClientSize = New-Object System.Drawing.Size(480, 120)
$label = New-Object System.Windows.Forms.Label
$label.Text = 'Installing Windows updates. This takes about 10 minutes and your PC will restart once. Please don''t turn it off.'
$label.Font = New-Object System.Drawing.Font('Segoe UI', 11)
$label.TextAlign = 'MiddleCenter'
$label.Dock = 'Fill'
$form.Controls.Add($label)
$form.ShowDialog() | Out-Null
'@)))

dism.exe /Online /Add-Package /PackagePath:""$($target.FullName)"" /NoRestart /Quiet /LogPath:""$logs\Winhance-UpdateInstall-dism.log""
$exit = $LASTEXITCODE
""DISM exit code $exit"" | Out-File $log -Append
if ($exit -notin 0, 3010) { 'The update did not install; see the DISM log' | Out-File $log -Append }

Stop-Process -Id $window.Id -Force
Remove-Item $updates, $record -Recurse -Force 2>&1 | Out-File $log -Append
shutdown.exe /r /t 10
exit 0
";

    public Task<UpdateInstallStepResult> EnsureAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
        EnsureAsync(files.CombinePath(workingDirectory, "autounattend.xml"), workingDirectory, cancellationToken);

    public async Task<UpdateInstallStepResult> EnsureAsync(string xmlPath, string workingDirectory, CancellationToken cancellationToken = default)
    {
        if (!UpdateStaging.IsStaged(files, workingDirectory))
        {
            return UpdateInstallStepResult.NothingStaged;
        }

        var (doc, root, existed) = await AnswerFileSteps.LoadAsync(files, xmlPath, cancellationToken).ConfigureAwait(false);

        var added = AnswerFileSteps.EnsureScriptFile(root, ScriptPath, InstallScript);
        foreach (var component in AnswerFileSteps.Components(root, "specialize", "Microsoft-Windows-Deployment").ToList())
        {
            added |= EnsureExtractCommand(component);
        }
        foreach (var component in AnswerFileSteps.Components(root, "oobeSystem", "Microsoft-Windows-Shell-Setup").ToList())
        {
            added |= EnsureFirstLogonCommand(component);
        }

        if (!added)
        {
            return UpdateInstallStepResult.AlreadyPresent;
        }

        await AnswerFileSteps.SaveAsync(files, xmlPath, doc, cancellationToken).ConfigureAwait(false);
        if (existed)
        {
            log.LogInformation($"Added the update install step to {xmlPath}");
            return UpdateInstallStepResult.Added;
        }

        log.LogInformation($"No autounattend.xml on the media; created an update-install-only one at {xmlPath}");
        return UpdateInstallStepResult.CreatedXml;
    }

    public async Task<bool> RemoveAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        var xmlPath = files.CombinePath(workingDirectory, "autounattend.xml");
        if (!files.FileExists(xmlPath))
        {
            return false;
        }

        var (doc, root, _) = await AnswerFileSteps.LoadAsync(files, xmlPath, cancellationToken).ConfigureAwait(false);
        var removedCommands = AnswerFileSteps.RemoveCommands(root, Unattend + "SynchronousCommand", Marker);
        var removedScript = AnswerFileSteps.RemoveScriptFile(root, ScriptPath);
        if (removedCommands == 0 && !removedScript)
        {
            return false;
        }

        await AnswerFileSteps.SaveAsync(files, xmlPath, doc, cancellationToken).ConfigureAwait(false);
        log.LogInformation($"Removed the step '{Marker}' from {xmlPath}");
        return true;
    }

    private static bool EnsureExtractCommand(XElement component)
    {
        var runSynchronous = component.Child("RunSynchronous");
        var commands = runSynchronous.Elements(Unattend + "RunSynchronousCommand").ToList();
        var next = AnswerFileSteps.NextOrder(commands);
        AnswerFileSteps.AddExtractCommandIfMissing(runSynchronous, commands, ref next);
        return runSynchronous.Elements(Unattend + "RunSynchronousCommand").Count() != commands.Count;
    }

    private static bool EnsureFirstLogonCommand(XElement component)
    {
        var list = component.Child("FirstLogonCommands");
        var commands = list.Elements(Unattend + "SynchronousCommand").ToList();
        if (commands.Any(c => (string?)c.Element(Unattend + "Description") == Marker))
        {
            return false;
        }

        list.Add(new XElement(Unattend + "SynchronousCommand",
            new XAttribute(Wcm + "action", "add"),
            new XElement(Unattend + "Order", AnswerFileSteps.NextOrder(commands)),
            new XElement(Unattend + "Description", Marker),
            new XElement(Unattend + "CommandLine", InstallCommand)));
        return true;
    }
}
