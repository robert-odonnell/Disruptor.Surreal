using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Connection;

/// <summary>
/// An RPC command to send to the server. Closed hierarchy — every native v1 method
/// has a sealed record below.
/// </summary>
internal abstract record Command
{
    private protected Command() { }

    /// <summary>The wire-level method name for this command.</summary>
    public abstract string Method { get; }

    /// <summary>Builds the <c>params</c> array for the wire request, or <c>null</c> when the command has none.</summary>
    public abstract Value? BuildParams();

    /// <summary>The transaction id this command runs inside, if any.</summary>
    public virtual Guid? TxnId => null;
}

internal sealed record UseCommand(string? Namespace, string? Database) : Command
{
    public override string Method => "use";
    public override Value BuildParams() => new ArrayValue(new SurrealArray
    {
        Namespace is null ? Value.None : Namespace,
        Database is null ? Value.None : Database,
    });
}

internal sealed record SigninCommand(SurrealObject Credentials) : Command
{
    public override string Method => "signin";
    public override Value BuildParams() => new ArrayValue(new SurrealArray { new ObjectValue(Credentials) });
}

internal sealed record SignupCommand(SurrealObject Credentials) : Command
{
    public override string Method => "signup";
    public override Value BuildParams() => new ArrayValue(new SurrealArray { new ObjectValue(Credentials) });
}

internal sealed record AuthenticateCommand(string Token) : Command
{
    public override string Method => "authenticate";
    public override Value BuildParams() => new ArrayValue(new SurrealArray { Token });
}

internal sealed record InvalidateCommand : Command
{
    public override string Method => "invalidate";
    public override Value? BuildParams() => null;
}

internal sealed record SetCommand(string Key, Value VarValue) : Command
{
    public override string Method => "let";
    public override Value BuildParams() => new ArrayValue(new SurrealArray { Key, VarValue });
}

internal sealed record UnsetCommand(string Key) : Command
{
    public override string Method => "unset";
    public override Value BuildParams() => new ArrayValue(new SurrealArray { Key });
}

internal sealed record QueryCommand(string Sql, SurrealObject? Variables, Guid? Txn) : Command
{
    public override string Method => "query";
    public override Guid? TxnId => Txn;
    public override Value BuildParams() => new ArrayValue(new SurrealArray
    {
        Sql,
        new ObjectValue(Variables ?? new SurrealObject()),
    });
}

internal sealed record BeginCommand : Command
{
    public override string Method => "begin";
    public override Value? BuildParams() => null;
}

internal sealed record CommitCommand(Guid Txn) : Command
{
    public override string Method => "commit";
    public override Value BuildParams() => new ArrayValue(new SurrealArray { Txn });
}

internal sealed record CancelCommand(Guid Txn) : Command
{
    public override string Method => "cancel";
    public override Value BuildParams() => new ArrayValue(new SurrealArray { Txn });
}

internal sealed record HealthCommand : Command
{
    public override string Method => "ping";
    public override Value? BuildParams() => null;
}

internal sealed record VersionCommand : Command
{
    public override string Method => "version";
    public override Value? BuildParams() => null;
}
