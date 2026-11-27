namespace Winhance.Core.Features.Common.Localization;

// A key that exists in en.json. The constructor is private and only the nested classes the generator emits call it,
// so a LocKey cannot name a key the file does not carry.
public readonly partial record struct LocKey
{
    private LocKey(string value) => Value = value;

    public string Value { get; }

    public override string ToString() => Value;

    // For test fixtures only: LocKeyIsSealedTests fails the build if anything under src/ names it.
    internal static LocKey Unchecked(string value) => new(value);
}
