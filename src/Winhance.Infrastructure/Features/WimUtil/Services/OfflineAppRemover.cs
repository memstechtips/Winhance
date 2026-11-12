using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class OfflineAppRemover(
    IDismProcessRunner dism,
    IFileSystemService files,
    IPowerShellRunner powerShell,
    IOfflineHiveLoader hives,
    ILogService log) : IOfflineAppRemover
{
    public async Task<OfflineStepOutcome> RemoveAsync(
        string mountDir,
        SelectionSet set,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        mountDir = mountDir.TrimEnd('\\');
        var plan = AppRemovalPlan.For(set.WindowsApps);
        var tally = new Tally();

        // No script can run inside an image, so a dedicated app's own packages are removed here as well.
        await RemoveProvisionedAsync(mountDir, [.. plan.Packages, .. plan.DedicatedPackages], tally, progress, cancellationToken).ConfigureAwait(false);
        await RemoveCapabilitiesAsync(mountDir, plan.Capabilities, tally, progress, cancellationToken).ConfigureAwait(false);
        await DisableFeaturesAsync(mountDir, plan.Features, tally, progress, cancellationToken).ConfigureAwait(false);

        var gameBar = GameBarRemoval.Includes(plan.Packages);
        if (plan.Dedicated.Count > 0 || gameBar)
        {
            await CleanImageAsync(mountDir, plan, gameBar, tally, cancellationToken).ConfigureAwait(false);
        }

        if (tally.Failed.Count > 0)
        {
            return new OfflineStepOutcome(false, tally.Failed, tally.Applied);
        }

        // The answer file reads this to know the continuous removal is not needed for these apps.
        var marker = files.CombinePath(mountDir, ScriptPaths.UnattendAppsRemovedMarker[3..]);
        files.CreateDirectory(files.GetDirectoryName(marker)!);
        files.WriteAllText(marker, string.Empty);
        return new OfflineStepOutcome(true, [], tally.Applied);
    }

    private async Task RemoveProvisionedAsync(
        string mountDir,
        IReadOnlyList<string> packages,
        Tally tally,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken ct)
    {
        if (packages.Count == 0)
        {
            return;
        }

        var (exit, output) = await dism.RunProcessWithProgressAsync("dism.exe", $"/Image:\"{mountDir}\" /English /Get-ProvisionedAppxPackages", progress, ct).ConfigureAwait(false);
        if (!InstallerExitCode.IsSuccess(exit))
        {
            log.LogWarning($"Could not list the provisioned packages (exit {exit})");
            tally.Failed.AddRange(packages);
            return;
        }

        var present = DismListParser.ValuesIn(output);
        foreach (var name in packages.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var full in present.Where(p => DismListParser.IsPackageOf(p, name)))
            {
                if (!await RunAsync($"/Image:\"{mountDir}\" /Remove-ProvisionedAppxPackage /PackageName:{full}", progress, ct).ConfigureAwait(false))
                {
                    tally.Failed.Add(full);
                }
                else
                {
                    tally.Applied++;
                }
            }
        }
    }

    private async Task RemoveCapabilitiesAsync(
        string mountDir,
        IReadOnlyList<string> capabilities,
        Tally tally,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken ct)
    {
        if (capabilities.Count == 0)
        {
            return;
        }

        var (exit, output) = await dism.RunProcessWithProgressAsync("dism.exe", $"/Image:\"{mountDir}\" /English /Get-Capabilities", progress, ct).ConfigureAwait(false);
        if (!InstallerExitCode.IsSuccess(exit))
        {
            log.LogWarning($"Could not list the capabilities (exit {exit})");
            tally.Failed.AddRange(capabilities);
            return;
        }

        var installed = DismListParser.InstalledCapabilities(output);
        foreach (var capability in capabilities.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var full in installed.Where(i => i.StartsWith(capability, StringComparison.OrdinalIgnoreCase)))
            {
                if (!await RunAsync($"/Image:\"{mountDir}\" /Remove-Capability /CapabilityName:{full}", progress, ct).ConfigureAwait(false))
                {
                    tally.Failed.Add(full);
                }
                else
                {
                    tally.Applied++;
                }
            }
        }
    }

    // An edition that never shipped a feature fails /Disable-Feature, so only features the image has enabled are
    // disabled; the rest are already in the state the user asked for.
    private async Task DisableFeaturesAsync(
        string mountDir,
        IReadOnlyList<string> features,
        Tally tally,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken ct)
    {
        if (features.Count == 0)
        {
            return;
        }

        var (exit, output) = await dism.RunProcessWithProgressAsync("dism.exe", $"/Image:\"{mountDir}\" /English /Get-Features", progress, ct).ConfigureAwait(false);
        if (!InstallerExitCode.IsSuccess(exit))
        {
            log.LogWarning($"Could not list the optional features (exit {exit})");
            tally.Failed.AddRange(features);
            return;
        }

        var enabled = DismListParser.EnabledFeatures(output);
        foreach (var feature in features.Distinct(StringComparer.OrdinalIgnoreCase).Where(enabled.Contains))
        {
            if (!await RunAsync($"/Image:\"{mountDir}\" /Disable-Feature /FeatureName:{feature}", progress, ct).ConfigureAwait(false))
            {
                tally.Failed.Add(feature);
            }
            else
            {
                tally.Applied++;
            }
        }
    }

    private async Task<bool> RunAsync(string arguments, IProgress<TaskProgressDetail>? progress, CancellationToken ct)
    {
        log.LogInformation($"dism.exe {arguments}");
        var (exit, output) = await dism.RunProcessWithProgressAsync("dism.exe", arguments, progress, ct).ConfigureAwait(false);
        if (!InstallerExitCode.IsSuccess(exit))
        {
            log.LogWarning($"dism.exe exited with {exit}: {output[^Math.Min(output.Length, 500)..]}");
        }
        return InstallerExitCode.IsSuccess(exit);
    }

    // The scripts are the ones the live run uses; -Image points them at the mounted image and the loaded hives.
    private async Task CleanImageAsync(string mountDir, AppRemovalPlan plan, bool gameBar, Tally tally, CancellationToken ct)
    {
        var edge = plan.Dedicated.FirstOrDefault(s => s.RemovalScriptName == EdgeRemovalScript.ScriptName);
        var oneDrive = plan.Dedicated.FirstOrDefault(s => s.RemovalScriptName == OneDriveRemovalScript.ScriptName);

        // A script this class has no offline form for stays with the answer file.
        tally.Failed.AddRange(plan.Dedicated
            .Where(s => s.RemovalScriptName is not (EdgeRemovalScript.ScriptName or OneDriveRemovalScript.ScriptName))
            .Select(s => s.Name));

        IOfflineHiveSession session;
        try
        {
            session = await hives.LoadAsync(mountDir, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError($"Could not load the image registry: {ex.Message}", ex);
            tally.Failed.Add("registry");
            return;
        }

        var folder = files.CombinePath(files.GetTempPath(), "Winhance_Image_" + Guid.NewGuid().ToString("N"));
        var image = $"-Image \"{mountDir}\"";
        var software = $"-SoftwareRoot \"{session.SoftwareRoot}\"";
        var user = $"-UserRoot \"{session.DefaultUserRoot}\"";

        await using (session.ConfigureAwait(false))
        {
            try
            {
                files.CreateDirectory(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.LogError($"Could not create {folder}: {ex.Message}", ex);
                tally.Failed.AddRange(plan.Dedicated.Select(s => s.Name).Where(n => !tally.Failed.Contains(n)));
                if (gameBar)
                {
                    tally.Failed.Add("Game Bar");
                }
                return;
            }

            try
            {
                if (edge is not null)
                {
                    await RunScriptAsync(folder, edge.Name, edge.RemovalScript!(), $"{image} {software} -SystemRoot \"{session.SystemRoot}\\ControlSet001\" {user}", tally, ct).ConfigureAwait(false);
                }
                if (oneDrive is not null)
                {
                    await RunScriptAsync(folder, "OneDrive", oneDrive.RemovalScript!(), $"{image} {software} {user}", tally, ct).ConfigureAwait(false);
                }
                if (gameBar)
                {
                    var xbox = plan.Packages.Where(p => GameBarRemoval.Packages.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList();
                    var script = BloatRemovalScriptGenerator.GenerateScript(xbox, [], [], [], includeXboxRegistryFix: true);
                    await RunScriptAsync(folder, "Game Bar", script, $"{image} {software}", tally, ct).ConfigureAwait(false);
                }
            }
            finally
            {
                try
                {
                    files.DeleteDirectory(folder, true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log.LogWarning($"Could not delete {folder}: {ex.Message}");
                }
            }
        }
    }

    private async Task RunScriptAsync(string folder, string name, string script, string arguments, Tally tally, CancellationToken ct)
    {
        var path = files.CombinePath(folder, Guid.NewGuid().ToString("N") + ".ps1");
        try
        {
            await files.WriteAllTextAsync(path, script, ct).ConfigureAwait(false);
            var output = await powerShell.RunScriptFileAsync(path, arguments, null, ct).ConfigureAwait(false);
            log.LogInformation($"{name} removal in the image: {output}");
            tally.Applied++;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError($"{name} removal in the image failed: {ex.Message}", ex);
            tally.Failed.Add(name);
        }
    }

    private sealed class Tally
    {
        public List<string> Failed { get; } = [];

        public int Applied { get; set; }
    }
}
