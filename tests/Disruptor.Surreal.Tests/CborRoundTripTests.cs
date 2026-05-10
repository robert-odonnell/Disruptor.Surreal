using Disruptor.Surreal.Cbor;
using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class CborRoundTripTests
{
    [Fact]
    public void None_RoundTrips()
    {
        var bytes = CborValueWriter.Encode(Value.None);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(Value.None, decoded);
    }

    [Fact]
    public void Null_RoundTrips()
    {
        var bytes = CborValueWriter.Encode(Value.Null);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(Value.Null, decoded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bool_RoundTrips(bool b)
    {
        Value source = b;
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void Int_RoundTrips(long n)
    {
        Value source = n;
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(3.14159)]
    [InlineData(-2.71828)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void Float_RoundTrips(double d)
    {
        Value source = d;
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Decimal_RoundTrips()
    {
        Value source = 123.456789m;
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void String_RoundTrips()
    {
        Value source = "hello, 世界";
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Bytes_RoundTrips()
    {
        Value source = new BytesValue(new byte[] { 1, 2, 3, 255, 0, 128 });
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Datetime_RoundTrips_WithSubMicrosecondPrecision()
    {
        var datetime = new Datetime(seconds: 1_700_000_000, nanos: 123_456_789);
        Value source = new DatetimeValue(datetime);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
        Assert.Equal(datetime.Nanos, ((DatetimeValue)decoded).Datetime.Nanos);
    }

    [Fact]
    public void Duration_RoundTrips_WithNanos()
    {
        var dur = new Duration(seconds: 90, nanos: 500_000_000);
        Value source = new DurationValue(dur);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Uuid_RoundTrips()
    {
        Value source = Guid.Parse("8c5b1e4d-3f2a-4c6e-9e7f-1a2b3c4d5e6f");
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void RecordId_StringKey_RoundTrips()
    {
        Value source = new RecordId("person", "jaime");
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void RecordId_UlidKey_WritesAsCanonicalString()
    {
        // Ulid round-trips asymmetrically: writes as canonical text (the form SurrealDB
        // stores), reads back as StringRecordIdKey. Verify the string round-trip is
        // stable and matches the canonical form.
        var ulid = Ulid.NewUlid();
        Value source = new RecordId("person", ulid);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);

        var rid = Assert.IsType<RecordIdValue>(decoded).RecordId;
        Assert.Equal("person", rid.Table.Name);
        var key = Assert.IsType<StringRecordIdKey>(rid.Key);
        Assert.Equal(ulid.ToString(), key.Value);
        Assert.Equal(ulid, Ulid.Parse(key.Value));
    }

    [Fact]
    public void RecordId_IntegerKey_RoundTrips()
    {
        Value source = new RecordId("user", 42L);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Table_RoundTrips()
    {
        Value source = new Table("person");
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Array_RoundTrips()
    {
        var arr = new SurrealArray { 1L, "two", true, Value.None };
        Value source = new ArrayValue(arr);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Object_RoundTrips_PreservesInsertionOrder()
    {
        var obj = new SurrealObject { ["name"] = "Jaime", ["age"] = 30L, ["admin"] = true };
        Value source = new ObjectValue(obj);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);

        var roundtrippedKeys = ((ObjectValue)decoded).Object.Keys.ToList();
        Assert.Equal(new[] { "name", "age", "admin" }, roundtrippedKeys);
    }

    [Fact]
    public void Range_FullyBounded_RoundTrips()
    {
        var range = new SurrealRange(
            new Bound<Value>.Included(1L),
            new Bound<Value>.Excluded(10L));
        Value source = new RangeValue(range);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Range_Unbounded_RoundTrips()
    {
        Value source = new RangeValue(SurrealRange.Unbounded());
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Range_HalfOpen_RoundTrips()
    {
        var range = new SurrealRange(
            new Bound<Value>.Included(0L),
            Bound<Value>.Unbounded.Instance);
        Value source = new RangeValue(range);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void RecordId_WithRangeKey_RoundTrips()
    {
        // person:a..z — a range over string-keyed records on `person`.
        var rangeKey = new RangeRecordIdKey(new RecordIdKeyRange(
            new Bound<RecordIdKey>.Included(new StringRecordIdKey("a")),
            new Bound<RecordIdKey>.Excluded(new StringRecordIdKey("z"))));
        var rid = new RecordId(new Table("person"), rangeKey);
        Value source = new RecordIdValue(rid);

        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);

        var roundtripped = (RecordIdValue)decoded;
        var roundtrippedRange = Assert.IsType<RangeRecordIdKey>(roundtripped.RecordId.Key);
        Assert.IsType<Bound<RecordIdKey>.Included>(roundtrippedRange.Range.Start);
        Assert.IsType<Bound<RecordIdKey>.Excluded>(roundtrippedRange.Range.End);
    }

    [Fact]
    public void Geometry_Point_RoundTrips()
    {
        Value source = new GeometryValue(new Geometry.Point(1.5, -2.25));
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Geometry_Line_RoundTrips()
    {
        var line = new Geometry.Line(
        [
            new Geometry.Point(0, 0),
            new Geometry.Point(1, 1),
            new Geometry.Point(2, 0)
        ]
        );
        Value source = new GeometryValue(line);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Geometry_PolygonWithHole_RoundTrips()
    {
        var exterior = new Geometry.Line(
        [
            new Geometry.Point(0, 0), new Geometry.Point(10, 0),
            new Geometry.Point(10, 10), new Geometry.Point(0, 10),
            new Geometry.Point(0, 0)
        ]
        );
        var hole = new Geometry.Line(
        [
            new Geometry.Point(2, 2), new Geometry.Point(4, 2),
            new Geometry.Point(4, 4), new Geometry.Point(2, 4),
            new Geometry.Point(2, 2)
        ]
        );
        Value source = new GeometryValue(new Geometry.Polygon(exterior, [hole]));
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Geometry_MultiPoint_RoundTrips()
    {
        Value source = new GeometryValue(new Geometry.MultiPoint(
        [
            new Geometry.Point(1, 2), new Geometry.Point(3, 4)
        ]
        ));
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Geometry_Collection_HeterogeneousRoundTrips()
    {
        var collection = new Geometry.Collection(
        [
            new Geometry.Point(0, 0),
            new Geometry.Line([new Geometry.Point(0, 0), new Geometry.Point(1, 1)])
        ]
        );
        Value source = new GeometryValue(collection);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void File_RoundTrips()
    {
        Value source = new FileValue(new SurrealFile("avatars", "/users/jaime.png"));
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void File_AutoPrependsLeadingSlash()
    {
        var f = new SurrealFile("bucket", "no-slash.txt");
        Assert.Equal("/no-slash.txt", f.Key);
    }

    [Fact]
    public void Set_RoundTrips_Dedupes()
    {
        var set = new SurrealSet { 1L, 2L, 3L, 2L, 1L }; // duplicates collapse
        Assert.Equal(3, set.Count);
        Value source = new SetValue(set);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
        Assert.Equal(3, ((SetValue)decoded).Set.Count);
    }

    [Fact]
    public void Set_Empty_RoundTrips()
    {
        Value source = new SetValue([]);
        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void NestedComposite_RoundTrips()
    {
        var inner = new SurrealObject { ["x"] = 1L, ["y"] = 2.5 };
        var arr = new SurrealArray { new ObjectValue(inner), new RecordId("t", "k") };
        var outer = new SurrealObject { ["items"] = new ArrayValue(arr), ["ts"] = DateTimeOffset.UtcNow };
        Value source = new ObjectValue(outer);

        var bytes = CborValueWriter.Encode(source);
        var decoded = CborValueReader.Decode(bytes);

        Assert.Equal(source, decoded);
    }
}
