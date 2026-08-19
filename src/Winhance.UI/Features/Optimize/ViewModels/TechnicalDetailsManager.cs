using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.TechnicalDetails;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// UI adapter for the Technical Details panel: marshals the build onto the UI thread and hands the
/// result to the view-model. All the documentation rules live in <see cref="TechnicalDetailsBuilder"/>,
/// which is pure and unit-tested. The command is no longer attached to the model - it reaches the
/// buttons as a dependency property on the control, so Core carries no ICommand at all.
/// </summary>
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

    /// <summary>Rebuilds the panel from the setting model + current-state snapshot, on the UI thread.</summary>
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
