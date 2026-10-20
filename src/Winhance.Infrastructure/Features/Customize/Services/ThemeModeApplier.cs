using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Customize.Services;

internal sealed class ThemeModeApplier(
    IStateWriter stateWriter,
    ILogService logService) : ISpecialSettingHandler
{
    public Task<bool> TryApplySpecialSettingAsync(
        string settingId,
        object value,
        bool additionalContext = false,
        ISettingApplicationService? settingApplicationService = null)
    {
        if (settingId != "theme-mode-windows") return Task.FromResult(false);
        if (value is not int selectionIndex) return Task.FromResult(false);

        logService.Log(LogLevel.Info, $"Applying theme mode - Index: {selectionIndex}");

        var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == "theme-mode-windows");
        if (catalogSetting is null)
        {
            logService.Log(LogLevel.Warning,
                "theme-mode-windows missing from the catalog - theme registry write skipped");
            return Task.FromResult(true);
        }

        // The option index IS the state index (Light = 0, Dark = 1), so resolve the state POSITIONALLY
        // rather than mapping the index back to a label.
        //
        // A DETECT-ONLY state ("Mixed", index 2) is NOT an apply target: it carries no Set, and the
        // relationship reverse-sync hands this handler exactly that index when the two theme children
        // disagree. It is handled (return true) but writes nothing - the mix IS the children's own
        // states, and there is nothing for the master to write.
        var themeState = selectionIndex >= 0 && selectionIndex < catalogSetting.States.Count
            ? catalogSetting.States[selectionIndex]
            : null;
        if (themeState is null || themeState.IsDetectOnly)
        {
            logService.Log(LogLevel.Info,
                $"Index {selectionIndex} is not an applicable theme state - nothing written");
            return Task.FromResult(true);
        }

        var plan = ApplyPlan.From(ApplyPlanBuilder.Build(catalogSetting, themeState));
        var result = ApplyExecutor.Execute(plan, stateWriter);
        if (!result.AllSucceeded)
            logService.Log(LogLevel.Warning,
                $"{result.Failed}/{result.Total} theme write op(s) failed: {string.Join("; ", result.Failures)}");

        // This path is synchronous and cannot await, so a process-launching effect would be dropped.
        if (plan.AsyncEffects.Count > 0)
            logService.Log(LogLevel.Error,
                $"{plan.AsyncEffects.Count} async effect(s) NOT run - a theme state gained one but this apply path is synchronous");

        return Task.FromResult(true);
    }
}
