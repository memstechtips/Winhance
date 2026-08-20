using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.TechnicalDetails;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Optimize.ViewModels;

// Marshals the build onto the UI thread; the documentation rules live in TechnicalDetailsBuilder, which is pure and unit-tested.
internal sealed class TechnicalDetailsManager
{
    private readonly Func<string> _getSettingId;
    private readonly Action<OptionMatrix?> _setMatrix;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IRegeditLauncher? _regeditLauncher;
    private readonly ILocalizationService _localizationService;
    private readonly WinBuild _build;

    public IRelayCommand<string> OpenRegeditCommand { get; }

    public TechnicalDetailsManager(
        Func<string> getSettingId,
        Action<OptionMatrix?> setMatrix,
        ILogService logService,
        IDispatcherService dispatcherService,
        IRegeditLauncher? regeditLauncher,
        ILocalizationService localizationService,
        WinBuild build)
    {
        _getSettingId = getSettingId;
        _setMatrix = setMatrix;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _regeditLauncher = regeditLauncher;
        _localizationService = localizationService;
        _build = build;

        OpenRegeditCommand = new RelayCommand<string>(OpenRegeditAtPath);
    }

    public void Update(Setting? setting, SettingStateSnapshot snapshot)
    {
        _dispatcherService.RunOnUIThread(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                var matrix = TechnicalDetailsBuilder.Build(setting, snapshot, _localizationService, _build);
                _setMatrix(matrix);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error,
                    $"[TechnicalDetails] Build failed for '{_getSettingId()}': {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        });
    }

    private void OpenRegeditAtPath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
            _regeditLauncher?.OpenAtPath(path);
    }
}
