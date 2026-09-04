using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Catalog;

internal class WindowsAsyncEffectRunner(
    IPowerShellRunner powerShell,
    IRegImportService regImport,
    ILogService log) : IAsyncEffectRunner
{
    public async Task<bool> RunAsync(Effect effect)
    {
        try
        {
            switch (effect)
            {
                case ScriptEffect s:
                    // Result deliberately not inspected. RunContext is carried on the effect but not passed.
                    await powerShell.RunScriptInMemoryAsync(s.Script).ConfigureAwait(false);
                    return true;

                case RegContentEffect r:
                    // A non-zero reg.exe exit is logged by the service, not treated as failure.
                    await regImport.RunRegImportAsync(r.Content).ConfigureAwait(false);
                    return true;

                default:
                    // Only IsAsyncIo effects are routed here, so an unrecognised one means the two have
                    // drifted apart - loud, not a silent success.
                    log.Log(LogLevel.Error,
                        $"No handler for deferred effect {effect.GetType().Name}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            log.Log(LogLevel.Error,
                $"{effect.GetType().Name} threw: {ex.Message}");
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> RunAllAsync(IReadOnlyList<Effect> effects)
    {
        if (effects.Count == 0)
            return Array.Empty<string>();

        var failures = new List<string>();
        foreach (var effect in effects)
        {
            if (!await RunAsync(effect).ConfigureAwait(false))
                failures.Add($"Deferred effect failed: {effect.GetType().Name}");
        }

        return failures;
    }
}
