namespace Disruptor.Surreal.Auth;

/// <summary>
/// A long-lived refresh token used to obtain new access tokens without re-authenticating.
/// </summary>
/// <remarks>
/// Mirrors the Rust client's <c>opt::auth::RefreshToken</c> — same secure-string
/// pattern as <see cref="AccessToken"/>, same <c>as_insecure_token()</c> escape hatch.
/// </remarks>
public sealed class RefreshToken
{
    private readonly string _value;

    public RefreshToken(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _value = value;
    }

    /// <summary>Returns the bearer string. Treat as a secret.</summary>
    public string AsInsecureToken() => _value;

    /// <summary>Returns a redacted placeholder. Use <see cref="AsInsecureToken"/> to access the value.</summary>
    public override string ToString() => "RefreshToken(REDACTED)";
}
