using System.Collections;

namespace Disruptor.Surreal.Values;

/// <summary>
/// An ordered map of string keys to <see cref="Value"/>s. Insertion order is preserved
/// so wire round-trips are stable, matching the Rust client's <c>BTreeMap</c>-equivalent behavior.
/// Named with the <c>Surreal</c> prefix to avoid colliding with <see cref="System.Object"/>.
/// </summary>
public sealed class SurrealObject : IDictionary<string, Value>, IReadOnlyDictionary<string, Value>, IEquatable<SurrealObject>
{
    private readonly OrderedDictionary<string, Value> _entries;

    public SurrealObject() => _entries = new();
    public SurrealObject(int capacity) => _entries = new(capacity);
    public SurrealObject(IEnumerable<KeyValuePair<string, Value>> entries) : this()
    {
        foreach (var (k, v) in entries) _entries[k] = v;
    }

    public Value this[string key]
    {
        get => _entries[key];
        set => _entries[key] = value;
    }

    public ICollection<string> Keys => _entries.Keys;
    public ICollection<Value> Values => _entries.Values;
    IEnumerable<string> IReadOnlyDictionary<string, Value>.Keys => _entries.Keys;
    IEnumerable<Value> IReadOnlyDictionary<string, Value>.Values => _entries.Values;

    public int Count => _entries.Count;
    public bool IsReadOnly => false;

    public void Add(string key, Value value) => _entries.Add(key, value);
    public void Add(KeyValuePair<string, Value> item) => _entries.Add(item.Key, item.Value);
    public void Clear() => _entries.Clear();
    public bool Contains(KeyValuePair<string, Value> item) =>
        _entries.TryGetValue(item.Key, out var v) && EqualityComparer<Value>.Default.Equals(v, item.Value);
    public bool ContainsKey(string key) => _entries.ContainsKey(key);
    public void CopyTo(KeyValuePair<string, Value>[] array, int arrayIndex)
    {
        foreach (var kv in _entries) array[arrayIndex++] = kv;
    }
    public bool Remove(string key) => _entries.Remove(key);
    public bool Remove(KeyValuePair<string, Value> item) =>
        Contains(item) && _entries.Remove(item.Key);
    public bool TryGetValue(string key, out Value value) => _entries.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<string, Value>> GetEnumerator() => _entries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(SurrealObject? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_entries.Count != other._entries.Count) return false;
        foreach (var (k, v) in _entries)
        {
            if (!other._entries.TryGetValue(k, out var ov)) return false;
            if (!EqualityComparer<Value>.Default.Equals(v, ov)) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as SurrealObject);

    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var (k, v) in _entries)
            hash ^= HashCode.Combine(k, v);
        return hash;
    }

    public override string ToString()
    {
        if (_entries.Count == 0) return "{}";
        return "{ " + string.Join(", ", _entries.Select(kv => $"{kv.Key}: {kv.Value}")) + " }";
    }
}
