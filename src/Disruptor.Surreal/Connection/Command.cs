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
    public abstract SurrealValue? BuildParams();

    /// <summary>The transaction id this command runs inside, if any.</summary>
    public virtual Guid? TxnId => null;
}

internal sealed record UseCommand(string? Namespace, string? Database) : Command
{
    public override string Method => "use";
    public override SurrealValue BuildParams() => new SurrealListValue(
        [
            Namespace is null ? SurrealValue.None : Namespace,
            Database is null ? SurrealValue.None : Database
        ]
    );
}

internal sealed record SigninCommand(SurrealObject Credentials) : Command
{
    public override string Method => "signin";
    public override SurrealValue BuildParams() => new SurrealListValue([new SurrealObjectValue(Credentials)]);
}

internal sealed record SignupCommand(SurrealObject Credentials) : Command
{
    public override string Method => "signup";
    public override SurrealValue BuildParams() => new SurrealListValue([new SurrealObjectValue(Credentials)]);
}

internal sealed record AuthenticateCommand(string Token) : Command
{
    public override string Method => "authenticate";
    public override SurrealValue BuildParams() => new SurrealListValue([Token]);
}

internal sealed record RefreshCommand(Auth.SurrealToken SurrealToken) : Command
{
    public override string Method => "refresh";
    public override SurrealValue BuildParams() => new SurrealListValue([SurrealToken.ToValue()]);
}

internal sealed record InvalidateCommand : Command
{
    public override string Method => "invalidate";
    public override SurrealValue? BuildParams() => null;
}

internal sealed record SetCommand(string Key, SurrealValue SurrealVarValue) : Command
{
    public override string Method => "let";
    public override SurrealValue BuildParams() => new SurrealListValue(
        [
            Key,
            SurrealVarValue
        ]
    );
}

internal sealed record UnsetCommand(string Key) : Command
{
    public override string Method => "unset";
    public override SurrealValue BuildParams() => new SurrealListValue([Key]);
}

internal sealed record QueryCommand(string Sql, SurrealObject? Variables, Guid? Txn) : Command
{
    public override string Method => "query";
    public override Guid? TxnId => Txn;
    public override SurrealValue BuildParams() => new SurrealListValue(
        [
            Sql,
            new SurrealObjectValue(Variables ?? new SurrealObject())
        ]
    );
}

internal sealed record BeginCommand : Command
{
    public override string Method => "begin";
    public override SurrealValue? BuildParams() => null;
}

internal sealed record CommitCommand(Guid Txn) : Command
{
    public override string Method => "commit";
    public override SurrealValue BuildParams() => new SurrealListValue([Txn]);
}

internal sealed record CancelCommand(Guid Txn) : Command
{
    public override string Method => "cancel";
    public override SurrealValue BuildParams() => new SurrealListValue([Txn]);
}

internal sealed record HealthCommand : Command
{
    public override string Method => "ping";
    public override SurrealValue? BuildParams() => null;
}

internal sealed record VersionCommand : Command
{
    public override string Method => "version";
    public override SurrealValue? BuildParams() => null;
}

internal sealed record RunCommand(string Name, string? Version, IReadOnlyList<SurrealValue> Args) : Command
{
    public override string Method => "run";
    public override SurrealValue BuildParams()
    {
        var argsArray = new SurrealList(Args.Count);
        foreach (var a in Args) argsArray.Add(a);
        return new SurrealListValue(
            [
                Name,
                Version is null ? SurrealValue.None : Version,
                new SurrealListValue(argsArray)
            ]
        );
    }
}

internal sealed record KillCommand(Guid LiveQueryId) : Command
{
    public override string Method => "kill";
    public override SurrealValue BuildParams() => new SurrealListValue([LiveQueryId]);
}

/// <summary>
/// Bridge between the internal <see cref="Command"/> DSL and the public wire-level
/// <see cref="ISurrealConnection.SendAsync(string, SurrealValue?, Guid?, CancellationToken)"/>.
/// Keeps existing <c>_connection.SendAsync(new XCommand(...))</c> call sites in
/// <see cref="SurrealClient"/> compiling without converting to the wire signature at every
/// call.
/// </summary>
internal static class ConnectionCommandExtensions
{
    public static Task<SurrealValue> SendAsync(
        this ISurrealConnection surrealConnection,
        Command command,
        CancellationToken ct = default) =>
        surrealConnection.SendAsync(command.Method, command.BuildParams(), command.TxnId, ct);
}
