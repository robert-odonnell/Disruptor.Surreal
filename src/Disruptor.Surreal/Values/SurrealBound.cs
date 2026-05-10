namespace Disruptor.Surreal.Values;

/// <summary>
/// One end of a range. Mirrors Rust's <c>std::ops::Bound&lt;T&gt;</c>:
/// <see cref="Included"/>, <see cref="Excluded"/>, or <see cref="Unbounded"/>.
/// </summary>
public abstract record SurrealBound<T>
{
    private protected SurrealBound() { }

    /// <summary>The bound includes the value at <paramref name="Value"/>.</summary>
    public sealed record Included(T Value) : SurrealBound<T>;

    /// <summary>The bound excludes the value at <paramref name="Value"/>.</summary>
    public sealed record Excluded(T Value) : SurrealBound<T>;

    /// <summary>No bound — open at this end of the range.</summary>
    public sealed record Unbounded : SurrealBound<T>
    {
        /// <summary>Singleton instance.</summary>
        public static readonly Unbounded Instance = new();
        private Unbounded() { }
    }
}

/// <summary>
/// Non-generic ergonomic factories for <see cref="SurrealBound{T}"/>. Lets callers construct
/// bounds without restating the generic type argument.
/// </summary>
public static class SurrealBound
{
    /// <summary>Inclusive lower or upper bound.</summary>
    public static SurrealBound<T>.Included Included<T>(T value) => new(value);

    /// <summary>Exclusive lower or upper bound.</summary>
    public static SurrealBound<T>.Excluded Excluded<T>(T value) => new(value);

    /// <summary>Open / unbounded end.</summary>
    public static SurrealBound<T>.Unbounded Unbounded<T>() => SurrealBound<T>.Unbounded.Instance;
}
