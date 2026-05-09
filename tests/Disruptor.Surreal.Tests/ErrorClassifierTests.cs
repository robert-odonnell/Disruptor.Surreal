using Disruptor.Surreal;
using Disruptor.Surreal.Connection;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class ErrorClassifierTests
{
    [Fact]
    public void TokenExpired_ProducesAuthExceptionWithFlag()
    {
        var err = new RpcErrorPayload(401, "token expired", Kind: "Auth", DetailsKind: "TokenExpired");
        var ex = Assert.IsType<SurrealAuthException>(err.ToException());
        Assert.True(ex.IsTokenExpired);
        Assert.Equal("Auth", ex.Kind);
    }

    [Fact]
    public void GenericAuthFailure_StillProducesAuthException()
    {
        var err = new RpcErrorPayload(401, "unauthorized", Kind: null, DetailsKind: null);
        var ex = Assert.IsType<SurrealAuthException>(err.ToException());
        Assert.False(ex.IsTokenExpired);
    }

    [Fact]
    public void TransactionConflict_ProducesConflictException()
    {
        var err = new RpcErrorPayload(0, "transaction conflict", null, null);
        Assert.IsType<SurrealConflictException>(err.ToException());
    }

    [Fact]
    public void TransactionNotFound_ProducesAbortedException()
    {
        var err = new RpcErrorPayload(0, "transaction not found", null, null);
        Assert.IsType<SurrealTransactionAbortedException>(err.ToException());
    }

    [Fact]
    public void Unknown_FallsThroughToRpcException()
    {
        var err = new RpcErrorPayload(0, "some other failure", null, null);
        var ex = err.ToException();
        Assert.IsType<SurrealRpcException>(ex);
        Assert.IsNotType<SurrealConflictException>(ex);
        Assert.IsNotType<SurrealAuthException>(ex);
    }
}
