namespace Disruptor.Surreal.Values;

/// <summary>
/// Abstraction over a SurrealDB record identifier. Implemented by the SDK's <see cref="RecordId"/>
/// and by any consumer-defined per-table id type (e.g. an ORM's generated <c>{Name}Id</c> structs)
/// that wants to flow through the SDK's API surface without per-callsite conversion.
/// </summary>
public interface IRecordId
{
    /// <summary>The table component of the id.</summary>
    Table Table { get; }

    /// <summary>The key component of the id.</summary>
    RecordIdKey Key { get; }
}

/// <summary>Convenience helpers for working with <see cref="IRecordId"/> values.</summary>
public static class RecordIdExtensions
{
    /// <summary>
    /// Materialise any <see cref="IRecordId"/> as a concrete <see cref="RecordId"/>.
    /// No-op when the source is already a <see cref="RecordId"/>.
    /// </summary>
    public static RecordId ToRecordId(this IRecordId id) =>
        id as RecordId ?? new RecordId(id.Table, id.Key);
}
