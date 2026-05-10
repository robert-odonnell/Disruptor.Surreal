namespace Disruptor.Surreal.Values;

/// <summary>
/// A SurrealDB table reference (a typed wrapper over a name).
/// </summary>
public sealed record SurrealTable(string Name) : IComparable<SurrealTable>
{
    /// <summary>Implicit conversion from string for ergonomics.</summary>
    public static implicit operator SurrealTable(string name) => new(name);

    public int CompareTo(SurrealTable? other) =>
        other is null ? 1 : string.CompareOrdinal(Name, other.Name);

    public override string ToString() => Name;
}
