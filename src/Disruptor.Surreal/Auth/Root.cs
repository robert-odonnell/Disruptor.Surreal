using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Auth;

/// <summary>Marker interface for credential payloads that can be signed in.</summary>
public interface ICredentials
{
    /// <summary>Renders the credentials to the wire-level <see cref="SurrealObject"/>.</summary>
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
/// A JWT access token issued by SurrealDB. Wraps the bearer string with a redacted
/// <see cref="ToString"/> so it doesn't appear in logs by accident.
/// </summary>
/// <remarks>
/// Mirrors the Rust client's <c>opt::auth::AccessToken</c> — same secure-string
/// pattern, same <c>as_insecure_token()</c> method to escape the wrapper when you
/// need to send it.
/// </remarks>
public sealed class AccessToken
{
    private readonly string _value;

    public AccessToken(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _value = value;
    }

    /// <summary>Returns the bearer string. Treat as a secret.</summary>
    public string AsInsecureToken() => _value;

    /// <summary>Returns a redacted placeholder. Use <see cref="AsInsecureToken"/> to access the value.</summary>
    public override string ToString() => "AccessToken(REDACTED)";

    /// <summary>Implicit conversion to string for callers that need the bearer value directly.</summary>
    public static implicit operator string(AccessToken t) => t._value;
}
