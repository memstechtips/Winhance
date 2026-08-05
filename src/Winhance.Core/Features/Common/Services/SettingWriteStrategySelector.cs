using System;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services;

/// <summary>
/// Picks the write strategy for the active mode.
///
/// The choice is derived from <see cref="ModeCapabilities"/>, not from the mode itself, and that is
/// the point: a new <see cref="Enums.WinhanceMode"/> has to declare what it permits before it can be
/// used at all, and the write strategy then follows from that declaration for free. Switching on the
/// mode here as well would be a second table saying the same thing, free to disagree with the first.
///
/// <para>That declaration is enforced by <c>ModeCapabilitiesTests.EveryDeclaredMode_HasAnExplicitRow</c>,
/// not by the compiler: <see cref="ModeCapabilities.For"/> throws on an unhandled member rather than
/// failing to build. An unhandled arm in a switch expression is only CS8509, a warning, and this repo
/// does not treat warnings as errors — so the test is the thing that goes red, and it runs in
/// <c>winhance-test</c>.</para>
/// </summary>
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
