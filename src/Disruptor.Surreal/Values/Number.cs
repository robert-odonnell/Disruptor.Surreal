using System.Globalization;

namespace Disruptor.Surreal.Values;

/// <summary>The numeric kind discriminator.</summary>
public enum NumberKind
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
/// to keep <see cref="NumberValue"/> allocation-free.
/// </summary>
public readonly struct Number : IEquatable<Number>
{
    private readonly long _intValue;
    private readonly double _floatValue;
    private readonly decimal _decimalValue;

    /// <summary>The active variant.</summary>
    public NumberKind Kind { get; }

    private Number(NumberKind kind, long i, double f, decimal d)
    {
        Kind = kind;
        _intValue = i;
        _floatValue = f;
        _decimalValue = d;
    }

    public static Number FromInt(long value) => new(NumberKind.Int, value, 0, 0);
    public static Number FromFloat(double value) => new(NumberKind.Float, 0, value, 0);
    public static Number FromDecimal(decimal value) => new(NumberKind.Decimal, 0, 0, value);

    /// <summary>The int64 value. Throws if <see cref="Kind"/> is not <see cref="NumberKind.Int"/>.</summary>
    public long AsInt() => Kind == NumberKind.Int ? _intValue
        : throw new InvalidOperationException($"Number is {Kind}, not Int");

    /// <summary>The float64 value. Throws if <see cref="Kind"/> is not <see cref="NumberKind.Float"/>.</summary>
    public double AsFloat() => Kind == NumberKind.Float ? _floatValue
        : throw new InvalidOperationException($"Number is {Kind}, not Float");

    /// <summary>The decimal value. Throws if <see cref="Kind"/> is not <see cref="NumberKind.Decimal"/>.</summary>
    public decimal AsDecimal() => Kind == NumberKind.Decimal ? _decimalValue
        : throw new InvalidOperationException($"Number is {Kind}, not Decimal");

    /// <summary>Returns the value as <see cref="double"/>, regardless of kind. Lossy for Decimal values out of range.</summary>
    public double ToDouble() => Kind switch
    {
        NumberKind.Int => _intValue,
        NumberKind.Float => _floatValue,
        NumberKind.Decimal => (double)_decimalValue,
        _ => throw new InvalidOperationException(),
    };

    public bool Equals(Number other)
    {
        if (Kind != other.Kind) return false;
        return Kind switch
        {
            NumberKind.Int => _intValue == other._intValue,
            NumberKind.Float => _floatValue.Equals(other._floatValue),
            NumberKind.Decimal => _decimalValue == other._decimalValue,
            _ => false,
        };
    }

    public override bool Equals(object? obj) => obj is Number n && Equals(n);

    public override int GetHashCode() => Kind switch
    {
        NumberKind.Int => HashCode.Combine(Kind, _intValue),
        NumberKind.Float => HashCode.Combine(Kind, _floatValue),
        NumberKind.Decimal => HashCode.Combine(Kind, _decimalValue),
        _ => 0,
    };

    public static bool operator ==(Number a, Number b) => a.Equals(b);
    public static bool operator !=(Number a, Number b) => !a.Equals(b);

    public override string ToString() => Kind switch
    {
        NumberKind.Int => _intValue.ToString(CultureInfo.InvariantCulture),
        NumberKind.Float => _floatValue.ToString("R", CultureInfo.InvariantCulture) + "f",
        NumberKind.Decimal => _decimalValue.ToString(CultureInfo.InvariantCulture) + "dec",
        _ => "?",
    };
}
