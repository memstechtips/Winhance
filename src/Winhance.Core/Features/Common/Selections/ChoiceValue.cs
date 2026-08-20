namespace Winhance.Core.Features.Common.Selections;

// One setting's chosen state, independent of where it came from (machine snapshot, Builder edit, seed, file) and
// of where it goes (file, autounattend, live apply). Numbers are SYSTEM units - what powercfg stores and what the
// .winhance file has always held; the ViewModel converts on the way in, so no consumer converts.
public abstract record ChoiceValue
{
    private ChoiceValue() { }

    public sealed record Toggle(bool On) : ChoiceValue;
    public sealed record Option(int Index) : ChoiceValue;
    public sealed record CustomValues(IReadOnlyDictionary<string, object> Values) : ChoiceValue;
    public sealed record AcDcOption(int AcIndex, int DcIndex) : ChoiceValue;
    public sealed record Number(int Value) : ChoiceValue;
    public sealed record AcDcNumber(int Ac, int Dc) : ChoiceValue;
    public sealed record PowerPlan(string Guid, string Name) : ChoiceValue;
}
