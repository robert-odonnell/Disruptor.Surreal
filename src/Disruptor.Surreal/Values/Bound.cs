namespace Disruptor.Surreal.Values;

/// <summary>
/// One end of a range. Mirrors Rust's <c>std::ops::Bound&lt;T&gt;</c>:
/// <see cref="Included"/>, <see cref="Excluded"/>, or <see cref="Unbounded"/>.
/// </summary>
public abstract record Bound<T>
{
    private protected Bound() { }

    /// <summary>The bound includes the value at <paramref name="Value"/>.</summary>
    public sealed record Included(T Value) : Bound<T>;

    /// <summary>The bound excludes the value at <paramref name="Value"/>.</summary>
    public sealed record Excluded(T Value) : Bound<T>;

    /// <summary>No bound — open at this end of the range.</summary>
    public sealed record Unbounded : Bound<T>
    {
        /// <summary>Singleton instance.</summary>
        public static readonly Unbounded Instance = new();
        private Unbounded() { }
    }
}

/// <summary>
/// Non-generic ergonomic factories for <see cref="Bound{T}"/>. Lets callers construct
/// bounds without restating the generic type argument.
/// </summary>
public static class Bound
{
    /// <summary>Inclusive lower or upper bound.</summary>
    public static Bound<T>.Included Included<T>(T value) => new(value);

    /// <summary>Exclusive lower or upper bound.</summary>
    public static Bound<T>.Excluded Excluded<T>(T value) => new(value);

    /// <summary>Open / unbounded end.</summary>
    public static Bound<T>.Unbounded Unbounded<T>() => Bound<T>.Unbounded.Instance;
}
