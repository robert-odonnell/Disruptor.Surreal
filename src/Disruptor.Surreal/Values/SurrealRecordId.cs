namespace Disruptor.Surreal.Values;

/// <summary>
/// A SurrealDB record identifier — a (table, key) pair.
/// </summary>
public sealed record SurrealRecordId(SurrealTable Table, SurrealRecordIdKey Key) : ISurrealRecordId
{
    /// <summary>Convenience overload for the common <c>(table-name, string-key)</c> shape.</summary>
    public SurrealRecordId(string table, string key) : this(new SurrealTable(table), new SurrealStringRecordIdKey(key)) { }

    /// <summary>Convenience overload for the common <c>(table-name, integer-key)</c> shape.</summary>
    public SurrealRecordId(string table, long key) : this(new SurrealTable(table), new SurrealIntegerRecordIdKey(key)) { }

    /// <summary>Convenience overload for the common <c>(table-name, uuid-key)</c> shape.</summary>
    public SurrealRecordId(string table, Guid key) : this(new SurrealTable(table), new SurrealUuidRecordIdKey(key)) { }

    /// <summary>Convenience overload for the common <c>(table-name, ulid-key)</c> shape.</summary>
    public SurrealRecordId(string table, Ulid key) : this(new SurrealTable(table), new SurrealUlidRecordIdKey(key)) { }

    /// <summary>Parses a record id of the form <c>table:key</c> (string keys only).</summary>
    public static SurrealRecordId ParseSimple(string text)
    {
        var idx = text.IndexOf(':');
        if (idx <= 0 || idx == text.Length - 1)
            throw new FormatException($"Invalid record id: {text}");
        return new SurrealRecordId(text[..idx], text[(idx + 1)..]);
    }

    public override string ToString() => $"{Table}:{Key}";
}
