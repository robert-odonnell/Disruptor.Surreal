using Disruptor.Surreal.Cbor;
using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class CborRoundTripTests
{
    [Fact]
    public void None_RoundTrips()
    {
        var bytes = SurrealCborValueWriter.Encode(SurrealValue.None);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(SurrealValue.None, decoded);
    }

    [Fact]
    public void Null_RoundTrips()
    {
        var bytes = SurrealCborValueWriter.Encode(SurrealValue.Null);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(SurrealValue.Null, decoded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bool_RoundTrips(bool b)
    {
        SurrealValue source = b;
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
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
        SurrealValue source = n;
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
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
        SurrealValue source = d;
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Decimal_RoundTrips()
    {
        SurrealValue source = 123.456789m;
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void String_RoundTrips()
    {
        SurrealValue source = "hello, 世界";
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Bytes_RoundTrips()
    {
        SurrealValue source = new SurrealBytesValue(new byte[] { 1, 2, 3, 255, 0, 128 });
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Datetime_RoundTrips_WithSubMicrosecondPrecision()
    {
        var datetime = new SurrealDateTime(seconds: 1_700_000_000, nanos: 123_456_789);
        SurrealValue source = new SurrealDateTimeValue(datetime);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
        Assert.Equal(datetime.Nanos, ((SurrealDateTimeValue)decoded).SurrealDateTime.Nanos);
    }

    [Fact]
    public void Duration_RoundTrips_WithNanos()
    {
        var dur = new SurrealDuration(seconds: 90, nanos: 500_000_000);
        SurrealValue source = new SurrealDurationValue(dur);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Uuid_RoundTrips()
    {
        SurrealValue source = Guid.Parse("8c5b1e4d-3f2a-4c6e-9e7f-1a2b3c4d5e6f");
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void RecordId_StringKey_RoundTrips()
    {
        SurrealValue source = new SurrealRecordId("person", "jaime");
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void RecordId_IntegerKey_RoundTrips()
    {
        SurrealValue source = new SurrealRecordId("user", 42L);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Table_RoundTrips()
    {
        SurrealValue source = new SurrealTable("person");
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Array_RoundTrips()
    {
        var arr = new SurrealList { 1L, "two", true, SurrealValue.None };
        SurrealValue source = new SurrealListValue(arr);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Object_RoundTrips_PreservesInsertionOrder()
    {
        var obj = new SurrealObject { ["name"] = "Jaime", ["age"] = 30L, ["admin"] = true };
        SurrealValue source = new SurrealObjectValue(obj);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);

        var roundtrippedKeys = ((SurrealObjectValue)decoded).Object.Keys.ToList();
        Assert.Equal(new[] { "name", "age", "admin" }, roundtrippedKeys);
    }

    [Fact]
    public void Range_FullyBounded_RoundTrips()
    {
        var range = new SurrealRange(
            new SurrealBound<SurrealValue>.Included(1L),
            new SurrealBound<SurrealValue>.Excluded(10L));
        SurrealValue source = new SurrealRangeValue(range);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Range_Unbounded_RoundTrips()
    {
        SurrealValue source = new SurrealRangeValue(SurrealRange.Unbounded());
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Range_HalfOpen_RoundTrips()
    {
        var range = new SurrealRange(
            new SurrealBound<SurrealValue>.Included(0L),
            SurrealBound<SurrealValue>.Unbounded.Instance);
        SurrealValue source = new SurrealRangeValue(range);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void RecordId_WithRangeKey_RoundTrips()
    {
        // person:a..z — a range over string-keyed records on `person`.
        var rangeKey = new SurrealRangeRecordIdKey(new RecordIdKeyRange(
            new SurrealBound<SurrealRecordIdKey>.Included(new SurrealStringRecordIdKey("a")),
            new SurrealBound<SurrealRecordIdKey>.Excluded(new SurrealStringRecordIdKey("z"))));
        var rid = new SurrealRecordId(new SurrealTable("person"), rangeKey);
        SurrealValue source = new SurrealRecordIdValue(rid);

        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);

        var roundtripped = (SurrealRecordIdValue)decoded;
        var roundtrippedRange = Assert.IsType<SurrealRangeRecordIdKey>(roundtripped.SurrealRecordId.Key);
        Assert.IsType<SurrealBound<SurrealRecordIdKey>.Included>(roundtrippedRange.Range.Start);
        Assert.IsType<SurrealBound<SurrealRecordIdKey>.Excluded>(roundtrippedRange.Range.End);
    }

    [Fact]
    public void Geometry_Point_RoundTrips()
    {
        SurrealValue source = new SurrealGeometryValue(new SurrealGeometry.Point(1.5, -2.25));
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Geometry_Line_RoundTrips()
    {
        var line = new SurrealGeometry.Line(
        [
            new SurrealGeometry.Point(0, 0),
            new SurrealGeometry.Point(1, 1),
            new SurrealGeometry.Point(2, 0)
        ]
        );
        SurrealValue source = new SurrealGeometryValue(line);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Geometry_PolygonWithHole_RoundTrips()
    {
        var exterior = new SurrealGeometry.Line(
        [
            new SurrealGeometry.Point(0, 0), new SurrealGeometry.Point(10, 0),
            new SurrealGeometry.Point(10, 10), new SurrealGeometry.Point(0, 10),
            new SurrealGeometry.Point(0, 0)
        ]
        );
        var hole = new SurrealGeometry.Line(
        [
            new SurrealGeometry.Point(2, 2), new SurrealGeometry.Point(4, 2),
            new SurrealGeometry.Point(4, 4), new SurrealGeometry.Point(2, 4),
            new SurrealGeometry.Point(2, 2)
        ]
        );
        SurrealValue source = new SurrealGeometryValue(new SurrealGeometry.Polygon(exterior, [hole]));
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Geometry_MultiPoint_RoundTrips()
    {
        SurrealValue source = new SurrealGeometryValue(new SurrealGeometry.MultiPoint(
        [
            new SurrealGeometry.Point(1, 2), new SurrealGeometry.Point(3, 4)
        ]
        ));
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Geometry_Collection_HeterogeneousRoundTrips()
    {
        var collection = new SurrealGeometry.Collection(
        [
            new SurrealGeometry.Point(0, 0),
            new SurrealGeometry.Line([new SurrealGeometry.Point(0, 0), new SurrealGeometry.Point(1, 1)])
        ]
        );
        SurrealValue source = new SurrealGeometryValue(collection);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void File_RoundTrips()
    {
        SurrealValue source = new SurrealFileValue(new SurrealFile("avatars", "/users/jaime.png"));
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
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
        SurrealValue source = new SurrealSetValue(set);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
        Assert.Equal(3, ((SurrealSetValue)decoded).Set.Count);
    }

    [Fact]
    public void Set_Empty_RoundTrips()
    {
        SurrealValue source = new SurrealSetValue([]);
        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void NestedComposite_RoundTrips()
    {
        var inner = new SurrealObject { ["x"] = 1L, ["y"] = 2.5 };
        var arr = new SurrealList { new SurrealObjectValue(inner), new SurrealRecordId("t", "k") };
        var outer = new SurrealObject { ["items"] = new SurrealListValue(arr), ["ts"] = DateTimeOffset.UtcNow };
        SurrealValue source = new SurrealObjectValue(outer);

        var bytes = SurrealCborValueWriter.Encode(source);
        var decoded = SurrealCborValueReader.Decode(bytes);

        Assert.Equal(source, decoded);
    }
}
