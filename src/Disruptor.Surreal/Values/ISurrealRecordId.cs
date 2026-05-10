namespace Disruptor.Surreal.Values;

/// <summary>
/// Abstraction over a SurrealDB record identifier. Implemented by the SDK's <see cref="SurrealRecordId"/>
/// and by any consumer-defined per-table id type (e.g. an ORM's generated <c>{Name}Id</c> structs)
/// that wants to flow through the SDK's API surface without per-callsite conversion.
/// </summary>
public interface ISurrealRecordId
{
    /// <summary>The table component of the id.</summary>
    SurrealTable Table { get; }

    /// <summary>The key component of the id.</summary>
    SurrealRecordIdKey Key { get; }
}

/// <summary>Convenience helpers for working with <see cref="ISurrealRecordId"/> values.</summary>
public static class RecordIdExtensions
{
    /// <summary>
    /// Materialise any <see cref="ISurrealRecordId"/> as a concrete <see cref="SurrealRecordId"/>.
    /// No-op when the source is already a <see cref="SurrealRecordId"/>.
    /// </summary>
    public static SurrealRecordId ToRecordId(this ISurrealRecordId id) =>
        id as SurrealRecordId ?? new SurrealRecordId(id.Table, id.Key);
}
