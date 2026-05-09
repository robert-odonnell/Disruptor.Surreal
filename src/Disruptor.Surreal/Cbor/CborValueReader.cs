using System.Formats.Cbor;
using System.Globalization;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Cbor;

/// <summary>
/// Decodes a SurrealDB-flavored CBOR payload back into the <see cref="Value"/> tree.
/// Accepts every tag the writer can produce, plus their alternate text-form encodings
/// (tags 0/9/13) the server may emit interchangeably.
/// </summary>
public static class CborValueReader
{
    /// <summary>Decode a value from a byte buffer.</summary>
    public static Value Decode(ReadOnlyMemory<byte> bytes)
    {
        var reader = new CborReader(bytes, CborConformanceMode.Lax);
        var value = Read(reader);
        if (reader.BytesRemaining != 0)
            throw new CborContentException(
                $"Trailing bytes in CBOR payload ({reader.BytesRemaining} bytes left).");
        return value;
    }

    /// <summary>Decode the next value from <paramref name="reader"/>.</summary>
    public static Value Read(CborReader reader)
    {
        var state = reader.PeekState();

        if (state == CborReaderState.Tag)
        {
            var tag = (ulong)reader.PeekTag();
            return ReadTagged(reader, tag);
        }

        return state switch
        {
            CborReaderState.Null => ReadNull(reader),
            CborReaderState.Boolean => new BoolValue(reader.ReadBoolean()),
            CborReaderState.UnsignedInteger => ReadUnsigned(reader),
            CborReaderState.NegativeInteger => new NumberValue(Number.FromInt(reader.ReadInt64())),
            CborReaderState.HalfPrecisionFloat
                or CborReaderState.SinglePrecisionFloat
                or CborReaderState.DoublePrecisionFloat => new NumberValue(Number.FromFloat(reader.ReadDouble())),
            CborReaderState.TextString => new StringValue(reader.ReadTextString()),
            CborReaderState.ByteString => new BytesValue(reader.ReadByteString()),
            CborReaderState.StartArray => ReadArray(reader),
            CborReaderState.StartMap => ReadObject(reader),
            CborReaderState.SimpleValue => ReadSimple(reader),
            _ => throw new CborContentException($"Unsupported CBOR state: {state}"),
        };
    }

    private static Value ReadNull(CborReader reader)
    {
        reader.ReadNull();
        return Value.Null;
    }

    private static Value ReadUnsigned(CborReader reader)
    {
        // CBOR unsigned can exceed long.MaxValue; fall back to ulong-as-signed via cast.
        var u = reader.ReadUInt64();
        return u <= long.MaxValue
            ? new NumberValue(Number.FromInt((long)u))
            : new NumberValue(Number.FromFloat(u)); // lossy, but rare; matches Rust's i128-as-i64 cast
    }

    private static Value ReadSimple(CborReader reader)
    {
        var simple = reader.ReadSimpleValue();
        return simple switch
        {
            CborSimpleValue.False => new BoolValue(false),
            CborSimpleValue.True => new BoolValue(true),
            CborSimpleValue.Null => Value.Null,
            CborSimpleValue.Undefined => Value.None,
            _ => throw new CborContentException($"Unsupported simple value: {simple}"),
        };
    }

    private static Value ReadTagged(CborReader reader, ulong tag)
    {
        reader.ReadTag();
        return tag switch
        {
            CborTags.None => ReadNoneTag(reader),
            CborTags.Table => new TableValue(new Table(reader.ReadTextString())),
            CborTags.RecordId => ReadRecordId(reader),
            CborTags.SpecDatetime => ReadSpecDatetime(reader),
            CborTags.CustomDatetime => ReadCustomDatetime(reader),
            CborTags.StringUuid => new UuidValue(Guid.Parse(reader.ReadTextString())),
            CborTags.SpecUuid => ReadSpecUuid(reader),
            CborTags.StringDecimal => new NumberValue(
                Number.FromDecimal(decimal.Parse(reader.ReadTextString(), CultureInfo.InvariantCulture))),
            CborTags.StringDuration => new DurationValue(ParseDurationText(reader.ReadTextString())),
            CborTags.CustomDuration => ReadCustomDuration(reader),
            CborTags.Set => ReadSet(reader),
            CborTags.File => ReadFile(reader),
            CborTags.Range => new RangeValue(ReadRange(reader)),
            CborTags.GeometryPoint => new GeometryValue(ReadGeometryPoint(reader)),
            CborTags.GeometryLine => new GeometryValue(ReadGeometryLine(reader)),
            CborTags.GeometryPolygon => new GeometryValue(ReadGeometryPolygon(reader)),
            CborTags.GeometryMultiPoint => new GeometryValue(ReadGeometryMultiPoint(reader)),
            CborTags.GeometryMultiLine => new GeometryValue(ReadGeometryMultiLine(reader)),
            CborTags.GeometryMultiPolygon => new GeometryValue(ReadGeometryMultiPolygon(reader)),
            CborTags.GeometryCollection => new GeometryValue(ReadGeometryCollection(reader)),
            _ => throw new CborContentException($"Unrecognized SurrealDB CBOR tag: {tag}"),
        };
    }

    private static Value ReadNoneTag(CborReader reader)
    {
        // The payload is conventionally null, but some encoders use an empty object.
        var state = reader.PeekState();
        if (state == CborReaderState.Null)
        {
            reader.ReadNull();
        }
        else
        {
            // Skip whatever the payload is.
            reader.SkipValue();
        }
        return Value.None;
    }

    private static Value ReadSpecDatetime(CborReader reader)
    {
        var text = reader.ReadTextString();
        var dto = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        return new DatetimeValue(new Datetime(dto));
    }

    private static Value ReadCustomDatetime(CborReader reader)
    {
        reader.ReadStartArray();
        var seconds = reader.ReadInt64();
        var nanos = reader.ReadUInt32();
        reader.ReadEndArray();
        return new DatetimeValue(new Datetime(seconds, nanos));
    }

    private static Value ReadCustomDuration(CborReader reader)
    {
        var len = reader.ReadStartArray();
        ulong seconds = 0;
        uint nanos = 0;
        if (len is > 0) seconds = reader.ReadUInt64();
        if (len is > 1) nanos = reader.ReadUInt32();
        reader.ReadEndArray();
        return new DurationValue(new Duration(seconds, nanos));
    }

    private static Value ReadSpecUuid(CborReader reader)
    {
        var bytes = reader.ReadByteString();
        if (bytes.Length != 16)
            throw new CborContentException($"UUID byte string must be 16 bytes (got {bytes.Length}).");
        return new UuidValue(new Guid(bytes, bigEndian: true));
    }

    private static Value ReadRecordId(CborReader reader)
    {
        var state = reader.PeekState();
        if (state == CborReaderState.TextString)
        {
            return new RecordIdValue(RecordId.ParseSimple(reader.ReadTextString()));
        }
        if (state != CborReaderState.StartArray)
            throw new CborContentException($"RecordId payload must be text or array; got {state}.");

        reader.ReadStartArray();
        var tableValue = Read(reader);
        var keyValue = Read(reader);
        reader.ReadEndArray();

        if (tableValue is not StringValue tableStr)
            throw new CborContentException("RecordId table component must be a string.");
        var key = ToRecordIdKey(keyValue);
        return new RecordIdValue(new RecordId(new Table(tableStr.Value), key));
    }

    private static RecordIdKey ToRecordIdKey(Value v) => v switch
    {
        StringValue s => new StringRecordIdKey(s.Value),
        NumberValue { Number.Kind: NumberKind.Int } n => new IntegerRecordIdKey(n.Number.AsInt()),
        UuidValue u => new UuidRecordIdKey(u.Value),
        ArrayValue a => new ArrayRecordIdKey(a.Array),
        ObjectValue o => new ObjectRecordIdKey(o.Object),
        // A Range value inside a RecordId key position is a RangeRecordIdKey.
        // Re-walk the inner range so its bounds are typed as RecordIdKey, not Value.
        RangeValue r => new RangeRecordIdKey(ConvertRangeToRecordIdKeyRange(r.Range)),
        _ => throw new CborContentException($"Unsupported RecordId key kind: {v.Kind}"),
    };

    private static RecordIdKeyRange ConvertRangeToRecordIdKeyRange(SurrealRange range) =>
        new(ConvertBound(range.Start), ConvertBound(range.End));

    private static Bound<RecordIdKey> ConvertBound(Bound<Value> bound) => bound switch
    {
        Bound<Value>.Included i => new Bound<RecordIdKey>.Included(ToRecordIdKey(i.Value)),
        Bound<Value>.Excluded e => new Bound<RecordIdKey>.Excluded(ToRecordIdKey(e.Value)),
        Bound<Value>.Unbounded => Bound<RecordIdKey>.Unbounded.Instance,
        _ => throw new CborContentException($"Unhandled Bound: {bound}"),
    };

    private static Value ReadArray(CborReader reader)
    {
        var len = reader.ReadStartArray();
        var arr = len.HasValue ? new SurrealArray(len.Value) : new SurrealArray();
        while (reader.PeekState() != CborReaderState.EndArray)
            arr.Add(Read(reader));
        reader.ReadEndArray();
        return new ArrayValue(arr);
    }

    private static Value ReadSet(CborReader reader)
    {
        var len = reader.ReadStartArray();
        var set = len.HasValue ? new SurrealSet(len.Value) : new SurrealSet();
        while (reader.PeekState() != CborReaderState.EndArray)
            set.Add(Read(reader));
        reader.ReadEndArray();
        return new SetValue(set);
    }

    private static Value ReadFile(CborReader reader)
    {
        var len = reader.ReadStartArray();
        if (len != 2)
            throw new CborContentException($"File payload must be an array of 2 strings; got length {len}.");
        var bucket = reader.ReadTextString();
        var key = reader.ReadTextString();
        reader.ReadEndArray();
        return new FileValue(new SurrealFile(bucket, key));
    }

    private static SurrealRange ReadRange(CborReader reader)
    {
        var len = reader.ReadStartArray();
        if (len != 2)
            throw new CborContentException($"Range payload must be an array of 2 bounds; got length {len}.");
        var start = ReadBound(reader);
        var end = ReadBound(reader);
        reader.ReadEndArray();
        return new SurrealRange(start, end);
    }

    private static Bound<Value> ReadBound(CborReader reader)
    {
        var state = reader.PeekState();
        if (state == CborReaderState.Null)
        {
            reader.ReadNull();
            return Bound<Value>.Unbounded.Instance;
        }
        if (state != CborReaderState.Tag)
            throw new CborContentException($"Bound payload must be a tagged value or null; got {state}.");
        var tag = (ulong)reader.PeekTag();
        return tag switch
        {
            CborTags.BoundIncluded => Inner(reader, true),
            CborTags.BoundExcluded => Inner(reader, false),
            _ => throw new CborContentException($"Unexpected CBOR tag in Bound position: {tag}"),
        };

        static Bound<Value> Inner(CborReader reader, bool included)
        {
            reader.ReadTag();
            var v = Read(reader);
            return included ? new Bound<Value>.Included(v) : new Bound<Value>.Excluded(v);
        }
    }

    // ─── Geometry decoders ────────────────────────────────────────────────────
    // The tag has already been read by ReadTagged before each entry point.

    private static Geometry.Point ReadGeometryPoint(CborReader reader)
    {
        var len = reader.ReadStartArray();
        if (len != 2)
            throw new CborContentException($"Geometry Point payload must be array of 2; got length {len}.");
        var x = reader.ReadDouble();
        var y = reader.ReadDouble();
        reader.ReadEndArray();
        return new Geometry.Point(x, y);
    }

    private static Geometry.Line ReadGeometryLine(CborReader reader)
    {
        reader.ReadStartArray();
        var pts = new List<Geometry.Point>();
        while (reader.PeekState() != CborReaderState.EndArray)
            pts.Add(ReadInnerGeometry(reader) as Geometry.Point ?? throw new CborContentException("Geometry Line elements must be Points."));
        reader.ReadEndArray();
        return new Geometry.Line(pts);
    }

    private static Geometry.Polygon ReadGeometryPolygon(CborReader reader)
    {
        reader.ReadStartArray();
        var lines = new List<Geometry.Line>();
        while (reader.PeekState() != CborReaderState.EndArray)
            lines.Add(ReadInnerGeometry(reader) as Geometry.Line ?? throw new CborContentException("Geometry Polygon elements must be Lines."));
        reader.ReadEndArray();
        if (lines.Count == 0)
            throw new CborContentException("Geometry Polygon must have at least an exterior ring.");
        return new Geometry.Polygon(lines[0], lines.Skip(1));
    }

    private static Geometry.MultiPoint ReadGeometryMultiPoint(CborReader reader)
    {
        reader.ReadStartArray();
        var pts = new List<Geometry.Point>();
        while (reader.PeekState() != CborReaderState.EndArray)
            pts.Add(ReadInnerGeometry(reader) as Geometry.Point ?? throw new CborContentException("MultiPoint elements must be Points."));
        reader.ReadEndArray();
        return new Geometry.MultiPoint(pts);
    }

    private static Geometry.MultiLine ReadGeometryMultiLine(CborReader reader)
    {
        reader.ReadStartArray();
        var lines = new List<Geometry.Line>();
        while (reader.PeekState() != CborReaderState.EndArray)
            lines.Add(ReadInnerGeometry(reader) as Geometry.Line ?? throw new CborContentException("MultiLine elements must be Lines."));
        reader.ReadEndArray();
        return new Geometry.MultiLine(lines);
    }

    private static Geometry.MultiPolygon ReadGeometryMultiPolygon(CborReader reader)
    {
        reader.ReadStartArray();
        var polys = new List<Geometry.Polygon>();
        while (reader.PeekState() != CborReaderState.EndArray)
            polys.Add(ReadInnerGeometry(reader) as Geometry.Polygon ?? throw new CborContentException("MultiPolygon elements must be Polygons."));
        reader.ReadEndArray();
        return new Geometry.MultiPolygon(polys);
    }

    private static Geometry.Collection ReadGeometryCollection(CborReader reader)
    {
        reader.ReadStartArray();
        var items = new List<Geometry>();
        while (reader.PeekState() != CborReaderState.EndArray)
            items.Add(ReadInnerGeometry(reader));
        reader.ReadEndArray();
        return new Geometry.Collection(items);
    }

    /// <summary>Reads a tagged geometry payload, returning the bare <see cref="Geometry"/>.</summary>
    private static Geometry ReadInnerGeometry(CborReader reader)
    {
        if (reader.PeekState() != CborReaderState.Tag)
            throw new CborContentException(
                $"Expected a tagged Geometry value; got {reader.PeekState()}.");
        var tag = (ulong)reader.PeekTag();
        reader.ReadTag();
        return tag switch
        {
            CborTags.GeometryPoint => ReadGeometryPoint(reader),
            CborTags.GeometryLine => ReadGeometryLine(reader),
            CborTags.GeometryPolygon => ReadGeometryPolygon(reader),
            CborTags.GeometryMultiPoint => ReadGeometryMultiPoint(reader),
            CborTags.GeometryMultiLine => ReadGeometryMultiLine(reader),
            CborTags.GeometryMultiPolygon => ReadGeometryMultiPolygon(reader),
            CborTags.GeometryCollection => ReadGeometryCollection(reader),
            _ => throw new CborContentException($"Unexpected tag {tag} in Geometry context."),
        };
    }

    private static RecordIdKeyRange ReadRecordIdKeyRange(CborReader reader)
    {
        var len = reader.ReadStartArray();
        if (len != 2)
            throw new CborContentException($"RecordIdKeyRange payload must be an array of 2 bounds; got length {len}.");
        var start = ReadRecordIdKeyBound(reader);
        var end = ReadRecordIdKeyBound(reader);
        reader.ReadEndArray();
        return new RecordIdKeyRange(start, end);
    }

    private static Bound<RecordIdKey> ReadRecordIdKeyBound(CborReader reader)
    {
        var state = reader.PeekState();
        if (state == CborReaderState.Null)
        {
            reader.ReadNull();
            return Bound<RecordIdKey>.Unbounded.Instance;
        }
        if (state != CborReaderState.Tag)
            throw new CborContentException($"RecordIdKey bound must be tagged or null; got {state}.");
        var tag = (ulong)reader.PeekTag();
        return tag switch
        {
            CborTags.BoundIncluded => Inner(reader, true),
            CborTags.BoundExcluded => Inner(reader, false),
            _ => throw new CborContentException($"Unexpected CBOR tag in RecordIdKey-bound position: {tag}"),
        };

        static Bound<RecordIdKey> Inner(CborReader reader, bool included)
        {
            reader.ReadTag();
            var v = Read(reader);
            var key = ToRecordIdKey(v);
            return included ? new Bound<RecordIdKey>.Included(key) : new Bound<RecordIdKey>.Excluded(key);
        }
    }

    private static Value ReadObject(CborReader reader)
    {
        var len = reader.ReadStartMap();
        var obj = len.HasValue ? new SurrealObject(len.Value) : new SurrealObject();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            obj[key] = Read(reader);
        }
        reader.ReadEndMap();
        return new ObjectValue(obj);
    }

    private static Duration ParseDurationText(string text)
    {
        // Minimal parser for SurrealDB's compact form (e.g. "1h30m45s500ms").
        // Supports unit suffixes: ns, us, µs, ms, s, m, h, d, w, y.
        ulong seconds = 0;
        ulong nanos = 0;

        var i = 0;
        while (i < text.Length)
        {
            var start = i;
            while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                i++;
            if (start == i)
                throw new FormatException($"Invalid duration: {text}");
            var numText = text[start..i];

            var unitStart = i;
            while (i < text.Length && !char.IsDigit(text[i]))
                i++;
            var unit = text[unitStart..i];

            var num = double.Parse(numText, CultureInfo.InvariantCulture);
            switch (unit)
            {
                case "ns": nanos += (ulong)num; break;
                case "us":
                case "µs": nanos += (ulong)(num * 1_000); break;
                case "ms": nanos += (ulong)(num * 1_000_000); break;
                case "s":  seconds += (ulong)num; nanos += (ulong)((num - Math.Truncate(num)) * 1_000_000_000); break;
                case "m":  seconds += (ulong)(num * 60); break;
                case "h":  seconds += (ulong)(num * 3_600); break;
                case "d":  seconds += (ulong)(num * 86_400); break;
                case "w":  seconds += (ulong)(num * 604_800); break;
                case "y":  seconds += (ulong)(num * 31_557_600); break;
                default:   throw new FormatException($"Unknown duration unit: {unit}");
            }
        }

        seconds += nanos / 1_000_000_000;
        nanos %= 1_000_000_000;
        return new Duration(seconds, (uint)nanos);
    }
}
