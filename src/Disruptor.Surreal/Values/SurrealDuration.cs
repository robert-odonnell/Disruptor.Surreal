namespace Disruptor.Surreal.Values;

/// <summary>
/// A duration with nanosecond precision.
/// </summary>
/// <remarks>
/// <see cref="TimeSpan"/> resolves to 100ns ticks; the extra <see cref="Nanos"/> field
/// holds the 0–99 sub-tick nanosecond remainder so wire round-trips don't lose precision.
/// </remarks>
public readonly struct SurrealDuration : IEquatable<SurrealDuration>, IComparable<SurrealDuration>
{
    /// <summary>Whole seconds.</summary>
    public ulong Seconds { get; }

    /// <summary>Nanoseconds within the second (0 ≤ Nanos &lt; 1_000_000_000).</summary>
    public uint Nanos { get; }

    public SurrealDuration(ulong seconds, uint nanos)
    {
        if (nanos >= 1_000_000_000u)
            throw new ArgumentOutOfRangeException(nameof(nanos), "Nanos must be < 1_000_000_000.");
        Seconds = seconds;
        Nanos = nanos;
    }

    public SurrealDuration(TimeSpan value)
    {
        if (value.Ticks < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Duration cannot be negative.");
        Seconds = (ulong)(value.Ticks / TimeSpan.TicksPerSecond);
        var subSecondTicks = value.Ticks - (long)Seconds * TimeSpan.TicksPerSecond;
        Nanos = (uint)(subSecondTicks * 100);
    }

    public TimeSpan ToTimeSpan()
    {
        var ticks = (long)Seconds * TimeSpan.TicksPerSecond + Nanos / 100;
        return TimeSpan.FromTicks(ticks);
    }

    public bool Equals(SurrealDuration other) => Seconds == other.Seconds && Nanos == other.Nanos;
    public override bool Equals(object? obj) => obj is SurrealDuration d && Equals(d);
    public override int GetHashCode() => HashCode.Combine(Seconds, Nanos);

    public int CompareTo(SurrealDuration other)
    {
        var c = Seconds.CompareTo(other.Seconds);
        return c != 0 ? c : Nanos.CompareTo(other.Nanos);
    }

    public static bool operator ==(SurrealDuration a, SurrealDuration b) => a.Equals(b);
    public static bool operator !=(SurrealDuration a, SurrealDuration b) => !a.Equals(b);

    public static implicit operator SurrealDuration(TimeSpan value) => new(value);
    public static implicit operator TimeSpan(SurrealDuration value) => value.ToTimeSpan();

    public override string ToString()
    {
        if (Seconds == 0 && Nanos == 0) return "0ns";
        var sb = new System.Text.StringBuilder();
        var s = Seconds;
        if (s >= 86_400) { sb.Append(s / 86_400).Append('d'); s %= 86_400; }
        if (s >= 3_600)  { sb.Append(s / 3_600).Append('h'); s %= 3_600; }
        if (s >= 60)     { sb.Append(s / 60).Append('m'); s %= 60; }
        if (s > 0)       sb.Append(s).Append('s');
        if (Nanos > 0)
        {
            if (Nanos % 1_000_000 == 0) sb.Append(Nanos / 1_000_000).Append("ms");
            else if (Nanos % 1_000 == 0) sb.Append(Nanos / 1_000).Append("us");
            else sb.Append(Nanos).Append("ns");
        }
        return sb.ToString();
    }
}
