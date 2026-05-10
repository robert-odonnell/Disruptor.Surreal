using System.Formats.Cbor;

namespace Disruptor.Surreal.Cbor;

/// <summary>
/// SurrealDB CBOR tag numbers. Sourced from
/// <c>core/src/rpc/format/cbor/convert.rs</c> in the official Rust implementation.
/// </summary>
internal static class SurrealCborTags
{
    /// <summary>RFC 8949 §3.4.1 — text-form datetime.</summary>
    public const ulong SpecDatetime = 0;

    /// <summary>SurrealDB <c>NONE</c> sentinel (paired with a CBOR null payload).</summary>
    public const ulong None = 6;

    /// <summary>Table reference; payload is a text string with the table name.</summary>
    public const ulong Table = 7;

    /// <summary>Record id; payload is either a text <c>"table:key"</c> or an array <c>[table, key]</c>.</summary>
    public const ulong RecordId = 8;

    /// <summary>UUID expressed as a textual form (8-4-4-4-12).</summary>
    public const ulong StringUuid = 9;

    /// <summary>Decimal expressed as its canonical text form.</summary>
    public const ulong StringDecimal = 10;

    /// <summary>Custom datetime; payload is an array <c>[seconds: i64, nanos: u32]</c>.</summary>
    public const ulong CustomDatetime = 12;

    /// <summary>Duration expressed as text (e.g. <c>"1h30m"</c>).</summary>
    public const ulong StringDuration = 13;

    /// <summary>Custom duration; payload is an array <c>[seconds?: u64, nanos?: u32]</c>.</summary>
    public const ulong CustomDuration = 14;

    /// <summary>RFC 8949 §3.4.2 — UUID expressed as 16 raw bytes.</summary>
    public const ulong SpecUuid = 37;

    /// <summary>Range; payload is a CBOR array <c>[start_bound, end_bound]</c>.</summary>
    public const ulong Range = 49;

    /// <summary>Inclusive range bound; payload is the bound value.</summary>
    public const ulong BoundIncluded = 50;

    /// <summary>Exclusive range bound; payload is the bound value.</summary>
    public const ulong BoundExcluded = 51;

    /// <summary>File reference; payload is an array <c>[bucket, key]</c> of strings.</summary>
    public const ulong File = 55;

    /// <summary>Set; payload is an array of values (uniqueness enforced by the consumer).</summary>
    public const ulong Set = 56;

    /// <summary>Geometry Point; payload is array <c>[x: float, y: float]</c>.</summary>
    public const ulong GeometryPoint = 88;

    /// <summary>Geometry Line (polyline); payload is array of <see cref="GeometryPoint"/>-tagged values.</summary>
    public const ulong GeometryLine = 89;

    /// <summary>Geometry Polygon; payload is array of <see cref="GeometryLine"/>-tagged values (first is exterior, rest are holes).</summary>
    public const ulong GeometryPolygon = 90;

    /// <summary>Geometry MultiPoint; payload is array of <see cref="GeometryPoint"/>-tagged values.</summary>
    public const ulong GeometryMultiPoint = 91;

    /// <summary>Geometry MultiLine; payload is array of <see cref="GeometryLine"/>-tagged values.</summary>
    public const ulong GeometryMultiLine = 92;

    /// <summary>Geometry MultiPolygon; payload is array of <see cref="GeometryPolygon"/>-tagged values.</summary>
    public const ulong GeometryMultiPolygon = 93;

    /// <summary>Geometry Collection; payload is heterogeneous array of geometry-tagged values.</summary>
    public const ulong GeometryCollection = 94;

    /// <summary>Returns the <see cref="CborTag"/> equivalent.</summary>
    public static CborTag AsCborTag(this ulong tag) => (CborTag)tag;
}
