using System.Collections;

namespace Disruptor.Surreal.Values;

/// <summary>
/// A list of <see cref="SurrealValue"/>s. Idiomatic .NET collection with structural equality.
/// Named with the <c>Surreal</c> prefix to avoid colliding with <see cref="System.Array"/>.
/// </summary>
public sealed class SurrealList : IList<SurrealValue>, IReadOnlyList<SurrealValue>, IEquatable<SurrealList>
{
    private readonly List<SurrealValue> items;

    public SurrealList() => items = [];
    public SurrealList(int capacity) => items = new(capacity);
    public SurrealList(IEnumerable<SurrealValue> items) => this.items = [..items];

    public SurrealValue this[int index]
    {
        get => items[index];
        set => items[index] = value;
    }

    public int Count => items.Count;
    public bool IsReadOnly => false;

    public void Add(SurrealValue item) => items.Add(item);
    public void Clear() => items.Clear();
    public bool Contains(SurrealValue item) => items.Contains(item);
    public void CopyTo(SurrealValue[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
    public IEnumerator<SurrealValue> GetEnumerator() => items.GetEnumerator();
    public int IndexOf(SurrealValue item) => items.IndexOf(item);
    public void Insert(int index, SurrealValue item) => items.Insert(index, item);
    public bool Remove(SurrealValue item) => items.Remove(item);
    public void RemoveAt(int index) => items.RemoveAt(index);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(SurrealList? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (items.Count != other.items.Count) return false;
        for (var i = 0; i < items.Count; i++)
            if (!EqualityComparer<SurrealValue>.Default.Equals(items[i], other.items[i]))
                return false;
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as SurrealList);

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
