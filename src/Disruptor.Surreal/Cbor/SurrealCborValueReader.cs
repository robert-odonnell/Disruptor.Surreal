using System.Formats.Cbor;
using System.Globalization;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Cbor;

/// <summary>
/// Decodes a SurrealDB-flavored CBOR payload back into the <see cref="SurrealValue"/> tree.
/// Accepts every tag the writer can produce, plus their alternate text-form encodings
/// (tags 0/9/13) the server may emit interchangeably.
/// </summary>
public static class SurrealCborValueReader
{
    /// <summary>Decode a value from a byte buffer.</summary>
    public static SurrealValue Decode(ReadOnlyMemory<byte> bytes)
    {
        var reader = new CborReader(bytes, CborConformanceMode.Lax);
        var value = Read(reader);
        if (reader.BytesRemaining != 0)
            throw new CborContentException(
                $"Trailing bytes in CBOR payload ({reader.BytesRemaining} bytes left).");
        return value;
    }

    /// <summary>Decode the next value from <paramref name="reader"/>.</summary>
    public static SurrealValue Read(CborReader reader)
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
            CborReaderState.Boolean => new SurrealBoolValue(reader.ReadBoolean()),
            CborReaderState.UnsignedInteger => ReadUnsigned(reader),
            CborReaderState.NegativeInteger => new SurrealNumberValue(SurrealNumber.FromInt(reader.ReadInt64())),
            CborReaderState.HalfPrecisionFloat
                or CborReaderState.SinglePrecisionFloat
                or CborReaderState.DoublePrecisionFloat => new SurrealNumberValue(SurrealNumber.FromFloat(reader.ReadDouble())),
            CborReaderState.TextString => new StringSurrealValue(reader.ReadTextString()),
            CborReaderState.ByteString => new SurrealBytesValue(reader.ReadByteString()),
            CborReaderState.StartArray => ReadList(reader),
            CborReaderState.StartMap => ReadObject(reader),
            CborReaderState.SimpleValue => ReadSimple(reader),
            _ => throw new CborContentException($"Unsupported CBOR state: {state}"),
        };
    }

    private static SurrealNullValue ReadNull(CborReader reader)
    {
        reader.ReadNull();
        return SurrealValue.Null;
    }

    private static SurrealNumberValue ReadUnsigned(CborReader reader)
    {
        // CBOR unsigned can exceed long.MaxValue; fall back to ulong-as-signed via cast.
        var u = reader.ReadUInt64();
        return u <= long.MaxValue
            ? new SurrealNumberValue(SurrealNumber.FromInt((long)u))
            : new SurrealNumberValue(SurrealNumber.FromFloat(u)); // lossy, but rare; matches Rust's i128-as-i64 cast
    }

    private static SurrealValue ReadSimple(CborReader reader)
    {
        var simple = reader.ReadSimpleValue();
        return simple switch
        {
            CborSimpleValue.False => new SurrealBoolValue(false),
            CborSimpleValue.True => new SurrealBoolValue(true),
            CborSimpleValue.Null => SurrealValue.Null,
            CborSimpleValue.Undefined => SurrealValue.None,
            _ => throw new CborContentException($"Unsupported simple value: {simple}"),
        };
    }

    private static SurrealValue ReadTagged(CborReader reader, ulong tag)
    {
        reader.ReadTag();
        return tag switch
        {
            SurrealCborTags.None => ReadNoneTag(reader),
            SurrealCborTags.Table => new SurrealTableValue(new SurrealTable(reader.ReadTextString())),
            SurrealCborTags.RecordId => ReadRecordId(reader),
            SurrealCborTags.SpecDatetime => ReadSpecDateTime(reader),
            SurrealCborTags.CustomDatetime => ReadCustomDateTime(reader),
            SurrealCborTags.StringUuid => new SurrealUuidValue(Guid.Parse(reader.ReadTextString())),
            SurrealCborTags.SpecUuid => ReadSpecUuid(reader),
            SurrealCborTags.StringDecimal => new SurrealNumberValue(
                SurrealNumber.FromDecimal(decimal.Parse(reader.ReadTextString(), CultureInfo.InvariantCulture))),
            SurrealCborTags.StringDuration => new SurrealDurationValue(ParseDurationText(reader.ReadTextString())),
            SurrealCborTags.CustomDuration => ReadCustomDuration(reader),
            SurrealCborTags.Set => ReadSet(reader),
            SurrealCborTags.File => ReadFile(reader),
            SurrealCborTags.Range => new SurrealRangeValue(ReadRange(reader)),
            SurrealCborTags.GeometryPoint => new SurrealGeometryValue(ReadGeometryPoint(reader)),
            SurrealCborTags.GeometryLine => new SurrealGeometryValue(ReadGeometryLine(reader)),
            SurrealCborTags.GeometryPolygon => new SurrealGeometryValue(ReadGeometryPolygon(reader)),
            SurrealCborTags.GeometryMultiPoint => new SurrealGeometryValue(ReadGeometryMultiPoint(reader)),
            SurrealCborTags.GeometryMultiLine => new SurrealGeometryValue(ReadGeometryMultiLine(reader)),
            SurrealCborTags.GeometryMultiPolygon => new SurrealGeometryValue(ReadGeometryMultiPolygon(reader)),
            SurrealCborTags.GeometryCollection => new SurrealGeometryValue(ReadGeometryCollection(reader)),
            _ => throw new CborContentException($"Unrecognized SurrealDB CBOR tag: {tag}"),
        };
    }

    private static SurrealNoneValue ReadNoneTag(CborReader reader)
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
        return SurrealValue.None;
    }

    private static SurrealDateTimeValue ReadSpecDateTime(CborReader reader)
    {
        var text = reader.ReadTextString();
        var dto = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        return new SurrealDateTimeValue(new SurrealDateTime(dto));
    }

    private static SurrealDateTimeValue ReadCustomDateTime(CborReader reader)
    {
        reader.ReadStartArray();
        var seconds = reader.ReadInt64();
        var nanos = reader.ReadUInt32();
        reader.ReadEndArray();
        return new SurrealDateTimeValue(new SurrealDateTime(seconds, nanos));
    }

    private static SurrealDurationValue ReadCustomDuration(CborReader reader)
    {
        var len = reader.ReadStartArray();
        ulong seconds = 0;
        uint nanos = 0;
        if (len is > 0) seconds = reader.ReadUInt64();
        if (len is > 1) nanos = reader.ReadUInt32();
        reader.ReadEndArray();
        return new SurrealDurationValue(new SurrealDuration(seconds, nanos));
    }

    private static SurrealUuidValue ReadSpecUuid(CborReader reader)
    {
        var bytes = reader.ReadByteString();
        if (bytes.Length != 16)
            throw new CborContentException($"UUID byte string must be 16 bytes (got {bytes.Length}).");
        return new SurrealUuidValue(new Guid(bytes, bigEndian: true));
    }

    private static SurrealRecordIdValue ReadRecordId(CborReader reader)
    {
        var state = reader.PeekState();
        if (state == CborReaderState.TextString)
        {
            return new SurrealRecordIdValue(SurrealRecordId.ParseSimple(reader.ReadTextString()));
        }
        if (state != CborReaderState.StartArray)
            throw new CborContentException($"RecordId payload must be text or array; got {state}.");

        reader.ReadStartArray();
        var tableValue = Read(reader);
        var keyValue = Read(reader);
        reader.ReadEndArray();

        if (tableValue is not StringSurrealValue tableStr)
            throw new CborContentException("RecordId table component must be a string.");
        var key = ToRecordIdKey(keyValue);
        return new SurrealRecordIdValue(new SurrealRecordId(new SurrealTable(tableStr.Value), key));
    }

    private static SurrealRecordIdKey ToRecordIdKey(SurrealValue v) => v switch
    {
        StringSurrealValue s => new SurrealStringRecordIdKey(s.Value),
        SurrealNumberValue { SurrealNumber.Kind: SurrealNumberKind.Int } n => new SurrealIntegerRecordIdKey(n.SurrealNumber.AsInt()),
        SurrealUuidValue u => new SurrealUuidRecordIdKey(u.Value),
        SurrealListValue a => new SurrealListRecordIdKey(a.List),
        SurrealObjectValue o => new SurrealObjectRecordIdKey(o.Object),
        // A Range value inside a RecordId key position is a RangeRecordIdKey.
        // Re-walk the inner range so its bounds are typed as RecordIdKey, not Value.
        SurrealRangeValue r => new SurrealRangeRecordIdKey(ConvertRangeToRecordIdKeyRange(r.Range)),
        _ => throw new CborContentException($"Unsupported RecordId key kind: {v.Kind}"),
    };

    private static RecordIdKeyRange ConvertRangeToRecordIdKeyRange(SurrealRange range) =>
        new(ConvertBound(range.Start), ConvertBound(range.End));

    private static SurrealBound<SurrealRecordIdKey> ConvertBound(SurrealBound<SurrealValue> surrealBound) => surrealBound switch
    {
        SurrealBound<SurrealValue>.Included i => new SurrealBound<SurrealRecordIdKey>.Included(ToRecordIdKey(i.Value)),
        SurrealBound<SurrealValue>.Excluded e => new SurrealBound<SurrealRecordIdKey>.Excluded(ToRecordIdKey(e.Value)),
        SurrealBound<SurrealValue>.Unbounded => SurrealBound<SurrealRecordIdKey>.Unbounded.Instance,
        _ => throw new CborContentException($"Unhandled Bound: {surrealBound}"),
    };

    private static SurrealListValue ReadList(CborReader reader)
    {
        var len = reader.ReadStartArray();
        var arr = len.HasValue ? new SurrealList(len.Value) : new SurrealList();
        while (reader.PeekState() != CborReaderState.EndArray)
            arr.Add(Read(reader));
        reader.ReadEndArray();
        return new SurrealListValue(arr);
    }

    private static SurrealSetValue ReadSet(CborReader reader)
    {
        var len = reader.ReadStartArray();
        var set = len.HasValue ? new SurrealSet(len.Value) : new SurrealSet();
        while (reader.PeekState() != CborReaderState.EndArray)
            set.Add(Read(reader));
        reader.ReadEndArray();
        return new SurrealSetValue(set);
    }

    private static SurrealFileValue ReadFile(CborReader reader)
    {
        var len = reader.ReadStartArray();
        if (len != 2)
            throw new CborContentException($"File payload must be an array of 2 strings; got length {len}.");
        var bucket = reader.ReadTextString();
        var key = reader.ReadTextString();
        reader.ReadEndArray();
        return new SurrealFileValue(new SurrealFile(bucket, key));
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

    private static SurrealBound<SurrealValue> ReadBound(CborReader reader)
    {
        var state = reader.PeekState();
        if (state == CborReaderState.Null)
        {
            reader.ReadNull();
            return SurrealBound<SurrealValue>.Unbounded.Instance;
        }
        if (state != CborReaderState.Tag)
            throw new CborContentException($"Bound payload must be a tagged value or null; got {state}.");
        var tag = (ulong)reader.PeekTag();
        return tag switch
        {
            SurrealCborTags.BoundIncluded => Inner(reader, true),
            SurrealCborTags.BoundExcluded => Inner(reader, false),
            _ => throw new CborContentException($"Unexpected CBOR tag in Bound position: {tag}"),
        };

        static SurrealBound<SurrealValue> Inner(CborReader reader, bool included)
        {
            reader.ReadTag();
            var v = Read(reader);
            return included ? new SurrealBound<SurrealValue>.Included(v) : new SurrealBound<SurrealValue>.Excluded(v);
        }
    }

    // ─── Geometry decoders ────────────────────────────────────────────────────
    // The tag has already been read by ReadTagged before each entry point.

    private static SurrealGeometry.Point ReadGeometryPoint(CborReader reader)
    {
        var len = reader.ReadStartArray();
        if (len != 2)
            throw new CborContentException($"Geometry Point payload must be array of 2; got length {len}.");
        var x = reader.ReadDouble();
        var y = reader.ReadDouble();
        reader.ReadEndArray();
        return new SurrealGeometry.Point(x, y);
    }

    private static SurrealGeometry.Line ReadGeometryLine(CborReader reader)
    {
        reader.ReadStartArray();
        var pts = new List<SurrealGeometry.Point>();
        while (reader.PeekState() != CborReaderState.EndArray)
            pts.Add(ReadInnerGeometry(reader) as SurrealGeometry.Point ?? throw new CborContentException("Geometry Line elements must be Points."));
        reader.ReadEndArray();
        return new SurrealGeometry.Line(pts);
    }

    private static SurrealGeometry.Polygon ReadGeometryPolygon(CborReader reader)
    {
        reader.ReadStartArray();
        var lines = new List<SurrealGeometry.Line>();
        while (reader.PeekState() != CborReaderState.EndArray)
            lines.Add(ReadInnerGeometry(reader) as SurrealGeometry.Line ?? throw new CborContentException("Geometry Polygon elements must be Lines."));
        reader.ReadEndArray();
        if (lines.Count == 0)
            throw new CborContentException("Geometry Polygon must have at least an exterior ring.");
        return new SurrealGeometry.Polygon(lines[0], lines.Skip(1));
    }

    private static SurrealGeometry.MultiPoint ReadGeometryMultiPoint(CborReader reader)
    {
        reader.ReadStartArray();
        var pts = new List<SurrealGeometry.Point>();
        while (reader.PeekState() != CborReaderState.EndArray)
            pts.Add(ReadInnerGeometry(reader) as SurrealGeometry.Point ?? throw new CborContentException("MultiPoint elements must be Points."));
        reader.ReadEndArray();
        return new SurrealGeometry.MultiPoint(pts);
    }

    private static SurrealGeometry.MultiLine ReadGeometryMultiLine(CborReader reader)
    {
        reader.ReadStartArray();
        var lines = new List<SurrealGeometry.Line>();
        while (reader.PeekState() != CborReaderState.EndArray)
            lines.Add(ReadInnerGeometry(reader) as SurrealGeometry.Line ?? throw new CborContentException("MultiLine elements must be Lines."));
        reader.ReadEndArray();
        return new SurrealGeometry.MultiLine(lines);
    }

    private static SurrealGeometry.MultiPolygon ReadGeometryMultiPolygon(CborReader reader)
    {
        reader.ReadStartArray();
        var polys = new List<SurrealGeometry.Polygon>();
        while (reader.PeekState() != CborReaderState.EndArray)
            polys.Add(ReadInnerGeometry(reader) as SurrealGeometry.Polygon ?? throw new CborContentException("MultiPolygon elements must be Polygons."));
        reader.ReadEndArray();
        return new SurrealGeometry.MultiPolygon(polys);
    }

    private static SurrealGeometry.Collection ReadGeometryCollection(CborReader reader)
    {
        reader.ReadStartArray();
        var items = new List<SurrealGeometry>();
        while (reader.PeekState() != CborReaderState.EndArray)
            items.Add(ReadInnerGeometry(reader));
        reader.ReadEndArray();
        return new SurrealGeometry.Collection(items);
    }

    /// <summary>Reads a tagged geometry payload, returning the bare <see cref="SurrealGeometry"/>.</summary>
    private static SurrealGeometry ReadInnerGeometry(CborReader reader)
    {
        if (reader.PeekState() != CborReaderState.Tag)
            throw new CborContentException(
                $"Expected a tagged Geometry value; got {reader.PeekState()}.");
        var tag = (ulong)reader.PeekTag();
        reader.ReadTag();
        return tag switch
        {
            SurrealCborTags.GeometryPoint => ReadGeometryPoint(reader),
            SurrealCborTags.GeometryLine => ReadGeometryLine(reader),
            SurrealCborTags.GeometryPolygon => ReadGeometryPolygon(reader),
            SurrealCborTags.GeometryMultiPoint => ReadGeometryMultiPoint(reader),
            SurrealCborTags.GeometryMultiLine => ReadGeometryMultiLine(reader),
            SurrealCborTags.GeometryMultiPolygon => ReadGeometryMultiPolygon(reader),
            SurrealCborTags.GeometryCollection => ReadGeometryCollection(reader),
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

    private static SurrealBound<SurrealRecordIdKey> ReadRecordIdKeyBound(CborReader reader)
    {
        var state = reader.PeekState();
        if (state == CborReaderState.Null)
        {
            reader.ReadNull();
            return SurrealBound<SurrealRecordIdKey>.Unbounded.Instance;
        }
        if (state != CborReaderState.Tag)
            throw new CborContentException($"RecordIdKey bound must be tagged or null; got {state}.");
        var tag = (ulong)reader.PeekTag();
        return tag switch
        {
            SurrealCborTags.BoundIncluded => Inner(reader, true),
            SurrealCborTags.BoundExcluded => Inner(reader, false),
            _ => throw new CborContentException($"Unexpected CBOR tag in RecordIdKey-bound position: {tag}"),
        };

        static SurrealBound<SurrealRecordIdKey> Inner(CborReader reader, bool included)
        {
            reader.ReadTag();
            var v = Read(reader);
            var key = ToRecordIdKey(v);
            return included ? new SurrealBound<SurrealRecordIdKey>.Included(key) : new SurrealBound<SurrealRecordIdKey>.Excluded(key);
        }
    }

    private static SurrealObjectValue ReadObject(CborReader reader)
    {
        var len = reader.ReadStartMap();
        var obj = len.HasValue ? new SurrealObject(len.Value) : new SurrealObject();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            obj[key] = Read(reader);
        }
        reader.ReadEndMap();
        return new SurrealObjectValue(obj);
    }

    private static SurrealDuration ParseDurationText(string text)
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
        return new SurrealDuration(seconds, (uint)nanos);
    }
}
