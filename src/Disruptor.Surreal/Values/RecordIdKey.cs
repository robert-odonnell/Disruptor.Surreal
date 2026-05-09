using System.Globalization;

namespace Disruptor.Surreal.Values;

/// <summary>The variant of a <see cref="RecordIdKey"/>.</summary>
public enum RecordIdKeyKind
{
    String,
    Integer,
    Array,
    Object,
    Uuid,
}

/// <summary>
/// The key portion of a <see cref="RecordId"/>. Records can be keyed by string,
/// integer, UUID, or a composite array/object.
/// </summary>
public abstract record RecordIdKey
{
    private protected RecordIdKey() { }

    public abstract RecordIdKeyKind Kind { get; }

    public static implicit operator RecordIdKey(string value) => new StringRecordIdKey(value);
    public static implicit operator RecordIdKey(long value) => new IntegerRecordIdKey(value);
    public static implicit operator RecordIdKey(int value) => new IntegerRecordIdKey(value);
    public static implicit operator RecordIdKey(Guid value) => new UuidRecordIdKey(value);
}

/// <summary>A string-typed record key (the most common case).</summary>
public sealed record StringRecordIdKey(string Value) : RecordIdKey
{
    public override RecordIdKeyKind Kind => RecordIdKeyKind.String;
    public override string ToString() => Value;
}

/// <summary>An int64-typed record key.</summary>
public sealed record IntegerRecordIdKey(long Value) : RecordIdKey
{
    public override RecordIdKeyKind Kind => RecordIdKeyKind.Integer;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A UUID-typed record key.</summary>
public sealed record UuidRecordIdKey(Guid Value) : RecordIdKey
{
    public override RecordIdKeyKind Kind => RecordIdKeyKind.Uuid;
    public override string ToString() => $"u'{Value:D}'";
}

/// <summary>A composite array key.</summary>
public sealed record ArrayRecordIdKey(SurrealArray Items) : RecordIdKey
{
    public override RecordIdKeyKind Kind => RecordIdKeyKind.Array;
    public override string ToString() => Items.ToString();
}

/// <summary>A composite object key.</summary>
public sealed record ObjectRecordIdKey(SurrealObject Fields) : RecordIdKey
{
    public override RecordIdKeyKind Kind => RecordIdKeyKind.Object;
    public override string ToString() => Fields.ToString();
}
