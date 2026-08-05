using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;

namespace Winhance.TestSupport;

/// <summary>
/// Builds the real <see cref="SettingWriteStrategySelector"/> over the real strategies, for the
/// many fixtures that construct a <c>SettingItemViewModel</c>.
///
/// Deliberately not a stub of the selector: a test that toggles a setting should travel the same
/// write path the app does, so mode-specific behaviour is exercised rather than asserted against a
/// second implementation that can drift from the first.
/// </summary>
public static class SettingWriteStrategies
{
    /// <summary>
    /// A selector over live/authoring/read-only strategies. An omitted
    /// <paramref name="modeService"/> stands in as an unstubbed mock, which answers
    /// <c>WinhanceMode.Normal</c> — the live path, which is what a fixture that does not mention
    /// modes means.
    /// </summary>
    public static ISettingWriteStrategySelector Selector(
        ISettingApplicationService applicationService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        ILogService logService,
        IApplicationModeService? modeService = null)
    {
        modeService ??= Mock.Of<IApplicationModeService>();

        return new SettingWriteStrategySelector(
            modeService,
            new LiveSettingWriteStrategy(applicationService, dialogService, localizationService, logService),
            new BuilderSettingWriteStrategy(modeService, logService),
            new ReadOnlySettingWriteStrategy(logService));
    }
}
