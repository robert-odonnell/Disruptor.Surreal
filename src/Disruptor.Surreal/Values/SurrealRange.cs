namespace Disruptor.Surreal.Values;

/// <summary>
/// A range of <see cref="Value"/>s with inclusive / exclusive / open bounds at each end.
/// Mirrors the Rust client's <c>Range</c> (<c>types/src/value/range.rs</c>).
/// </summary>
/// <remarks>
/// Named with the <c>Surreal</c> prefix to avoid colliding with <see cref="System.Range"/>
/// (the .NET slicing primitive), matching the same pattern as
/// <see cref="SurrealObject"/> / <see cref="SurrealArray"/>.
/// </remarks>
public sealed record SurrealRange(Bound<Value> Start, Bound<Value> End)
{
    /// <summary>A range with no bounds at either end (<c>..</c>).</summary>
    public static SurrealRange Unbounded() =>
        new(Bound.Unbounded<Value>(), Bound.Unbounded<Value>());

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        switch (Start)
        {
            case Bound<Value>.Included i: sb.Append(i.Value); break;
            case Bound<Value>.Excluded e: sb.Append(e.Value).Append('>'); break;
        }
        sb.Append("..");
        switch (End)
        {
            case Bound<Value>.Included i: sb.Append('=').Append(i.Value); break;
            case Bound<Value>.Excluded e: sb.Append(e.Value); break;
        }
        return sb.ToString();
    }
}

/// <summary>
/// A range of <see cref="RecordIdKey"/>s. Mirrors the Rust client's
/// <c>RecordIdKeyRange</c>.
/// </summary>
public sealed record RecordIdKeyRange(Bound<RecordIdKey> Start, Bound<RecordIdKey> End)
{
    /// <summary>A range with no bounds at either end.</summary>
    public static RecordIdKeyRange Unbounded() =>
        new(Bound.Unbounded<RecordIdKey>(), Bound.Unbounded<RecordIdKey>());
}
