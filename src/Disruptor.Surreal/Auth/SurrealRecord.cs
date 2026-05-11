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
public sealed record SurrealRecord(string NS, string DB, string Access, SurrealObject Params) : ISurrealCredentials
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

    /// <summary>
    /// Redacted form — the auto-generated record ToString would leak whatever the
    /// caller put in <see cref="Params"/> (signup forms commonly include passwords or
    /// secrets). Frame is shown; params are redacted en bloc.
    /// </summary>
    public override string ToString() =>
        $"SurrealRecord {{ NS = {NS}, DB = {DB}, Access = {Access}, Params = ***** }}";
}
