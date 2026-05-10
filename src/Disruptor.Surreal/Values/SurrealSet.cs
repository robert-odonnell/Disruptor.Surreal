using System.Collections;

namespace Disruptor.Surreal.Values;

/// <summary>
/// A unordered collection of unique <see cref="SurrealValue"/>s. Mirrors the Rust client's
/// <c>Set</c> (<c>types/src/value/set.rs</c>) at the API level — Rust uses
/// <c>BTreeSet</c>; we use <see cref="HashSet{T}"/> because <see cref="SurrealValue"/> doesn't
/// carry the cross-variant total ordering Rust relies on. Wire-side this doesn't
/// matter: CBOR tag 56 wraps an array of values and the server re-collects into
/// its own ordered set on decode.
/// </summary>
public sealed class SurrealSet : ISet<SurrealValue>, IReadOnlyCollection<SurrealValue>, IEquatable<SurrealSet>
{
    private readonly HashSet<SurrealValue> items;

    public SurrealSet() => items = [];
    public SurrealSet(int capacity) => items = new(capacity);
    public SurrealSet(IEnumerable<SurrealValue> items) => this.items = [..items];

    public int Count => items.Count;
    public bool IsReadOnly => false;

    public bool Add(SurrealValue item) => items.Add(item);
    void ICollection<SurrealValue>.Add(SurrealValue item) => items.Add(item);
    public bool Contains(SurrealValue item) => items.Contains(item);
    public bool Remove(SurrealValue item) => items.Remove(item);
    public void Clear() => items.Clear();
    public void CopyTo(SurrealValue[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
    public IEnumerator<SurrealValue> GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void ExceptWith(IEnumerable<SurrealValue> other) => items.ExceptWith(other);
    public void IntersectWith(IEnumerable<SurrealValue> other) => items.IntersectWith(other);
    public bool IsProperSubsetOf(IEnumerable<SurrealValue> other) => items.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<SurrealValue> other) => items.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<SurrealValue> other) => items.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<SurrealValue> other) => items.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<SurrealValue> other) => items.Overlaps(other);
    public bool SetEquals(IEnumerable<SurrealValue> other) => items.SetEquals(other);
    public void SymmetricExceptWith(IEnumerable<SurrealValue> other) => items.SymmetricExceptWith(other);
    public void UnionWith(IEnumerable<SurrealValue> other) => items.UnionWith(other);

    public bool Equals(SurrealSet? other) => other is not null && items.SetEquals(other.items);
    public override bool Equals(object? obj) => Equals(obj as SurrealSet);

    public override int GetHashCode()
    {
        // Order-independent — XOR of element hashes so two sets with the same
        // contents hash equal regardless of insertion order.
        var hash = 0;
        foreach (var item in items) hash ^= item.GetHashCode();
        return hash;
    }

    public override string ToString()
    {
        if (items.Count == 0) return "{,}";
        return "{" + string.Join(", ", items) + "}";
    }
}
