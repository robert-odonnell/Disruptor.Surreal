using System.Globalization;

namespace Disruptor.Surreal.Values;

/// <summary>The variant of a <see cref="SurrealRecordIdKey"/>.</summary>
public enum SurrealRecordIdKeyKind
{
    String,
    Integer,
    List,
    Object,
    Uuid,
    Ulid,
    Range,
}

/// <summary>
/// The key portion of a <see cref="SurrealRecordId"/>. Records can be keyed by string,
/// integer, UUID, or a composite array/object.
/// </summary>
public abstract record SurrealRecordIdKey
{
    private protected SurrealRecordIdKey() { }

    public abstract SurrealRecordIdKeyKind Kind { get; }

    public static implicit operator SurrealRecordIdKey(string value) => new SurrealStringRecordIdKey(value);
    public static implicit operator SurrealRecordIdKey(long value) => new SurrealIntegerRecordIdKey(value);
    public static implicit operator SurrealRecordIdKey(int value) => new SurrealIntegerRecordIdKey(value);
    public static implicit operator SurrealRecordIdKey(Guid value) => new SurrealUuidRecordIdKey(value);
    public static implicit operator SurrealRecordIdKey(Ulid value) => new SurrealUlidRecordIdKey(value);
}

/// <summary>A string-typed record key (the most common case).</summary>
public sealed record SurrealStringRecordIdKey(string Value) : SurrealRecordIdKey
{
    public override SurrealRecordIdKeyKind Kind => SurrealRecordIdKeyKind.String;
    public override string ToString() => Value;
}

/// <summary>An int64-typed record key.</summary>
public sealed record SurrealIntegerRecordIdKey(long Value) : SurrealRecordIdKey
{
    public override SurrealRecordIdKeyKind Kind => SurrealRecordIdKeyKind.Integer;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A UUID-typed record key.</summary>
public sealed record SurrealUuidRecordIdKey(Guid Value) : SurrealRecordIdKey
{
    public override SurrealRecordIdKeyKind Kind => SurrealRecordIdKeyKind.Uuid;
    public override string ToString() => $"u'{Value:D}'";
}

/// <summary>A ULID-typed record key.</summary>
public sealed record SurrealUlidRecordIdKey(Ulid Value) : SurrealRecordIdKey
{
    public override SurrealRecordIdKeyKind Kind => SurrealRecordIdKeyKind.Ulid;
    public override string ToString() => Value.ToString();
}

/// <summary>A composite array key.</summary>
public sealed record SurrealListRecordIdKey(SurrealList Items) : SurrealRecordIdKey
{
    public override SurrealRecordIdKeyKind Kind => SurrealRecordIdKeyKind.List;
    public override string ToString() => Items.ToString();
}

/// <summary>A composite object key.</summary>
public sealed record SurrealObjectRecordIdKey(SurrealObject Fields) : SurrealRecordIdKey
{
    public override SurrealRecordIdKeyKind Kind => SurrealRecordIdKeyKind.Object;
    public override string ToString() => Fields.ToString();
}

/// <summary>A range over record-id keys (e.g. <c>person:a..z</c>).</summary>
public sealed record SurrealRangeRecordIdKey(RecordIdKeyRange Range) : SurrealRecordIdKey
{
    public override SurrealRecordIdKeyKind Kind => SurrealRecordIdKeyKind.Range;
    public override string ToString() => Range.ToString() ?? "..";
}
