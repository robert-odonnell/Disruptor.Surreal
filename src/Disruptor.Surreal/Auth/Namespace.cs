using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Auth;

/// <summary>
/// Credentials for a namespace-scoped user. Sent to the server as
/// <c>{ "ns": ..., "user": ..., "pass": ... }</c>.
/// </summary>
public sealed record Namespace(string NS, string Username, string Password) : ICredentials
{
    /// <inheritdoc />
    public SurrealObject ToObject() => new()
    {
        ["ns"] = NS,
        ["user"] = Username,
        ["pass"] = Password,
    };
}
