namespace Disruptor.Surreal.Values;

/// <summary>
/// A SurrealDB record identifier — a (table, key) pair.
/// </summary>
public sealed record RecordId(Table Table, RecordIdKey Key) : IRecordId
{
    /// <summary>Convenience overload for the common <c>(table-name, string-key)</c> shape.</summary>
    public RecordId(string table, string key) : this(new Table(table), new StringRecordIdKey(key)) { }

    /// <summary>Convenience overload for the common <c>(table-name, integer-key)</c> shape.</summary>
    public RecordId(string table, long key) : this(new Table(table), new IntegerRecordIdKey(key)) { }

    /// <summary>Convenience overload for the common <c>(table-name, uuid-key)</c> shape.</summary>
    public RecordId(string table, Guid key) : this(new Table(table), new UuidRecordIdKey(key)) { }

    /// <summary>Parses a record id of the form <c>table:key</c> (string keys only).</summary>
    public static RecordId ParseSimple(string text)
    {
        var idx = text.IndexOf(':');
        if (idx <= 0 || idx == text.Length - 1)
            throw new FormatException($"Invalid record id: {text}");
        return new RecordId(text[..idx], text[(idx + 1)..]);
    }

    public override string ToString() => $"{Table}:{Key}";
}
