using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Autounattend.Helpers;
using Winhance.Infrastructure.Features.Optimize.Services;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class OfflineSettingsApplier(
    IOfflineHiveLoader hives,
    IRegistryHiveNative hiveNative,
    IWindowsRegistryService registry,
    IOptionProviderRegistry optionProviders,
    IScheduledTaskStateService tasks,
    IPowerCfgApplier powerCfg,
    IPowerPlanActivationService activation,
    IWindowsThemeService theme,
    IDnsSettingsService dns,
    ISystemParametersService systemParameters,
    ILogService log) : IOfflineSettingsApplier
{
    public async Task<OfflineStepOutcome> ApplyAsync(
        string mountDir,
        SelectionSet set,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        await using var session = await hives.LoadAsync(mountDir, cancellationToken).ConfigureAwait(false);
        var offline = new OfflineRegistry(registry, session, hiveNative, log);
        var writer = new OfflineStateWriter(offline, tasks, powerCfg, activation, theme, dns, systemParameters, log);
        var build = BuildOf(offline);
        var catalog = SettingCatalog.All;
        var failed = new List<string>();
        var applied = 0;

        if (PickedPowerPlan(set, catalog) is { } plan)
        {
            writer.PickedPowerPlan = plan;
            if (!writer.ActivatePowerPlan(plan.ToString("D")))
            {
                log.LogInformation("The power plan could not be made active in the image; setup activates it");
            }
        }

        foreach (var choice in set.Settings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var setting = catalog.FirstOrDefault(s => s.Id == SettingIdAliases.Normalize(choice.SettingId));
            if (setting is null)
            {
                log.LogWarning(ApplyOpScriptEmitter.MissingSettingMessage(choice.SettingId));
                failed.Add(choice.SettingId);
                continue;
            }

            if (IsHandledElsewhere(setting, choice.Value))
            {
                continue;
            }

            var ops = ApplyOpScriptEmitter.OpsFor(setting, choice.Value, catalog, build, optionProviders);
            if (ops is null)
            {
                log.LogWarning(ApplyOpScriptEmitter.UnresolvedMessage(setting, choice.Value));
                failed.Add(setting.Id);
                continue;
            }

            // A write the image has no hive for is not a failure: the answer file's script applies every chosen
            // setting again during setup, so it lands there. Each op runs on its own so a refused one cannot hide
            // another op of the same setting that really failed.
            var realFailures = new List<string>();
            var refusedOps = 0;
            foreach (var op in ApplyPlan.From(ops).SyncOps)
            {
                var refusedBefore = offline.Refusals;
                var result = ApplyExecutor.Execute(new ApplyPlan([op], []), writer);
                if (result.AllSucceeded)
                {
                    continue;
                }

                if (offline.Refusals > refusedBefore)
                {
                    refusedOps++;
                }
                else
                {
                    realFailures.AddRange(result.Failures);
                }
            }

            if (realFailures.Count > 0)
            {
                log.LogWarning($"'{setting.Id}' did not fully apply to the image: {string.Join("; ", realFailures)}");
                failed.Add(setting.Id);
            }
            else if (refusedOps > 0)
            {
                log.LogInformation($"'{setting.Id}' writes outside the loaded hives; setup applies it");
            }
            else
            {
                applied++;
            }
        }

        return new OfflineStepOutcome(failed.Count == 0, failed, applied);
    }

    // The pick is written once, before the values, so each value knows which scheme it belongs to.
    private static Guid? PickedPowerPlan(SelectionSet set, IReadOnlyList<Setting> catalog)
    {
        foreach (var choice in set.Settings)
        {
            if (choice.Value is ChoiceValue.Keyed keyed
                && Guid.TryParse(keyed.Key, out var plan)
                && catalog.FirstOrDefault(s => s.Id == SettingIdAliases.Normalize(choice.SettingId))?.Options?.Source == OptionSource.PowerPlans)
            {
                return plan;
            }
        }

        return null;
    }

    // Answer-file settings have no registry write to make here. The power plan pick is applied up front for any value
    // of a PowerPlans setting, where the emitter only does that for a keyed GUID.
    private static bool IsHandledElsewhere(Setting setting, ChoiceValue value) =>
        setting.IsAnswerFileOnly
        || setting.Options?.Source == OptionSource.PowerPlans
        || ApplyOpScriptEmitter.IsUnselectedAction(setting, value);

    // Build-gated targets (the This PC folders differ by Windows version) resolve for the image, not for this PC.
    private static WinBuild BuildOf(OfflineRegistry offline)
    {
        const string CurrentVersion = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        if (!int.TryParse(offline.GetValue(CurrentVersion, "CurrentBuildNumber") as string, out var build))
        {
            throw new InvalidOperationException("The image does not say which Windows build it is");
        }

        return new WinBuild(build, offline.GetValue(CurrentVersion, "UBR") is int revision ? revision : 0);
    }
}
