using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Selections;

// One setting's chosen state, independent of where it came from (machine snapshot, Builder edit, seed, file) and
// of where it goes (file, autounattend, live apply). Numbers are SYSTEM units - what powercfg stores and what the
// .winhance file holds; the ViewModel converts on the way in, so no consumer converts.
public abstract record ChoiceValue
{
    private ChoiceValue() { }

    public sealed record Toggle(bool On) : ChoiceValue;
    public sealed record CheckBox(bool Checked) : ChoiceValue;
    public sealed record Text(string Value) : ChoiceValue;

    // Field key to value: a Selection field holds its option index as a string, a CheckBox "true" or "false".
    public sealed record ListRow(IReadOnlyDictionary<string, string> Values)
    {
        public bool Equals(ListRow? other) =>
            other is not null && Values.Count == other.Values.Count
            && Values.All(pair => other.Values.TryGetValue(pair.Key, out var value) && value == pair.Value);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var (key, value) in Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                hash.Add(key);
                hash.Add(value);
            }
            return hash.ToHashCode();
        }
    }

    // SavePasswords governs the config file only; the answer file carries each row's password either way.
    public sealed record List(IReadOnlyList<ListRow> Rows, bool SavePasswords = false) : ChoiceValue
    {
        public bool Equals(List? other) =>
            other is not null && SavePasswords == other.SavePasswords && Rows.SequenceEqual(other.Rows);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(SavePasswords);
            foreach (var row in Rows)
                hash.Add(row);
            return hash.ToHashCode();
        }
    }

    public sealed record Option(int Index) : ChoiceValue;
    public sealed record CustomValues(IReadOnlyDictionary<string, object> Values) : ChoiceValue;
    public sealed record AcDcOption(int AcIndex, int DcIndex) : ChoiceValue;
    public sealed record Number(int Value) : ChoiceValue;
    public sealed record AcDcNumber(int Ac, int Dc) : ChoiceValue;

    // Label is what the dropdown showed at save time, so a PC that does not offer the key can still name it in the review.
    public sealed record Keyed(string Key, string Label) : ChoiceValue;

    public static ChoiceValue TwoState(ControlKind kind, bool on) =>
        kind == ControlKind.CheckBox ? new CheckBox(on) : new Toggle(on);
}
