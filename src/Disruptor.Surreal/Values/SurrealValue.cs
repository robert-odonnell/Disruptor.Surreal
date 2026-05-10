namespace Disruptor.Surreal.Values;

/// <summary>
/// Discriminator for <see cref="SurrealValue"/> variants.
/// </summary>
public enum SurrealValueKind
{
    None,
    Null,
    Bool,
    Number,
    String,
    Bytes,
    DateTime,
    Duration,
    Uuid,
    List,
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
public abstract record SurrealValue
{
    private protected SurrealValue() { }

    /// <summary>The variant of this value.</summary>
    public abstract SurrealValueKind Kind { get; }

    /// <summary>The singleton <c>NONE</c> value.</summary>
    public static SurrealNoneValue None { get; } = SurrealNoneValue.Instance;

    /// <summary>The singleton <c>NULL</c> value.</summary>
    public static SurrealNullValue Null { get; } = SurrealNullValue.Instance;

    /// <summary>Returns <c>true</c> if this value is NONE or NULL.</summary>
    public bool IsNullish => Kind is SurrealValueKind.None or SurrealValueKind.Null;

    public static implicit operator SurrealValue(bool v) => new SurrealBoolValue(v);
    public static implicit operator SurrealValue(long v) => new SurrealNumberValue(SurrealNumber.FromInt(v));
    public static implicit operator SurrealValue(int v) => new SurrealNumberValue(SurrealNumber.FromInt(v));
    public static implicit operator SurrealValue(double v) => new SurrealNumberValue(SurrealNumber.FromFloat(v));
    public static implicit operator SurrealValue(decimal v) => new SurrealNumberValue(SurrealNumber.FromDecimal(v));
    public static implicit operator SurrealValue(string v) => new StringSurrealValue(v);
    public static implicit operator SurrealValue(byte[] v) => new SurrealBytesValue(v);
    public static implicit operator SurrealValue(Guid v) => new SurrealUuidValue(v);
    public static implicit operator SurrealValue(DateTimeOffset v) => new SurrealDateTimeValue(new SurrealDateTime(v));
    public static implicit operator SurrealValue(TimeSpan v) => new SurrealDurationValue(new SurrealDuration(v));
    public static implicit operator SurrealValue(SurrealRecordId v) => new SurrealRecordIdValue(v);
    public static implicit operator SurrealValue(SurrealTable v) => new SurrealTableValue(v);
}

/// <summary>The absence of a value (SurrealDB <c>NONE</c>).</summary>
public sealed record SurrealNoneValue : SurrealValue
{
    public static readonly SurrealNoneValue Instance = new();
    private SurrealNoneValue() { }
    public override SurrealValueKind Kind => SurrealValueKind.None;
    public override string ToString() => "NONE";
}

/// <summary>An explicit null (SurrealDB <c>NULL</c>).</summary>
public sealed record SurrealNullValue : SurrealValue
{
    public static readonly SurrealNullValue Instance = new();
    private SurrealNullValue() { }
    public override SurrealValueKind Kind => SurrealValueKind.Null;
    public override string ToString() => "NULL";
}

/// <summary>A boolean value.</summary>
public sealed record SurrealBoolValue(bool Value) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.Bool;
    public override string ToString() => Value ? "true" : "false";
}

/// <summary>A numeric value (int64, float64, or decimal).</summary>
public sealed record SurrealNumberValue(SurrealNumber SurrealNumber) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.Number;
    public override string ToString() => SurrealNumber.ToString();
}

/// <summary>A UTF-8 string.</summary>
public sealed record StringSurrealValue(string Value) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.String;
    public override string ToString() => Value;
}

/// <summary>Binary data.</summary>
public sealed record SurrealBytesValue(ReadOnlyMemory<byte> Value) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.Bytes;
    public override string ToString() => $"<bytes:{Value.Length}>";

    public bool Equals(SurrealBytesValue? other) =>
        other is not null && Value.Span.SequenceEqual(other.Value.Span);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(Value.Span);
        return hash.ToHashCode();
    }
}

/// <summary>A datetime with full nanosecond precision.</summary>
public sealed record SurrealDateTimeValue(SurrealDateTime SurrealDateTime) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.DateTime;
    public override string ToString() => SurrealDateTime.ToString();
}

/// <summary>A duration with nanosecond precision.</summary>
public sealed record SurrealDurationValue(SurrealDuration SurrealDuration) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.Duration;
    public override string ToString() => SurrealDuration.ToString();
}

/// <summary>A UUID.</summary>
public sealed record SurrealUuidValue(Guid Value) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.Uuid;
    public override string ToString() => Value.ToString();
}

/// <summary>A list of values.</summary>
public sealed record SurrealListValue(SurrealList List) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.List;
    public override string ToString() => List.ToString();
}

/// <summary>An ordered map of string keys to values.</summary>
public sealed record SurrealObjectValue(SurrealObject Object) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.Object;
    public override string ToString() => Object.ToString();
}

/// <summary>A record identifier.</summary>
public sealed record SurrealRecordIdValue(SurrealRecordId SurrealRecordId) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.RecordId;
    public override string ToString() => SurrealRecordId.ToString();
}

/// <summary>A table reference.</summary>
public sealed record SurrealTableValue(SurrealTable SurrealTable) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.Table;
    public override string ToString() => SurrealTable.ToString();
}

/// <summary>An unordered set of unique values.</summary>
public sealed record SurrealSetValue(SurrealSet Set) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.Set;
    public override string ToString() => Set.ToString();
}

/// <summary>A file reference (bucket + key).</summary>
public sealed record SurrealFileValue(SurrealFile File) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.File;
    public override string ToString() => File.ToString();
}

/// <summary>A range of values with inclusive / exclusive / open bounds.</summary>
public sealed record SurrealRangeValue(SurrealRange Range) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.Range;
    public override string ToString() => Range.ToString();
}

/// <summary>A 2D geometry primitive (point, line, polygon, multi-*, collection).</summary>
public sealed record SurrealGeometryValue(SurrealGeometry SurrealGeometry) : SurrealValue
{
    public override SurrealValueKind Kind => SurrealValueKind.Geometry;
    public override string ToString() => SurrealGeometry.ToString() ?? "<geometry>";
}
