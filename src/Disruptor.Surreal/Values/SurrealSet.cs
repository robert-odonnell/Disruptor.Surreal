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
    private readonly HashSet<Value> _items;

    public SurrealSet() => _items = new();
    public SurrealSet(int capacity) => _items = new(capacity);
    public SurrealSet(IEnumerable<Value> items) => _items = new(items);

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public bool Add(Value item) => _items.Add(item);
    void ICollection<Value>.Add(Value item) => _items.Add(item);
    public bool Contains(Value item) => _items.Contains(item);
    public bool Remove(Value item) => _items.Remove(item);
    public void Clear() => _items.Clear();
    public void CopyTo(Value[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<Value> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void ExceptWith(IEnumerable<Value> other) => _items.ExceptWith(other);
    public void IntersectWith(IEnumerable<Value> other) => _items.IntersectWith(other);
    public bool IsProperSubsetOf(IEnumerable<Value> other) => _items.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<Value> other) => _items.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<Value> other) => _items.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<Value> other) => _items.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<Value> other) => _items.Overlaps(other);
    public bool SetEquals(IEnumerable<Value> other) => _items.SetEquals(other);
    public void SymmetricExceptWith(IEnumerable<Value> other) => _items.SymmetricExceptWith(other);
    public void UnionWith(IEnumerable<Value> other) => _items.UnionWith(other);

    public bool Equals(SurrealSet? other) => other is not null && _items.SetEquals(other._items);
    public override bool Equals(object? obj) => Equals(obj as SurrealSet);

    public override int GetHashCode()
    {
        // Order-independent — XOR of element hashes so two sets with the same
        // contents hash equal regardless of insertion order.
        var hash = 0;
        foreach (var item in _items) hash ^= item.GetHashCode();
        return hash;
    }

    public override string ToString()
    {
        if (_items.Count == 0) return "{,}";
        return "{" + string.Join(", ", _items) + "}";
    }
}
