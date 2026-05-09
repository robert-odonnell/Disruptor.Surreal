using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Auth;

/// <summary>
/// Credentials for a record-scope (access-method) signin or signup. Carries the
/// namespace, database, and access-method name plus arbitrary additional fields
/// the access method's signin/signup query expects.
/// </summary>
/// <remarks>
/// Wire shape: <c>{ "ns": …, "db": …, "ac": …, …Params }</c> — the params are
/// flattened into the top-level credentials object, matching the official Rust
/// client's <c>Record&lt;P&gt;</c> serialization.
/// </remarks>
public sealed record Record(string NS, string DB, string Access, SurrealObject Params) : ICredentials
{
    /// <inheritdoc />
    public SurrealObject ToObject()
    {
        var obj = new SurrealObject
        {
            ["ns"] = NS,
            ["db"] = DB,
            ["ac"] = Access,
        };
        foreach (var (k, v) in Params)
        {
            // Reserved keys win for the credential frame, but anything else flows through.
            if (k is "ns" or "db" or "ac") continue;
            obj[k] = v;
        }
        return obj;
    }
}
