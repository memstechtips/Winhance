namespace Winhance.Core.Features.Common.Catalog;

// Which catalog settings a caller wants to see. CurrentMachine = the OS-build gate, the hardware gate and the
// powercfg-existence gate all apply. Each flag relaxes one gate; relaxing hardware also relaxes existence for
// hardware-gated settings (a battery GUID that is absent on a desktop cannot pass existence but can be authored).
public readonly record struct CatalogScope(bool IncludeOtherOsVersions, bool IncludeOtherHardware)
{
    public static readonly CatalogScope CurrentMachine = new(false, false);
}
