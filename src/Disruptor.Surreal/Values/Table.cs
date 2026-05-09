namespace Disruptor.Surreal.Values;

/// <summary>
/// A SurrealDB table reference (a typed wrapper over a name).
/// </summary>
public sealed record Table(string Name) : IComparable<Table>
{
    /// <summary>Implicit conversion from string for ergonomics.</summary>
    public static implicit operator Table(string name) => new(name);

    public int CompareTo(Table? other) =>
        other is null ? 1 : string.CompareOrdinal(Name, other.Name);

    public override string ToString() => Name;
}
