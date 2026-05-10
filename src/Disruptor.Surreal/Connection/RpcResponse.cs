using Disruptor.Surreal.Cbor;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Connection;

/// <summary>
/// The wire-level response envelope. The server emits one of:
/// <code>
/// { id, result }                                    // success
/// { id, error: { code, message, kind?, details? } } // failure
/// { id?, result | error, ... }                      // also accepted for live notifications (no id)
/// </code>
/// </summary>
internal sealed class RpcResponse
{
    /// <summary>The id from the matching request, when present.</summary>
    public long? Id { get; init; }

    /// <summary>The session id (when the server includes one).</summary>
    public Guid? SessionId { get; init; }

    /// <summary>The success payload, or <c>null</c> if <see cref="Error"/> is set.</summary>
    public SurrealValue? Result { get; init; }

    /// <summary>The error payload, or <c>null</c> on success.</summary>
    public RpcErrorPayload? Error { get; init; }

    /// <summary>True when the response indicates failure.</summary>
    public bool IsError => Error is not null;

    public static RpcResponse Decode(ReadOnlyMemory<byte> bytes)
    {
        var value = SurrealCborValueReader.Decode(bytes);
        if (value is not SurrealObjectValue { Object: var obj })
            throw new SurrealProtocolException(
                $"RPC response must be a CBOR map; got {value.Kind}.");

        long? id = null;
        if (obj.TryGetValue("id", out var idValue) && idValue is SurrealNumberValue { SurrealNumber: var n } && n.Kind == SurrealNumberKind.Int)
            id = n.AsInt();

        Guid? sessionId = null;
        if (obj.TryGetValue("session", out var sessValue) && sessValue is SurrealUuidValue u)
            sessionId = u.Value;

        if (obj.TryGetValue("error", out var errVal))
        {
            var err = ParseError(errVal);
            return new RpcResponse { Id = id, SessionId = sessionId, Error = err };
        }

        if (obj.TryGetValue("result", out var resultVal))
            return new RpcResponse { Id = id, SessionId = sessionId, Result = resultVal };

        throw new SurrealProtocolException("RPC response missing both 'result' and 'error'.");
    }

    private static RpcErrorPayload ParseError(SurrealValue surrealValue)
    {
        if (surrealValue is not SurrealObjectValue { Object: var obj })
            return new RpcErrorPayload(0, $"Unstructured error: {surrealValue}", null, null);

        long code = 0;
        if (obj.TryGetValue("code", out var c) && c is SurrealNumberValue { SurrealNumber.Kind: SurrealNumberKind.Int } cn)
            code = cn.SurrealNumber.AsInt();

        var message = "Unknown error";
        if (obj.TryGetValue("message", out var m) && m is StringSurrealValue ms)
            message = ms.Value;

        string? kind = null;
        if (obj.TryGetValue("kind", out var k) && k is StringSurrealValue ks)
            kind = ks.Value;

        string? detailsKind = null;
        if (obj.TryGetValue("details", out var d) && d is SurrealObjectValue { Object: var details })
        {
            // Surreal flattens ErrorDetails: e.g. NotAllowed { Auth { TokenExpired } } produces
            // kind="NotAllowed", details={ kind: "Auth", details: { kind: "TokenExpired" } } — we
            // walk one level for the immediate sub-discriminator.
            if (details.TryGetValue("kind", out var dk) && dk is StringSurrealValue dks)
                detailsKind = dks.Value;
        }

        return new RpcErrorPayload(code, message, kind, detailsKind);
    }
}

/// <summary>The decoded shape of an RPC error from the server.</summary>
internal readonly record struct RpcErrorPayload(long Code, string Message, string? Kind, string? DetailsKind)
{
    /// <summary>
    /// Maps the wire error to the most specific exception type we can determine. Coarse on day one;
    /// refines as we observe live error responses.
    /// </summary>
    public SurrealRpcException ToException()
    {
        // Auth — token expired is the headline case the SDK auto-handles upstream.
        if (string.Equals(Kind, "Auth", StringComparison.OrdinalIgnoreCase)
            || string.Equals(DetailsKind, "Auth", StringComparison.OrdinalIgnoreCase)
            || ContainsAny(Message, "token expired", "unauthorized", "authentication"))
        {
            return new SurrealAuthException(Code, Message)
            {
                Kind = Kind,
                IsTokenExpired = ContainsAny(Message, "token expired", "expired token")
                    || string.Equals(DetailsKind, "TokenExpired", StringComparison.OrdinalIgnoreCase),
            };
        }

        // Transactional conflict — retryable.
        if (ContainsAny(Message,
            "transaction conflict",
            "serialization failure",
            "key conflict",
            "concurrent modification"))
        {
            return new SurrealConflictException(Code, Message) { Kind = Kind };
        }

        // Transaction is dead (id no longer valid, server aborted, etc.).
        if (ContainsAny(Message,
            "transaction not found",
            "transaction has been aborted",
            "no active transaction"))
        {
            return new SurrealTransactionAbortedException(Code, Message) { Kind = Kind };
        }

        // Schema / integrity violations.
        if (ContainsAny(Message,
            "already exists",         // record-id collision on CREATE / INSERT
            "already contains",       // UNIQUE index violation
            "violates assertion",     // ASSERT clause
            "field validation",
            "found a "))              // canonical "expected X but found a Y"
        {
            return new SurrealConstraintException(Code, Message) { Kind = Kind };
        }

        return new SurrealRpcException(Code, Message) { Kind = Kind };
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
