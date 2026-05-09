namespace Disruptor.Surreal.Values;

/// <summary>
/// A SurrealDB file reference: a (bucket, key) pair pointing into the server's
/// blob storage. Mirrors the Rust client's <c>File</c> (<c>types/src/value/file.rs</c>).
/// </summary>
/// <remarks>
/// The constructor auto-prepends <c>/</c> to <see cref="Key"/> when missing, matching
/// the Rust <c>File::new</c> contract: keys always start with <c>/</c>.
/// </remarks>
public sealed record SurrealFile
{
    /// <summary>The bucket name.</summary>
    public string Bucket { get; }

    /// <summary>The file key within the bucket; always starts with <c>/</c>.</summary>
    public string Key { get; }

    public SurrealFile(string bucket, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentNullException.ThrowIfNull(key);
        Bucket = bucket;
        Key = key.StartsWith('/') ? key : "/" + key;
    }

    public override string ToString() => $"f\"{Bucket}:{Key}\"";
}
