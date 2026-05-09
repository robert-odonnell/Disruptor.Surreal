using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Auth;

/// <summary>Marker interface for credential payloads that can be signed in.</summary>
public interface ICredentials
{
    /// <summary>Renders the credentials to the wire-level <see cref="Object"/>.</summary>
    SurrealObject ToObject();
}

/// <summary>
/// Credentials for the root SurrealDB user. Sent to the server as
/// <c>{ "user": ..., "pass": ... }</c>.
/// </summary>
public sealed record Root(string Username, string Password) : ICredentials
{
    /// <inheritdoc />
    public SurrealObject ToObject() => new()
    {
        ["user"] = Username,
        ["pass"] = Password,
    };
}

/// <summary>
/// An access token returned by signin/signup/authenticate. The string is redacted
/// in <see cref="ToString"/>; access it via <see cref="Token"/> when you need to send it.
/// </summary>
public sealed class AccessToken
{
    /// <summary>The raw bearer token. Treat as a secret.</summary>
    public string Token { get; }

    public AccessToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        Token = token;
    }

    /// <summary>Returns a redacted placeholder. Use <see cref="Token"/> to access the value.</summary>
    public override string ToString() => "AccessToken(REDACTED)";

    public static implicit operator string(AccessToken t) => t.Token;
}
