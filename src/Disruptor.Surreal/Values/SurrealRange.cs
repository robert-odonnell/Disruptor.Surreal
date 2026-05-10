namespace Disruptor.Surreal.Values;

/// <summary>
/// A range of <see cref="SurrealValue"/>s with inclusive / exclusive / open bounds at each end.
/// Mirrors the Rust client's <c>Range</c> (<c>types/src/value/range.rs</c>).
/// </summary>
/// <remarks>
/// Named with the <c>Surreal</c> prefix to avoid colliding with <see cref="System.Range"/>
/// (the .NET slicing primitive), matching the same pattern as
/// <see cref="SurrealObject"/> / <see cref="SurrealList"/>.
/// </remarks>
public sealed record SurrealRange(SurrealBound<SurrealValue> Start, SurrealBound<SurrealValue> End)
{
    /// <summary>A range with no bounds at either end (<c>..</c>).</summary>
    public static SurrealRange Unbounded() =>
        new(SurrealBound.Unbounded<SurrealValue>(), SurrealBound.Unbounded<SurrealValue>());

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        switch (Start)
        {
            case SurrealBound<SurrealValue>.Included i: sb.Append(i.Value); break;
            case SurrealBound<SurrealValue>.Excluded e: sb.Append(e.Value).Append('>'); break;
        }
        sb.Append("..");
        switch (End)
        {
            case SurrealBound<SurrealValue>.Included i: sb.Append('=').Append(i.Value); break;
            case SurrealBound<SurrealValue>.Excluded e: sb.Append(e.Value); break;
        }
        return sb.ToString();
    }
}

/// <summary>
/// A range of <see cref="SurrealRecordIdKey"/>s. Mirrors the Rust client's
/// <c>RecordIdKeyRange</c>.
/// </summary>
public sealed record RecordIdKeyRange(SurrealBound<SurrealRecordIdKey> Start, SurrealBound<SurrealRecordIdKey> End)
{
    /// <summary>A range with no bounds at either end.</summary>
    public static RecordIdKeyRange Unbounded() =>
        new(SurrealBound.Unbounded<SurrealRecordIdKey>(), SurrealBound.Unbounded<SurrealRecordIdKey>());
}
