namespace Disruptor.Surreal;

/// <summary>The base exception type raised by all <see cref="Disruptor.Surreal"/> APIs.</summary>
public class SurrealException : Exception
{
    /// <summary>The wire-level error code from the server, if available.</summary>
    public long Code { get; init; }

    public SurrealException(string message) : base(message) { }
    public SurrealException(string message, Exception inner) : base(message, inner) { }
    public SurrealException(long code, string message) : base(message) => Code = code;
}

/// <summary>Connection-level failure (transport, handshake, server unreachable).</summary>
public class SurrealConnectionException : SurrealException
{
    public SurrealConnectionException(string message) : base(message) { }
    public SurrealConnectionException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The server returned an error in response to an RPC.</summary>
public class SurrealRpcException(long code, string message)
    : SurrealException(code, message)
{
    /// <summary>The wire-level error <c>kind</c> string ("Validation", "NotAllowed", "Auth", ...) or null.</summary>
    public string? Kind { get; init; }
}

/// <summary>Authentication failure (signin, authenticate, expired token).</summary>
public class SurrealAuthException(long code, string message)
    : SurrealRpcException(code, message)
{
    /// <summary>True when the server specifically signalled the access token has expired.</summary>
    public bool IsTokenExpired { get; init; }
}

/// <summary>
/// A transactional conflict — the server detected concurrent modification or a serialization
/// failure. Retryable: re-run the whole load → mutate → commit cycle.
/// </summary>
public class SurrealConflictException(long code, string message)
    : SurrealRpcException(code, message);

/// <summary>
/// The server aborted the active transaction. Distinct from <see cref="SurrealConflictException"/>
/// — usually means the transaction id became invalid (server-side timeout, explicit abort,
/// connection-level reset). The transaction handle is dead; reload and start over.
/// </summary>
public class SurrealTransactionAbortedException(long code, string message)
    : SurrealRpcException(code, message);

/// <summary>
/// A schema or data-integrity violation — UNIQUE index, ASSERT clause, REFERENCE behaviour,
/// type mismatch. Not retryable; surface to the caller.
/// </summary>
public class SurrealConstraintException(long code, string message)
    : SurrealRpcException(code, message);

/// <summary>The wire payload couldn't be decoded.</summary>
public class SurrealProtocolException : SurrealException
{
    public SurrealProtocolException(string message) : base(message) { }
    public SurrealProtocolException(string message, Exception inner) : base(message, inner) { }
}
