using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;

namespace Winhance.TestSupport;

// Deliberately not a stub of the selector: a test that toggles a setting should travel the same write path the
// app does, so mode-specific behaviour is exercised rather than asserted against a second implementation that can drift.
public static class SettingWriteStrategies
{
    // An omitted modeService is an unstubbed mock, which answers Normal - the live path.
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
