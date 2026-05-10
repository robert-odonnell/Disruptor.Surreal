using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Auth;

/// <summary>
/// The complete authentication payload returned by signin / signup / refresh.
/// Carries an <see cref="SurrealAccessToken"/> always; the optional <see cref="Refresh"/>
/// is present when the access method has refresh-token rotation enabled.
/// </summary>
/// <remarks>
/// Wire shape (mirrors the Rust client's <c>SurrealValue</c> impl in
/// <c>src/opt/auth.rs</c>):
/// <list type="bullet">
/// <item>
/// <c>SurrealValue::into_value</c>: an object <c>{ "access": …, "refresh": … }</c>
/// when <see cref="Refresh"/> is present; otherwise the bare access-token string.
/// </item>
/// <item>
/// <c>SurrealValue::from_value</c>: accepts either a string (becomes
/// <see cref="SurrealToken"/> with no refresh) or an object with <c>access</c> /
/// <c>refresh</c> fields.
/// </item>
/// </list>
/// </remarks>
public sealed record SurrealToken(SurrealAccessToken SurrealAccess, SurrealRefreshToken? Refresh = null)
{
    /// <summary>Convenience constructor from a bare access-token string.</summary>
    public static SurrealToken FromAccessTokenString(string accessToken) =>
        new(new SurrealAccessToken(accessToken));

    /// <summary>Renders to the wire <see cref="SurrealValue"/> shape (string or object).</summary>
    internal SurrealValue ToValue() => Refresh is null
        ? SurrealAccess.AsInsecureToken()
        : new SurrealObjectValue(new SurrealObject
        {
            ["access"] = SurrealAccess.AsInsecureToken(),
            ["refresh"] = Refresh.AsInsecureToken(),
        });

    /// <summary>Parses the wire <see cref="SurrealValue"/> shape back into a <see cref="SurrealToken"/>.</summary>
    internal static SurrealToken FromValue(SurrealValue surrealValue)
    {
        switch (surrealValue)
        {
            case StringSurrealValue s:
                return FromAccessTokenString(s.Value);
            case SurrealObjectValue obj:
                {
                    var access = (obj.Object.TryGetValue("access", out var a) && a is StringSurrealValue accStr)
                        ? new SurrealAccessToken(accStr.Value)
                        : throw new SurrealProtocolException("Token object missing 'access' field.");
                    SurrealRefreshToken? refresh = null;
                    if (obj.Object.TryGetValue("refresh", out var r) && r is StringSurrealValue refStr
                        && !string.IsNullOrEmpty(refStr.Value))
                    {
                        refresh = new SurrealRefreshToken(refStr.Value);
                    }
                    return new SurrealToken(access, refresh);
                }
            default:
                throw new SurrealProtocolException(
                    $"Token must be a string or object; got {surrealValue.Kind}.");
        }
    }
}
