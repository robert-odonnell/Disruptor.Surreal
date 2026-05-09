using System.Globalization;

namespace Disruptor.Surreal.Values;

/// <summary>
/// A datetime with full nanosecond precision.
/// </summary>
/// <remarks>
/// SurrealDB's wire format encodes datetimes as <c>(seconds: i64, nanos: u32)</c> from the
/// Unix epoch. <see cref="DateTimeOffset"/> only resolves to 100ns ticks, so the extra
/// sub-tick nanoseconds (0–99) are preserved separately to round-trip without loss.
/// </remarks>
public readonly struct Datetime : IEquatable<Datetime>, IComparable<Datetime>
{
    /// <summary>Seconds since Unix epoch.</summary>
    public long Seconds { get; }

    /// <summary>Nanoseconds within the second (0 ≤ Nanos &lt; 1_000_000_000).</summary>
    public uint Nanos { get; }

    public Datetime(long seconds, uint nanos)
    {
        if (nanos >= 1_000_000_000u)
            throw new ArgumentOutOfRangeException(nameof(nanos), "Nanos must be < 1_000_000_000.");
        Seconds = seconds;
        Nanos = nanos;
    }

    public Datetime(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var ticks = utc.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks; // 100ns ticks
        Seconds = ticks / TimeSpan.TicksPerSecond;
        var subSecondTicks = ticks - Seconds * TimeSpan.TicksPerSecond;
        Nanos = (uint)(subSecondTicks * 100);
    }

    /// <summary>The current UTC instant.</summary>
    public static Datetime UtcNow => new(DateTimeOffset.UtcNow);

    /// <summary>Truncates sub-tick nanoseconds when converting back to <see cref="DateTimeOffset"/>.</summary>
    public DateTimeOffset ToDateTimeOffset()
    {
        var ticks = Seconds * TimeSpan.TicksPerSecond + Nanos / 100;
        return DateTimeOffset.UnixEpoch.AddTicks(ticks);
    }

    public bool Equals(Datetime other) => Seconds == other.Seconds && Nanos == other.Nanos;
    public override bool Equals(object? obj) => obj is Datetime d && Equals(d);
    public override int GetHashCode() => HashCode.Combine(Seconds, Nanos);

    public int CompareTo(Datetime other)
    {
        var c = Seconds.CompareTo(other.Seconds);
        return c != 0 ? c : Nanos.CompareTo(other.Nanos);
    }

    public static bool operator ==(Datetime a, Datetime b) => a.Equals(b);
    public static bool operator !=(Datetime a, Datetime b) => !a.Equals(b);
    public static bool operator <(Datetime a, Datetime b) => a.CompareTo(b) < 0;
    public static bool operator >(Datetime a, Datetime b) => a.CompareTo(b) > 0;
    public static bool operator <=(Datetime a, Datetime b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Datetime a, Datetime b) => a.CompareTo(b) >= 0;

    public static implicit operator Datetime(DateTimeOffset value) => new(value);
    public static implicit operator DateTimeOffset(Datetime value) => value.ToDateTimeOffset();

    public override string ToString() =>
        ToDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:ss.fffffffK", CultureInfo.InvariantCulture);
}
