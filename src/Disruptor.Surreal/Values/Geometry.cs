using System.Globalization;

namespace Disruptor.Surreal.Values;

/// <summary>
/// SurrealDB geometry primitives. Mirrors the Rust client's <c>Geometry</c> enum
/// (<c>types/src/value/geometry.rs</c>) at the API level — Rust uses
/// <c>geo_types</c>; we hand-roll minimal records to avoid pulling in a geometry
/// library as a dependency.
/// </summary>
/// <remarks>
/// Closed hierarchy via nested sealed records. Pattern-match on
/// <see cref="Geometry.Point"/> / <see cref="Geometry.Line"/> / etc.
/// </remarks>
public abstract record Geometry
{
    private protected Geometry() { }

    /// <summary>A 2D point (x, y).</summary>
    public sealed record Point(double X, double Y) : Geometry
    {
        public override string ToString() =>
            "(" + X.ToString("R", CultureInfo.InvariantCulture)
                + ", " + Y.ToString("R", CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>A polyline — an ordered sequence of points.</summary>
    public sealed record Line : Geometry
    {
        public IReadOnlyList<Point> Points { get; }

        public Line(IEnumerable<Point> points) =>
            Points = points as IReadOnlyList<Point> ?? points.ToList();

        public bool Equals(Line? other) =>
            other is not null && Points.SequenceEqual(other.Points);
        public override int GetHashCode()
        {
            var h = new HashCode();
            foreach (var p in Points) h.Add(p);
            return h.ToHashCode();
        }
        public override string ToString() => "[" + string.Join(", ", Points) + "]";
    }

    /// <summary>A polygon — exterior ring plus zero or more interior holes.</summary>
    public sealed record Polygon : Geometry
    {
        public Line Exterior { get; }
        public IReadOnlyList<Line> Interiors { get; }

        public Polygon(Line exterior, IEnumerable<Line>? interiors = null)
        {
            Exterior = exterior;
            Interiors = interiors is null
                ? []
                : interiors as IReadOnlyList<Line> ?? interiors.ToList();
        }

        public bool Equals(Polygon? other) =>
            other is not null && Exterior.Equals(other.Exterior) && Interiors.SequenceEqual(other.Interiors);
        public override int GetHashCode()
        {
            var h = new HashCode();
            h.Add(Exterior);
            foreach (var i in Interiors) h.Add(i);
            return h.ToHashCode();
        }
    }

    /// <summary>A collection of points.</summary>
    public sealed record MultiPoint : Geometry
    {
        public IReadOnlyList<Point> Points { get; }
        public MultiPoint(IEnumerable<Point> points) =>
            Points = points as IReadOnlyList<Point> ?? points.ToList();

        public bool Equals(MultiPoint? other) =>
            other is not null && Points.SequenceEqual(other.Points);
        public override int GetHashCode()
        {
            var h = new HashCode();
            foreach (var p in Points) h.Add(p);
            return h.ToHashCode();
        }
    }

    /// <summary>A collection of lines.</summary>
    public sealed record MultiLine : Geometry
    {
        public IReadOnlyList<Line> Lines { get; }
        public MultiLine(IEnumerable<Line> lines) =>
            Lines = lines as IReadOnlyList<Line> ?? lines.ToList();

        public bool Equals(MultiLine? other) =>
            other is not null && Lines.SequenceEqual(other.Lines);
        public override int GetHashCode()
        {
            var h = new HashCode();
            foreach (var l in Lines) h.Add(l);
            return h.ToHashCode();
        }
    }

    /// <summary>A collection of polygons.</summary>
    public sealed record MultiPolygon : Geometry
    {
        public IReadOnlyList<Polygon> Polygons { get; }
        public MultiPolygon(IEnumerable<Polygon> polygons) =>
            Polygons = polygons as IReadOnlyList<Polygon> ?? polygons.ToList();

        public bool Equals(MultiPolygon? other) =>
            other is not null && Polygons.SequenceEqual(other.Polygons);
        public override int GetHashCode()
        {
            var h = new HashCode();
            foreach (var p in Polygons) h.Add(p);
            return h.ToHashCode();
        }
    }

    /// <summary>A heterogeneous collection of geometries.</summary>
    public sealed record Collection : Geometry
    {
        public IReadOnlyList<Geometry> Items { get; }
        public Collection(IEnumerable<Geometry> items) =>
            Items = items as IReadOnlyList<Geometry> ?? items.ToList();

        public bool Equals(Collection? other) =>
            other is not null && Items.SequenceEqual(other.Items);
        public override int GetHashCode()
        {
            var h = new HashCode();
            foreach (var i in Items) h.Add(i);
            return h.ToHashCode();
        }
    }
}
