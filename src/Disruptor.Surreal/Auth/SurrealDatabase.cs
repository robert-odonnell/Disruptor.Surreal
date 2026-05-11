using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Auth;

/// <summary>
/// Credentials for a database-scoped user. Sent to the server as
/// <c>{ "ns": ..., "db": ..., "user": ..., "pass": ... }</c>.
/// </summary>
public sealed record SurrealDatabase(string NS, string DB, string Username, string Password) : ISurrealCredentials
{
    /// <inheritdoc />
    public SurrealObject ToObject() => new()
    {
        ["ns"] = NS,
        ["db"] = DB,
        ["user"] = Username,
        ["pass"] = Password,
    };

    /// <summary>Redacted form — the auto-generated record ToString would leak the password.</summary>
    public override string ToString() =>
        $"SurrealDatabase {{ NS = {NS}, DB = {DB}, Username = {Username}, Password = ***** }}";
}
