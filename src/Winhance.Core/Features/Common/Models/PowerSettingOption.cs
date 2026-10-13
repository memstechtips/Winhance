namespace Winhance.Core.Features.Common.Models;

// One entry of a power setting's own list of choices, as Windows reports it. Index is the possible-setting
// index, which is what powercfg writes and what a catalog state carries - NOT the value Windows stores for it
// (measured: the power button's five choices are indices 0-4 and values 0, 2, 3, 6, 8).
public sealed record PowerSettingOption(int Index, string Name);
