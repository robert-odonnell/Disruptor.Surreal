using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Auth;

/// <summary>
/// Credentials for a namespace-scoped user. Sent to the server as
/// <c>{ "ns": ..., "user": ..., "pass": ... }</c>.
/// </summary>
public sealed record SurrealNamespace(string NS, string Username, string Password) : ISurrealCredentials
{
    /// <inheritdoc />
    public SurrealObject ToObject() => new()
    {
        ["ns"] = NS,
        ["user"] = Username,
        ["pass"] = Password,
    };

    /// <summary>Redacted form — the auto-generated record ToString would leak the password.</summary>
    public override string ToString() =>
        $"SurrealNamespace {{ NS = {NS}, Username = {Username}, Password = ***** }}";
}
