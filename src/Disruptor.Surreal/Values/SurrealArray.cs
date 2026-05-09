using System.Collections;

namespace Disruptor.Surreal.Values;

/// <summary>
/// A list of <see cref="Value"/>s. Idiomatic .NET collection with structural equality.
/// Named with the <c>Surreal</c> prefix to avoid colliding with <see cref="System.Array"/>.
/// </summary>
public sealed class SurrealArray : IList<Value>, IReadOnlyList<Value>, IEquatable<SurrealArray>
{
    private readonly List<Value> items;

    public SurrealArray() => items = [];
    public SurrealArray(int capacity) => items = new(capacity);
    public SurrealArray(IEnumerable<Value> items) => this.items = [..items];

    public Value this[int index]
    {
        get => items[index];
        set => items[index] = value;
    }

    public int Count => items.Count;
    public bool IsReadOnly => false;

    public void Add(Value item) => items.Add(item);
    public void Clear() => items.Clear();
    public bool Contains(Value item) => items.Contains(item);
    public void CopyTo(Value[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
    public IEnumerator<Value> GetEnumerator() => items.GetEnumerator();
    public int IndexOf(Value item) => items.IndexOf(item);
    public void Insert(int index, Value item) => items.Insert(index, item);
    public bool Remove(Value item) => items.Remove(item);
    public void RemoveAt(int index) => items.RemoveAt(index);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(SurrealArray? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (items.Count != other.items.Count) return false;
        for (var i = 0; i < items.Count; i++)
            if (!EqualityComparer<Value>.Default.Equals(items[i], other.items[i]))
                return false;
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as SurrealArray);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in items) hash.Add(item);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        if (items.Count == 0) return "[]";
        return "[" + string.Join(", ", items) + "]";
    }
}
