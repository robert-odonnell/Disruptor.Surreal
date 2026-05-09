using Disruptor.Surreal.Connection;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal;

/// <summary>
/// Handle to a server-side transaction obtained from
/// <see cref="Surreal.BeginTransactionAsync(CancellationToken)"/>.
/// </summary>
/// <remarks>
/// Operations invoked via this handle run inside the transaction. Callers MUST
/// either <see cref="CommitAsync(CancellationToken)"/> or
/// <see cref="CancelAsync(CancellationToken)"/>; disposing without explicit settlement
/// triggers a best-effort cancel.
/// </remarks>
public sealed class Transaction : IAsyncDisposable
{
    private readonly Surreal _client;
    private readonly IConnection _connection;
    private int _settled;

    /// <summary>The server-issued transaction identifier.</summary>
    public Guid Id { get; }

    internal Transaction(Surreal client, IConnection connection, Guid id)
    {
        _client = client;
        _connection = connection;
        Id = id;
    }

    /// <summary>Run a query inside this transaction.</summary>
    public async Task<QueryResponse> QueryAsync(
        string sql, SurrealObject? bindings = null, CancellationToken ct = default)
    {
        EnsureLive();
        var raw = await _client.QueryRawAsync(sql, bindings, Id, ct).ConfigureAwait(false);
        return QueryResponse.FromValue(raw);
    }

    /// <summary>Select from a table inside this transaction.</summary>
    public Task<Value> SelectAsync(string table, CancellationToken ct = default)
    {
        EnsureLive();
        return _client.ResourceQueryAsync("SELECT * FROM $_table",
            new SurrealObject { ["_table"] = new TableValue(new Table(table)) }, Id, ct);
    }

    /// <summary>Select a record by id inside this transaction.</summary>
    public Task<Value> SelectAsync(IRecordId id, CancellationToken ct = default)
    {
        EnsureLive();
        return _client.ResourceQueryAsync("SELECT * FROM $_record_id",
            new SurrealObject { ["_record_id"] = new RecordIdValue(id.ToRecordId()) }, Id, ct);
    }

    /// <summary>Create a record at the given id inside this transaction.</summary>
    public Task<Value> CreateAsync(IRecordId id, SurrealObject? content = null, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject { ["_record_id"] = new RecordIdValue(id.ToRecordId()) };
        var sql = "CREATE $_record_id";
        if (content is not null)
        {
            vars["_content"] = new ObjectValue(content);
            sql = "CREATE $_record_id CONTENT $_content";
        }
        return _client.ResourceQueryAsync(sql, vars, Id, ct);
    }

    /// <summary>Update a record by id inside this transaction.</summary>
    public Task<Value> UpdateAsync(IRecordId id, SurrealObject content, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject
        {
            ["_record_id"] = new RecordIdValue(id.ToRecordId()),
            ["_content"] = new ObjectValue(content),
        };
        return _client.ResourceQueryAsync("UPDATE $_record_id CONTENT $_content", vars, Id, ct);
    }

    /// <summary>Delete a record by id inside this transaction.</summary>
    public Task<Value> DeleteAsync(IRecordId id, CancellationToken ct = default)
    {
        EnsureLive();
        return _client.ResourceQueryAsync("DELETE $_record_id RETURN BEFORE",
            new SurrealObject { ["_record_id"] = new RecordIdValue(id.ToRecordId()) }, Id, ct);
    }

    /// <summary>Commit the transaction. Subsequent operations on this handle throw.</summary>
    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _settled, 1) != 0)
            throw new InvalidOperationException("Transaction already settled.");
        await _connection.SendAsync(new CommitCommand(Id), ct).ConfigureAwait(false);
    }

    /// <summary>Cancel the transaction (rollback). Subsequent operations on this handle throw.</summary>
    public async Task CancelAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _settled, 1) != 0)
            throw new InvalidOperationException("Transaction already settled.");
        await _connection.SendAsync(new CancelCommand(Id), ct).ConfigureAwait(false);
    }

    private void EnsureLive()
    {
        if (Volatile.Read(ref _settled) != 0)
            throw new InvalidOperationException(
                "Transaction has already been committed or cancelled.");
    }

    /// <summary>If still pending, attempt a best-effort cancel.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _settled, 1) == 0 && _connection.IsConnected)
        {
            try
            {
                await _connection.SendAsync(new CancelCommand(Id), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // best-effort; the server will GC the transaction on session close
            }
        }
    }
}
