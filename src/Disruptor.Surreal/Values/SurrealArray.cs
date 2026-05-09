using System.Collections;

namespace Disruptor.Surreal.Values;

/// <summary>
/// A list of <see cref="Value"/>s. Idiomatic .NET collection with structural equality.
/// Named with the <c>Surreal</c> prefix to avoid colliding with <see cref="System.Array"/>.
/// </summary>
public sealed class SurrealArray : IList<Value>, IReadOnlyList<Value>, IEquatable<SurrealArray>
{
    private readonly List<Value> _items;

    public SurrealArray() => _items = new();
    public SurrealArray(int capacity) => _items = new(capacity);
    public SurrealArray(IEnumerable<Value> items) => _items = new(items);

    public Value this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(Value item) => _items.Add(item);
    public void Clear() => _items.Clear();
    public bool Contains(Value item) => _items.Contains(item);
    public void CopyTo(Value[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<Value> GetEnumerator() => _items.GetEnumerator();
    public int IndexOf(Value item) => _items.IndexOf(item);
    public void Insert(int index, Value item) => _items.Insert(index, item);
    public bool Remove(Value item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(SurrealArray? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_items.Count != other._items.Count) return false;
        for (var i = 0; i < _items.Count; i++)
            if (!EqualityComparer<Value>.Default.Equals(_items[i], other._items[i]))
                return false;
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as SurrealArray);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items) hash.Add(item);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        if (_items.Count == 0) return "[]";
        return "[" + string.Join(", ", _items) + "]";
    }
}
