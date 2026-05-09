namespace Disruptor.Surreal.Values;

/// <summary>
/// Discriminator for <see cref="Value"/> variants.
/// </summary>
public enum ValueKind
{
    None,
    Null,
    Bool,
    Number,
    String,
    Bytes,
    Datetime,
    Duration,
    Uuid,
    Array,
    Object,
    RecordId,
    Table,
    Set,
    File,
    Range,
    Geometry,
}

/// <summary>
/// A SurrealDB value. Closed hierarchy — every variant is a sealed record below.
/// </summary>
public abstract record Value
{
    private protected Value() { }

    /// <summary>The variant of this value.</summary>
    public abstract ValueKind Kind { get; }

    /// <summary>The singleton <c>NONE</c> value.</summary>
    public static NoneValue None { get; } = NoneValue.Instance;

    /// <summary>The singleton <c>NULL</c> value.</summary>
    public static NullValue Null { get; } = NullValue.Instance;

    /// <summary>Returns <c>true</c> if this value is NONE or NULL.</summary>
    public bool IsNullish => Kind is ValueKind.None or ValueKind.Null;

    public static implicit operator Value(bool v) => new BoolValue(v);
    public static implicit operator Value(long v) => new NumberValue(Number.FromInt(v));
    public static implicit operator Value(int v) => new NumberValue(Number.FromInt(v));
    public static implicit operator Value(double v) => new NumberValue(Number.FromFloat(v));
    public static implicit operator Value(decimal v) => new NumberValue(Number.FromDecimal(v));
    public static implicit operator Value(string v) => new StringValue(v);
    public static implicit operator Value(byte[] v) => new BytesValue(v);
    public static implicit operator Value(Guid v) => new UuidValue(v);
    public static implicit operator Value(DateTimeOffset v) => new DatetimeValue(new Datetime(v));
    public static implicit operator Value(TimeSpan v) => new DurationValue(new Duration(v));
    public static implicit operator Value(RecordId v) => new RecordIdValue(v);
    public static implicit operator Value(Table v) => new TableValue(v);
}

/// <summary>The absence of a value (SurrealDB <c>NONE</c>).</summary>
public sealed record NoneValue : Value
{
    public static readonly NoneValue Instance = new();
    private NoneValue() { }
    public override ValueKind Kind => ValueKind.None;
    public override string ToString() => "NONE";
}

/// <summary>An explicit null (SurrealDB <c>NULL</c>).</summary>
public sealed record NullValue : Value
{
    public static readonly NullValue Instance = new();
    private NullValue() { }
    public override ValueKind Kind => ValueKind.Null;
    public override string ToString() => "NULL";
}

/// <summary>A boolean value.</summary>
public sealed record BoolValue(bool Value) : Value
{
    public override ValueKind Kind => ValueKind.Bool;
    public override string ToString() => Value ? "true" : "false";
}

/// <summary>A numeric value (int64, float64, or decimal).</summary>
public sealed record NumberValue(Number Number) : Value
{
    public override ValueKind Kind => ValueKind.Number;
    public override string ToString() => Number.ToString();
}

/// <summary>A UTF-8 string.</summary>
public sealed record StringValue(string Value) : Value
{
    public override ValueKind Kind => ValueKind.String;
    public override string ToString() => Value;
}

/// <summary>Binary data.</summary>
public sealed record BytesValue(ReadOnlyMemory<byte> Value) : Value
{
    public override ValueKind Kind => ValueKind.Bytes;
    public override string ToString() => $"<bytes:{Value.Length}>";

    public bool Equals(BytesValue? other) =>
        other is not null && Value.Span.SequenceEqual(other.Value.Span);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(Value.Span);
        return hash.ToHashCode();
    }
}

/// <summary>A datetime with full nanosecond precision.</summary>
public sealed record DatetimeValue(Datetime Datetime) : Value
{
    public override ValueKind Kind => ValueKind.Datetime;
    public override string ToString() => Datetime.ToString();
}

/// <summary>A duration with nanosecond precision.</summary>
public sealed record DurationValue(Duration Duration) : Value
{
    public override ValueKind Kind => ValueKind.Duration;
    public override string ToString() => Duration.ToString();
}

/// <summary>A UUID.</summary>
public sealed record UuidValue(Guid Value) : Value
{
    public override ValueKind Kind => ValueKind.Uuid;
    public override string ToString() => Value.ToString();
}

/// <summary>An array of values.</summary>
public sealed record ArrayValue(SurrealArray Array) : Value
{
    public override ValueKind Kind => ValueKind.Array;
    public override string ToString() => Array.ToString();
}

/// <summary>An ordered map of string keys to values.</summary>
public sealed record ObjectValue(SurrealObject Object) : Value
{
    public override ValueKind Kind => ValueKind.Object;
    public override string ToString() => Object.ToString();
}

/// <summary>A record identifier.</summary>
public sealed record RecordIdValue(RecordId RecordId) : Value
{
    public override ValueKind Kind => ValueKind.RecordId;
    public override string ToString() => RecordId.ToString();
}

/// <summary>A table reference.</summary>
public sealed record TableValue(Table Table) : Value
{
    public override ValueKind Kind => ValueKind.Table;
    public override string ToString() => Table.ToString();
}

/// <summary>An unordered set of unique values.</summary>
public sealed record SetValue(SurrealSet Set) : Value
{
    public override ValueKind Kind => ValueKind.Set;
    public override string ToString() => Set.ToString();
}

/// <summary>A file reference (bucket + key).</summary>
public sealed record FileValue(SurrealFile File) : Value
{
    public override ValueKind Kind => ValueKind.File;
    public override string ToString() => File.ToString();
}

/// <summary>A range of values with inclusive / exclusive / open bounds.</summary>
public sealed record RangeValue(SurrealRange Range) : Value
{
    public override ValueKind Kind => ValueKind.Range;
    public override string ToString() => Range.ToString();
}

/// <summary>A 2D geometry primitive (point, line, polygon, multi-*, collection).</summary>
public sealed record GeometryValue(Geometry Geometry) : Value
{
    public override ValueKind Kind => ValueKind.Geometry;
    public override string ToString() => Geometry.ToString() ?? "<geometry>";
}
