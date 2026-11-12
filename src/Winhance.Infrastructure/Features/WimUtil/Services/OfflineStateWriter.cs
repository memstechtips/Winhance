using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Optimize.Services;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// The registry members run through WindowsStateWriter over the offline registry, so every byte-level edit behaves as
// it does live. Power values and the stock power plans are registry values in the image's SYSTEM hive and are written
// there. Tasks, custom power plans, themes, DNS and scripts have no session to act on in a mounted image; the setup
// script applies them.
internal sealed class OfflineStateWriter : IStateWriter
{
    private readonly OfflineRegistry _registry;
    private Guid? _powerScheme;
    private readonly WindowsStateWriter _registryWriter;
    private readonly ILogService _log;

    public OfflineStateWriter(
        OfflineRegistry registry,
        IScheduledTaskStateService tasks,
        IPowerCfgApplier powerCfg,
        IPowerPlanActivationService activation,
        IWindowsThemeService theme,
        IDnsSettingsService dns,
        ISystemParametersService systemParameters,
        ILogService log)
    {
        _registry = registry;
        _log = log;
        _registryWriter = new WindowsStateWriter(registry, tasks, powerCfg, activation, theme, dns, systemParameters, new NoServiceControl(), log);
    }

    public bool WriteRegistry(RegTarget target, string path, object value) =>
        LeavesMissingService(path) || _registryWriter.WriteRegistry(target, path, value);

    public bool DeleteRegistry(RegTarget target, string path) =>
        LeavesMissingService(path) || _registryWriter.DeleteRegistry(target, path);

    public bool EnsureRegistryKey(RegTarget target, string path) => _registryWriter.EnsureRegistryKey(target, path);

    public bool UnlockKey(RegTarget target, string path) => _registryWriter.UnlockKey(target, path);

    // The lock is an ACL on the live key; the setup script takes it after the value is in place.
    public bool LockKey(RegTarget target, string path) => true;

    public bool SetRegistryBit(RegTarget target, string path, int byteIndex, byte bitMask, bool set) =>
        _registryWriter.SetRegistryBit(target, path, byteIndex, bitMask, set);

    public bool SetRegistryByte(RegTarget target, string path, int byteIndex, byte value) =>
        _registryWriter.SetRegistryByte(target, path, byteIndex, value);

    public bool SetRegistryStringFlag(RegTarget target, string path, int flagMask, int absentBase, bool set) =>
        _registryWriter.SetRegistryStringFlag(target, path, flagMask, absentBase, set);

    public bool SetRegistryListItem(RegTarget target, string path, string prefix, string item, bool present) =>
        _registryWriter.SetRegistryListItem(target, path, prefix, item, present);

    public bool SetRegistryComposite(RegTarget target, string path, string compositeKey, string? subValue) =>
        _registryWriter.SetRegistryComposite(target, path, compositeKey, subValue);

    public bool WriteRegistryPerSubkey(RegTarget target, string parentPath, object value) =>
        _registryWriter.WriteRegistryPerSubkey(target, parentPath, value);

    public bool DeleteRegistryPerSubkey(RegTarget target, string parentPath) =>
        _registryWriter.DeleteRegistryPerSubkey(target, parentPath);

    public bool SetTask(TaskTarget target, bool enabled) => true;

    // The plan the answer file will activate, when the user picked one. Power values go into it, not into the image's
    // active plan.
    internal Guid? PickedPowerPlan { get; set; }

    // A plan Windows does not ship is copied from Balanced during setup, which also writes its values.
    public bool WritePowerCfgValue(PowerCfgTarget target, PowerContext context, int value)
    {
        if (PowerSchemeForWrites() is not { } scheme)
        {
            return true;
        }

        var key = OfflinePowerSchemes.SettingKey(target, scheme);
        return OfflinePowerSchemes.ValueNames(context)
            .All(name => _registry.SetProtectedValue(key, name, value, RegistryValueKind.DWord));
    }

    public bool ActivatePowerPlan(string guid)
    {
        if (!Guid.TryParse(guid, out var plan))
        {
            _log.LogWarning($"ActivatePowerPlan: invalid GUID '{guid}'");
            return false;
        }

        return !OfflinePowerSchemes.IsStock(plan)
            || _registry.SetProtectedValue(OfflinePowerSchemes.SchemesKey, OfflinePowerSchemes.ActiveSchemeValue, plan.ToString("D"), RegistryValueKind.String);
    }

    private Guid? PowerSchemeForWrites()
    {
        if (PickedPowerPlan is { } picked)
        {
            return OfflinePowerSchemes.IsStock(picked) ? picked : null;
        }

        return _powerScheme ??= OfflinePowerSchemes.ActiveSchemeOf(_registry);
    }

    public bool SetSlideshow(DesktopSlideshowTarget target, string folder) => true;

    public bool SetDns(IReadOnlyList<DnsServer> servers) => true;

    public bool RunEffect(Effect effect) =>
        effect is not RegistryWriteEffect || _registryWriter.RunEffect(effect);

    // A start type written under a service the image does not have would create a key for a service that cannot run.
    private bool LeavesMissingService(string path)
    {
        if (ServiceStartControl.ServiceNameOf(path) is not { } service || _registry.KeyExists(path))
        {
            return false;
        }

        _log.LogInformation($"The image has no {service} service, so its setting is left for setup");
        return true;
    }

    // The service manager belongs to the running PC; the offline hive only needs the value.
    private sealed class NoServiceControl : IServiceStartControl
    {
        public void Apply(string serviceName, int start)
        {
        }
    }
}
