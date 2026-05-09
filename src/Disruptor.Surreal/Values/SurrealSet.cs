using System.Collections;

namespace Disruptor.Surreal.Values;

/// <summary>
/// A unordered collection of unique <see cref="Value"/>s. Mirrors the Rust client's
/// <c>Set</c> (<c>types/src/value/set.rs</c>) at the API level — Rust uses
/// <c>BTreeSet</c>; we use <see cref="HashSet{T}"/> because <see cref="Value"/> doesn't
/// carry the cross-variant total ordering Rust relies on. Wire-side this doesn't
/// matter: CBOR tag 56 wraps an array of values and the server re-collects into
/// its own ordered set on decode.
/// </summary>
public sealed class SurrealSet : ISet<Value>, IReadOnlyCollection<Value>, IEquatable<SurrealSet>
{
    private readonly HashSet<Value> items;

    public SurrealSet() => items = [];
    public SurrealSet(int capacity) => items = new(capacity);
    public SurrealSet(IEnumerable<Value> items) => this.items = [..items];

    public int Count => items.Count;
    public bool IsReadOnly => false;

    public bool Add(Value item) => items.Add(item);
    void ICollection<Value>.Add(Value item) => items.Add(item);
    public bool Contains(Value item) => items.Contains(item);
    public bool Remove(Value item) => items.Remove(item);
    public void Clear() => items.Clear();
    public void CopyTo(Value[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
    public IEnumerator<Value> GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void ExceptWith(IEnumerable<Value> other) => items.ExceptWith(other);
    public void IntersectWith(IEnumerable<Value> other) => items.IntersectWith(other);
    public bool IsProperSubsetOf(IEnumerable<Value> other) => items.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<Value> other) => items.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<Value> other) => items.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<Value> other) => items.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<Value> other) => items.Overlaps(other);
    public bool SetEquals(IEnumerable<Value> other) => items.SetEquals(other);
    public void SymmetricExceptWith(IEnumerable<Value> other) => items.SymmetricExceptWith(other);
    public void UnionWith(IEnumerable<Value> other) => items.UnionWith(other);

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
