using System.Globalization;

namespace Disruptor.Surreal.Values;

/// <summary>The numeric kind discriminator.</summary>
public enum SurrealNumberKind
{
    /// <summary>64-bit signed integer.</summary>
    Int,
    /// <summary>IEEE-754 binary64.</summary>
    Float,
    /// <summary>128-bit fixed-point decimal.</summary>
    Decimal,
}

/// <summary>
/// A SurrealDB number — int64, float64, or decimal. Stored as a flat struct
/// to keep <see cref="SurrealNumberValue"/> allocation-free.
/// </summary>
public readonly struct SurrealNumber : IEquatable<SurrealNumber>
{
    private readonly long intValue;
    private readonly double floatValue;
    private readonly decimal decimalValue;

    /// <summary>The active variant.</summary>
    public SurrealNumberKind Kind { get; }

    private SurrealNumber(SurrealNumberKind kind, long i, double f, decimal d)
    {
        Kind = kind;
        intValue = i;
        floatValue = f;
        decimalValue = d;
    }

    public static SurrealNumber FromInt(long value) => new(SurrealNumberKind.Int, value, 0, 0);
    public static SurrealNumber FromFloat(double value) => new(SurrealNumberKind.Float, 0, value, 0);
    public static SurrealNumber FromDecimal(decimal value) => new(SurrealNumberKind.Decimal, 0, 0, value);

    /// <summary>The int64 value. Throws if <see cref="Kind"/> is not <see cref="SurrealNumberKind.Int"/>.</summary>
    public long AsInt() => Kind == SurrealNumberKind.Int ? intValue
        : throw new InvalidOperationException($"Number is {Kind}, not Int");

    /// <summary>The float64 value. Throws if <see cref="Kind"/> is not <see cref="SurrealNumberKind.Float"/>.</summary>
    public double AsFloat() => Kind == SurrealNumberKind.Float ? floatValue
        : throw new InvalidOperationException($"Number is {Kind}, not Float");

    /// <summary>The decimal value. Throws if <see cref="Kind"/> is not <see cref="SurrealNumberKind.Decimal"/>.</summary>
    public decimal AsDecimal() => Kind == SurrealNumberKind.Decimal ? decimalValue
        : throw new InvalidOperationException($"Number is {Kind}, not Decimal");

    /// <summary>Returns the value as <see cref="double"/>, regardless of kind. Lossy for Decimal values out of range.</summary>
    public double ToDouble() => Kind switch
    {
        SurrealNumberKind.Int => intValue,
        SurrealNumberKind.Float => floatValue,
        SurrealNumberKind.Decimal => (double)decimalValue,
        _ => throw new InvalidOperationException(),
    };

    public bool Equals(SurrealNumber other)
    {
        if (Kind != other.Kind) return false;
        return Kind switch
        {
            SurrealNumberKind.Int => intValue == other.intValue,
            SurrealNumberKind.Float => floatValue.Equals(other.floatValue),
            SurrealNumberKind.Decimal => decimalValue == other.decimalValue,
            _ => false,
        };
    }

    public override bool Equals(object? obj) => obj is SurrealNumber n && Equals(n);

    public override int GetHashCode() => Kind switch
    {
        SurrealNumberKind.Int => HashCode.Combine(Kind, intValue),
        SurrealNumberKind.Float => HashCode.Combine(Kind, floatValue),
        SurrealNumberKind.Decimal => HashCode.Combine(Kind, decimalValue),
        _ => 0,
    };

    public static bool operator ==(SurrealNumber a, SurrealNumber b) => a.Equals(b);
    public static bool operator !=(SurrealNumber a, SurrealNumber b) => !a.Equals(b);

    public override string ToString() => Kind switch
    {
        SurrealNumberKind.Int => intValue.ToString(CultureInfo.InvariantCulture),
        SurrealNumberKind.Float => floatValue.ToString("R", CultureInfo.InvariantCulture) + "f",
        SurrealNumberKind.Decimal => decimalValue.ToString(CultureInfo.InvariantCulture) + "dec",
        _ => "?",
    };
}
