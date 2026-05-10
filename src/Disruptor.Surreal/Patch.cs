using Disruptor.Surreal.Values;

namespace Disruptor.Surreal;

/// <summary>
/// Factory helpers for JSON-Patch operations consumed by
/// <see cref="SurrealClient.PatchAsync(string, IEnumerable{SurrealObject}, CancellationToken)"/>
/// and friends. Each operation is a <see cref="SurrealObject"/> with the standard
/// <c>op</c> / <c>path</c> / <c>value</c> / <c>from</c> shape SurrealDB accepts.
/// </summary>
public static class Patch
{
    /// <summary>Add a value at <paramref name="path"/>.</summary>
    public static SurrealObject Add(string path, SurrealValue surrealValue) =>
        new() { ["op"] = "add", ["path"] = path, ["value"] = surrealValue };

    /// <summary>Remove the value at <paramref name="path"/>.</summary>
    public static SurrealObject Remove(string path) =>
        new() { ["op"] = "remove", ["path"] = path };

    /// <summary>Replace the value at <paramref name="path"/> with <paramref name="surrealValue"/>.</summary>
    public static SurrealObject Replace(string path, SurrealValue surrealValue) =>
        new() { ["op"] = "replace", ["path"] = path, ["value"] = surrealValue };

    /// <summary>Move the value from <paramref name="from"/> to <paramref name="path"/>.</summary>
    public static SurrealObject Move(string from, string path) =>
        new() { ["op"] = "move", ["from"] = from, ["path"] = path };

    /// <summary>Copy the value at <paramref name="from"/> to <paramref name="path"/>.</summary>
    public static SurrealObject Copy(string from, string path) =>
        new() { ["op"] = "copy", ["from"] = from, ["path"] = path };

    /// <summary>Assert that the value at <paramref name="path"/> equals <paramref name="surrealValue"/>.</summary>
    public static SurrealObject Test(string path, SurrealValue surrealValue) =>
        new() { ["op"] = "test", ["path"] = path, ["value"] = surrealValue };

    /// <summary>SurrealDB-specific: apply a unified-diff string at <paramref name="path"/>.</summary>
    public static SurrealObject Change(string path, string diff) =>
        new() { ["op"] = "change", ["path"] = path, ["value"] = diff };
}
