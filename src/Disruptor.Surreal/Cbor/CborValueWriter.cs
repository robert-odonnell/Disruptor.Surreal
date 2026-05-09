using System.Formats.Cbor;
using System.Globalization;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Cbor;

/// <summary>
/// Encodes the SurrealDB <see cref="Value"/> tree into CBOR using the wire-format tags
/// defined in <see cref="CborTags"/>.
/// </summary>
public static class CborValueWriter
{
    /// <summary>Encode a value to a fresh byte array.</summary>
    public static byte[] Encode(Value value)
    {
        var writer = new CborWriter(CborConformanceMode.Lax, convertIndefiniteLengthEncodings: false);
        Write(writer, value);
        return writer.Encode();
    }

    /// <summary>Encode a value into the given <see cref="CborWriter"/>.</summary>
    public static void Write(CborWriter writer, Value value)
    {
        switch (value)
        {
            case NoneValue:
                writer.WriteTag(CborTags.None.AsCborTag());
                writer.WriteNull();
                break;

            case NullValue:
                writer.WriteNull();
                break;

            case BoolValue b:
                writer.WriteBoolean(b.Value);
                break;

            case NumberValue { Number: var n }:
                WriteNumber(writer, n);
                break;

            case StringValue s:
                writer.WriteTextString(s.Value);
                break;

            case BytesValue b:
                writer.WriteByteString(b.Value.Span);
                break;

            case DatetimeValue d:
                WriteDatetime(writer, d.Datetime);
                break;

            case DurationValue d:
                WriteDuration(writer, d.Duration);
                break;

            case UuidValue u:
                WriteUuid(writer, u.Value);
                break;

            case ArrayValue arr:
                WriteArray(writer, arr.Array);
                break;

            case ObjectValue obj:
                WriteObject(writer, obj.Object);
                break;

            case RecordIdValue r:
                WriteRecordId(writer, r.RecordId);
                break;

            case TableValue t:
                writer.WriteTag(CborTags.Table.AsCborTag());
                writer.WriteTextString(t.Table.Name);
                break;

            case SetValue s:
                WriteSet(writer, s.Set);
                break;

            default:
                throw new InvalidOperationException($"Unhandled Value variant: {value.Kind}");
        }
    }

    private static void WriteNumber(CborWriter writer, Number n)
    {
        switch (n.Kind)
        {
            case NumberKind.Int:
                writer.WriteInt64(n.AsInt());
                break;
            case NumberKind.Float:
                writer.WriteDouble(n.AsFloat());
                break;
            case NumberKind.Decimal:
                writer.WriteTag(CborTags.StringDecimal.AsCborTag());
                writer.WriteTextString(n.AsDecimal().ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException($"Unknown NumberKind: {n.Kind}");
        }
    }

    private static void WriteDatetime(CborWriter writer, Datetime d)
    {
        writer.WriteTag(CborTags.CustomDatetime.AsCborTag());
        writer.WriteStartArray(2);
        writer.WriteInt64(d.Seconds);
        writer.WriteUInt32(d.Nanos);
        writer.WriteEndArray();
    }

    private static void WriteDuration(CborWriter writer, Duration d)
    {
        writer.WriteTag(CborTags.CustomDuration.AsCborTag());
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
        writer.WriteTag(CborTags.SpecUuid.AsCborTag());
        writer.WriteByteString(buf);
    }

    private static void WriteArray(CborWriter writer, SurrealArray arr)
    {
        writer.WriteStartArray(arr.Count);
        foreach (var item in arr) Write(writer, item);
        writer.WriteEndArray();
    }

    private static void WriteSet(CborWriter writer, SurrealSet set)
    {
        writer.WriteTag(CborTags.Set.AsCborTag());
        writer.WriteStartArray(set.Count);
        foreach (var item in set) Write(writer, item);
        writer.WriteEndArray();
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

    private static void WriteRecordId(CborWriter writer, RecordId id)
    {
        writer.WriteTag(CborTags.RecordId.AsCborTag());
        writer.WriteStartArray(2);
        writer.WriteTextString(id.Table.Name);
        WriteRecordIdKey(writer, id.Key);
        writer.WriteEndArray();
    }

    private static void WriteRecordIdKey(CborWriter writer, RecordIdKey key)
    {
        switch (key)
        {
            case StringRecordIdKey s:
                writer.WriteTextString(s.Value);
                break;
            case IntegerRecordIdKey i:
                writer.WriteInt64(i.Value);
                break;
            case UuidRecordIdKey u:
                WriteUuid(writer, u.Value);
                break;
            case ArrayRecordIdKey a:
                WriteArray(writer, a.Items);
                break;
            case ObjectRecordIdKey o:
                WriteObject(writer, o.Fields);
                break;
            default:
                throw new InvalidOperationException($"Unhandled RecordIdKey variant: {key.Kind}");
        }
    }
}
