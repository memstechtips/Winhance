using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services;

// Derived from ModeCapabilities, not from the mode: a new WinhanceMode has to declare what it permits before it
// can be used, and the strategy follows for free. Switching on the mode here as well would be a second table
// free to disagree with the first.
public sealed class SettingWriteStrategySelector : ISettingWriteStrategySelector
{
    private readonly IApplicationModeService _applicationModeService;
    private readonly ISettingWriteStrategy _apply;
    private readonly ISettingWriteStrategy _author;
    private readonly ISettingWriteStrategy _refuse;

    public SettingWriteStrategySelector(
        IApplicationModeService applicationModeService,
        LiveSettingWriteStrategy apply,
        BuilderSettingWriteStrategy author,
        ReadOnlySettingWriteStrategy refuse)
    {
        _applicationModeService = applicationModeService;
        _apply = apply;
        _author = author;
        _refuse = refuse;
    }

    public ISettingWriteStrategy ForCurrentMode()
    {
        var capabilities = _applicationModeService.Capabilities();

        if (!capabilities.SettingsEditable)
            return _refuse;

        if (capabilities.AuthorsIntent)
            return _author;

        if (capabilities.AppliesToSystem)
            return _apply;

        throw new InvalidOperationException(
            $"Mode '{_applicationModeService.CurrentMode}' permits editing but neither applies edits " +
            "nor authors them, so there is nowhere for an edit to go. Give it AppliesToSystem or " +
            "AuthorsIntent in ModeCapabilities, or make it read-only.");
    }
}
