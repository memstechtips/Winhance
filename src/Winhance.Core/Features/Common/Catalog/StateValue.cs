namespace Winhance.Core.Features.Common.Catalog;

// Carries the write payload AND derives its own detection-accept set, so detect and apply come from one
// declaration - never author an accepted-values list separately.
public sealed record StateValue
{
    private StateValue(
        IReadOnlyList<object?> acceptedValues,
        bool acceptsAbsent,
        bool acceptsAnyPresent,
        object? writePayload,
        bool deleteOnWrite)
    {
        AcceptedValues = acceptedValues;
        AcceptsAbsent = acceptsAbsent;
        AcceptsAnyPresent = acceptsAnyPresent;
        WritePayload = writePayload;
        DeleteOnWrite = deleteOnWrite;
    }

    public IReadOnlyList<object?> AcceptedValues { get; }
    public bool AcceptsAbsent { get; init; }
    public bool AcceptsAnyPresent { get; }
    public object? WritePayload { get; }
    public bool DeleteOnWrite { get; }

    public static StateValue Of(object value) =>
        new(new object?[] { value }, acceptsAbsent: false, acceptsAnyPresent: false,
            writePayload: value, deleteOnWrite: false);

    public static StateValue OneOf(params object?[] values)
    {
        if (values.All(v => v == null))
            throw new System.ArgumentException(
                "OneOf needs at least one non-null value. For 'absent counts', use Of(x).OrAbsent() or Absent.",
                nameof(values));
        return new(values.ToArray(), acceptsAbsent: false, acceptsAnyPresent: false,
            writePayload: values.First(v => v != null), deleteOnWrite: false);
    }

    public static readonly StateValue Absent =
        new(System.Array.Empty<object?>(), acceptsAbsent: true, acceptsAnyPresent: false,
            writePayload: null, deleteOnWrite: true);

    public static readonly StateValue Exists =
        new(System.Array.Empty<object?>(), acceptsAbsent: false, acceptsAnyPresent: true,
            writePayload: null, deleteOnWrite: false);

    // The key being absent also counts as this state; the write is unchanged.
    public StateValue OrAbsent() => this with { AcceptsAbsent = true };

    public bool Matches(object? currentReading, bool present)
    {
        if (!present)
            return AcceptsAbsent;
        if (AcceptsAnyPresent)
            return true;
        return AcceptedValues.Any(accepted => CatalogValueComparer.AreEqual(currentReading, accepted));
    }
}
