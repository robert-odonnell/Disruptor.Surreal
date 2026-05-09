using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Auth;

/// <summary>
/// The complete authentication payload returned by signin / signup / refresh.
/// Carries an <see cref="AccessToken"/> always; the optional <see cref="Refresh"/>
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
/// <see cref="Token"/> with no refresh) or an object with <c>access</c> /
/// <c>refresh</c> fields.
/// </item>
/// </list>
/// </remarks>
public sealed record Token(AccessToken Access, RefreshToken? Refresh = null)
{
    /// <summary>Convenience constructor from a bare access-token string.</summary>
    public static Token FromAccessTokenString(string accessToken) =>
        new(new AccessToken(accessToken));

    /// <summary>Renders to the wire <see cref="Value"/> shape (string or object).</summary>
    internal Value ToValue() => Refresh is null
        ? Access.AsInsecureToken()
        : new ObjectValue(new SurrealObject
        {
            ["access"] = Access.AsInsecureToken(),
            ["refresh"] = Refresh.AsInsecureToken(),
        });

    /// <summary>Parses the wire <see cref="Value"/> shape back into a <see cref="Token"/>.</summary>
    internal static Token FromValue(Value value)
    {
        switch (value)
        {
            case StringValue s:
                return FromAccessTokenString(s.Value);
            case ObjectValue obj:
                {
                    var access = (obj.Object.TryGetValue("access", out var a) && a is StringValue accStr)
                        ? new AccessToken(accStr.Value)
                        : throw new SurrealProtocolException("Token object missing 'access' field.");
                    RefreshToken? refresh = null;
                    if (obj.Object.TryGetValue("refresh", out var r) && r is StringValue refStr
                        && !string.IsNullOrEmpty(refStr.Value))
                    {
                        refresh = new RefreshToken(refStr.Value);
                    }
                    return new Token(access, refresh);
                }
            default:
                throw new SurrealProtocolException(
                    $"Token must be a string or object; got {value.Kind}.");
        }
    }
}
