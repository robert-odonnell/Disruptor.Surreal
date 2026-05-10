using System.Formats.Cbor;
using System.Globalization;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Cbor;

/// <summary>
/// Encodes the SurrealDB <see cref="SurrealValue"/> tree into CBOR using the wire-format tags
/// defined in <see cref="SurrealCborTags"/>.
/// </summary>
public static class SurrealCborValueWriter
{
    /// <summary>Encode a value to a fresh byte array.</summary>
    public static byte[] Encode(SurrealValue surrealValue)
    {
        var writer = new CborWriter(CborConformanceMode.Lax, convertIndefiniteLengthEncodings: false);
        Write(writer, surrealValue);
        return writer.Encode();
    }

    /// <summary>Encode a value into the given <see cref="CborWriter"/>.</summary>
    public static void Write(CborWriter writer, SurrealValue surrealValue)
    {
        switch (surrealValue)
        {
            case SurrealNoneValue:
                writer.WriteTag(SurrealCborTags.None.AsCborTag());
                writer.WriteNull();
                break;

            case SurrealNullValue:
                writer.WriteNull();
                break;

            case SurrealBoolValue b:
                writer.WriteBoolean(b.Value);
                break;

            case SurrealNumberValue { SurrealNumber: var n }:
                WriteNumber(writer, n);
                break;

            case StringSurrealValue s:
                writer.WriteTextString(s.Value);
                break;

            case SurrealBytesValue b:
                writer.WriteByteString(b.Value.Span);
                break;

            case SurrealDateTimeValue d:
                WriteDatetime(writer, d.SurrealDateTime);
                break;

            case SurrealDurationValue d:
                WriteDuration(writer, d.SurrealDuration);
                break;

            case SurrealUuidValue u:
                WriteUuid(writer, u.Value);
                break;

            case SurrealListValue arr:
                WriteArray(writer, arr.List);
                break;

            case SurrealObjectValue obj:
                WriteObject(writer, obj.Object);
                break;

            case SurrealRecordIdValue r:
                WriteRecordId(writer, r.SurrealRecordId);
                break;

            case SurrealTableValue t:
                writer.WriteTag(SurrealCborTags.Table.AsCborTag());
                writer.WriteTextString(t.SurrealTable.Name);
                break;

            case SurrealSetValue s:
                WriteSet(writer, s.Set);
                break;

            case SurrealFileValue f:
                WriteFile(writer, f.File);
                break;

            case SurrealRangeValue r:
                WriteRange(writer, r.Range);
                break;

            case SurrealGeometryValue g:
                WriteGeometry(writer, g.SurrealGeometry);
                break;

            default:
                throw new InvalidOperationException($"Unhandled Value variant: {surrealValue.Kind}");
        }
    }

    private static void WriteNumber(CborWriter writer, SurrealNumber n)
    {
        switch (n.Kind)
        {
            case SurrealNumberKind.Int:
                writer.WriteInt64(n.AsInt());
                break;
            case SurrealNumberKind.Float:
                writer.WriteDouble(n.AsFloat());
                break;
            case SurrealNumberKind.Decimal:
                writer.WriteTag(SurrealCborTags.StringDecimal.AsCborTag());
                writer.WriteTextString(n.AsDecimal().ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException($"Unknown NumberKind: {n.Kind}");
        }
    }

    private static void WriteDatetime(CborWriter writer, SurrealDateTime d)
    {
        writer.WriteTag(SurrealCborTags.CustomDatetime.AsCborTag());
        writer.WriteStartArray(2);
        writer.WriteInt64(d.Seconds);
        writer.WriteUInt32(d.Nanos);
        writer.WriteEndArray();
    }

    private static void WriteDuration(CborWriter writer, SurrealDuration d)
    {
        writer.WriteTag(SurrealCborTags.CustomDuration.AsCborTag());
        // Match Rust: omit trailing zero elements for a smaller payload.
        if (d.Seconds == 0 && d.Nanos == 0)
        {
            writer.WriteStartArray(0);
            writer.WriteEndArray();
        }
        else if (d.Nanos == 0)
        {
            writer.WriteStartArray(1);
            writer.WriteUInt64(d.Seconds);
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteStartArray(2);
            writer.WriteUInt64(d.Seconds);
            writer.WriteUInt32(d.Nanos);
            writer.WriteEndArray();
        }
    }

    private static void WriteUuid(CborWriter writer, Guid g)
    {
        Span<byte> buf = stackalloc byte[16];
        if (!g.TryWriteBytes(buf, bigEndian: true, out _))
            throw new InvalidOperationException("Guid serialization failed.");
        writer.WriteTag(SurrealCborTags.SpecUuid.AsCborTag());
        writer.WriteByteString(buf);
    }

    private static void WriteArray(CborWriter writer, SurrealList arr)
    {
        writer.WriteStartArray(arr.Count);
        foreach (var item in arr) Write(writer, item);
        writer.WriteEndArray();
    }

    private static void WriteSet(CborWriter writer, SurrealSet set)
    {
        writer.WriteTag(SurrealCborTags.Set.AsCborTag());
        writer.WriteStartArray(set.Count);
        foreach (var item in set) Write(writer, item);
        writer.WriteEndArray();
    }

    private static void WriteFile(CborWriter writer, SurrealFile file)
    {
        writer.WriteTag(SurrealCborTags.File.AsCborTag());
        writer.WriteStartArray(2);
        writer.WriteTextString(file.Bucket);
        writer.WriteTextString(file.Key);
        writer.WriteEndArray();
    }

    private static void WriteRange(CborWriter writer, SurrealRange range)
    {
        writer.WriteTag(SurrealCborTags.Range.AsCborTag());
        writer.WriteStartArray(2);
        WriteBound(writer, range.Start);
        WriteBound(writer, range.End);
        writer.WriteEndArray();
    }

    private static void WriteBound(CborWriter writer, SurrealBound<SurrealValue> surrealBound)
    {
        switch (surrealBound)
        {
            case SurrealBound<SurrealValue>.Included i:
                writer.WriteTag(SurrealCborTags.BoundIncluded.AsCborTag());
                Write(writer, i.Value);
                break;
            case SurrealBound<SurrealValue>.Excluded e:
                writer.WriteTag(SurrealCborTags.BoundExcluded.AsCborTag());
                Write(writer, e.Value);
                break;
            case SurrealBound<SurrealValue>.Unbounded:
                writer.WriteNull();
                break;
            default:
                throw new InvalidOperationException($"Unhandled Bound<Value>: {surrealBound}");
        }
    }

    private static void WriteGeometry(CborWriter writer, SurrealGeometry g)
    {
        switch (g)
        {
            case SurrealGeometry.Point p:
                writer.WriteTag(SurrealCborTags.GeometryPoint.AsCborTag());
                writer.WriteStartArray(2);
                writer.WriteDouble(p.X);
                writer.WriteDouble(p.Y);
                writer.WriteEndArray();
                break;
            case SurrealGeometry.Line l:
                writer.WriteTag(SurrealCborTags.GeometryLine.AsCborTag());
                writer.WriteStartArray(l.Points.Count);
                foreach (var pt in l.Points) WriteGeometry(writer, pt);
                writer.WriteEndArray();
                break;
            case SurrealGeometry.Polygon p:
                writer.WriteTag(SurrealCborTags.GeometryPolygon.AsCborTag());
                writer.WriteStartArray(1 + p.Interiors.Count);
                WriteGeometry(writer, p.Exterior);
                foreach (var hole in p.Interiors) WriteGeometry(writer, hole);
                writer.WriteEndArray();
                break;
            case SurrealGeometry.MultiPoint mp:
                writer.WriteTag(SurrealCborTags.GeometryMultiPoint.AsCborTag());
                writer.WriteStartArray(mp.Points.Count);
                foreach (var pt in mp.Points) WriteGeometry(writer, pt);
                writer.WriteEndArray();
                break;
            case SurrealGeometry.MultiLine ml:
                writer.WriteTag(SurrealCborTags.GeometryMultiLine.AsCborTag());
                writer.WriteStartArray(ml.Lines.Count);
                foreach (var ln in ml.Lines) WriteGeometry(writer, ln);
                writer.WriteEndArray();
                break;
            case SurrealGeometry.MultiPolygon mpoly:
                writer.WriteTag(SurrealCborTags.GeometryMultiPolygon.AsCborTag());
                writer.WriteStartArray(mpoly.Polygons.Count);
                foreach (var poly in mpoly.Polygons) WriteGeometry(writer, poly);
                writer.WriteEndArray();
                break;
            case SurrealGeometry.Collection col:
                writer.WriteTag(SurrealCborTags.GeometryCollection.AsCborTag());
                writer.WriteStartArray(col.Items.Count);
                foreach (var item in col.Items) WriteGeometry(writer, item);
                writer.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException($"Unhandled Geometry: {g}");
        }
    }

    private static void WriteRecordIdKeyRange(CborWriter writer, RecordIdKeyRange range)
    {
        writer.WriteTag(SurrealCborTags.Range.AsCborTag());
        writer.WriteStartArray(2);
        WriteRecordIdKeyBound(writer, range.Start);
        WriteRecordIdKeyBound(writer, range.End);
        writer.WriteEndArray();
    }

    private static void WriteRecordIdKeyBound(CborWriter writer, SurrealBound<SurrealRecordIdKey> surrealBound)
    {
        switch (surrealBound)
        {
            case SurrealBound<SurrealRecordIdKey>.Included i:
                writer.WriteTag(SurrealCborTags.BoundIncluded.AsCborTag());
                WriteRecordIdKey(writer, i.Value);
                break;
            case SurrealBound<SurrealRecordIdKey>.Excluded e:
                writer.WriteTag(SurrealCborTags.BoundExcluded.AsCborTag());
                WriteRecordIdKey(writer, e.Value);
                break;
            case SurrealBound<SurrealRecordIdKey>.Unbounded:
                writer.WriteNull();
                break;
            default:
                throw new InvalidOperationException($"Unhandled Bound<RecordIdKey>: {surrealBound}");
        }
    }

    private static void WriteObject(CborWriter writer, SurrealObject obj)
    {
        writer.WriteStartMap(obj.Count);
        foreach (var (k, v) in obj)
        {
            writer.WriteTextString(k);
            Write(writer, v);
        }
        writer.WriteEndMap();
    }

    private static void WriteRecordId(CborWriter writer, SurrealRecordId id)
    {
        writer.WriteTag(SurrealCborTags.RecordId.AsCborTag());
        writer.WriteStartArray(2);
        writer.WriteTextString(id.Table.Name);
        WriteRecordIdKey(writer, id.Key);
        writer.WriteEndArray();
    }

    private static void WriteRecordIdKey(CborWriter writer, SurrealRecordIdKey key)
    {
        switch (key)
        {
            case SurrealStringRecordIdKey s:
                writer.WriteTextString(s.Value);
                break;
            case SurrealIntegerRecordIdKey i:
                writer.WriteInt64(i.Value);
                break;
            case SurrealUuidRecordIdKey u:
                WriteUuid(writer, u.Value);
                break;
            case SurrealUlidRecordIdKey u:
                // SurrealDB stores Ulid record ids as their canonical Crockford-base32
                // text form (e.g. person:01HXY...). Round-trip via string is asymmetric:
                // writes go out as Ulid, reads come back as StringRecordIdKey — call
                // Ulid.Parse on the string yourself if you know the shape.
                writer.WriteTextString(u.Value.ToString());
                break;
            case SurrealListRecordIdKey a:
                WriteArray(writer, a.Items);
                break;
            case SurrealObjectRecordIdKey o:
                WriteObject(writer, o.Fields);
                break;
            case SurrealRangeRecordIdKey r:
                WriteRecordIdKeyRange(writer, r.Range);
                break;
            default:
                throw new InvalidOperationException($"Unhandled RecordIdKey variant: {key.Kind}");
        }
    }
}
