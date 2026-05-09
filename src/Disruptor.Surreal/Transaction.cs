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
    private readonly Surreal client;
    private readonly IConnection connection;
    private int settled;

    /// <summary>The server-issued transaction identifier.</summary>
    public Guid Id { get; }

    internal Transaction(Surreal client, IConnection connection, Guid id)
    {
        this.client = client;
        this.connection = connection;
        Id = id;
    }

    /// <summary>Run a query inside this transaction.</summary>
    public async Task<QueryResponse> QueryAsync(
        string sql, SurrealObject? bindings = null, CancellationToken ct = default)
    {
        EnsureLive();
        var raw = await client.QueryRawAsync(sql, bindings, Id, ct).ConfigureAwait(false);
        return QueryResponse.FromValue(raw);
    }

    /// <summary>Select from a table inside this transaction.</summary>
    public Task<Value> SelectAsync(string table, CancellationToken ct = default)
    {
        EnsureLive();
        return client.ResourceQueryAsync("SELECT * FROM $_table",
            new SurrealObject { ["_table"] = new TableValue(new Table(table)) }, Id, ct);
    }

    /// <summary>Select a record by id inside this transaction.</summary>
    public Task<Value> SelectAsync(IRecordId id, CancellationToken ct = default)
    {
        EnsureLive();
        return client.ResourceQueryAsync("SELECT * FROM $_record_id",
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
        return client.ResourceQueryAsync(sql, vars, Id, ct);
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
        return client.ResourceQueryAsync("UPDATE $_record_id CONTENT $_content", vars, Id, ct);
    }

    /// <summary>Delete a record by id inside this transaction.</summary>
    public Task<Value> DeleteAsync(IRecordId id, CancellationToken ct = default)
    {
        EnsureLive();
        return client.ResourceQueryAsync("DELETE $_record_id RETURN BEFORE",
            new SurrealObject { ["_record_id"] = new RecordIdValue(id.ToRecordId()) }, Id, ct);
    }

    /// <summary>Upsert a record by id inside this transaction.</summary>
    public Task<Value> UpsertAsync(IRecordId id, SurrealObject content, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject
        {
            ["_record_id"] = new RecordIdValue(id.ToRecordId()),
            ["_content"] = new ObjectValue(content),
        };
        return client.ResourceQueryAsync("UPSERT $_record_id CONTENT $_content", vars, Id, ct);
    }

    /// <summary>Merge fields into a record by id inside this transaction.</summary>
    public Task<Value> MergeAsync(IRecordId id, SurrealObject content, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject
        {
            ["_record_id"] = new RecordIdValue(id.ToRecordId()),
            ["_content"] = new ObjectValue(content),
        };
        return client.ResourceQueryAsync("UPDATE $_record_id MERGE $_content", vars, Id, ct);
    }

    /// <summary>Apply JSON-Patch operations to a record by id inside this transaction.</summary>
    public Task<Value> PatchAsync(IRecordId id, IEnumerable<SurrealObject> patches, CancellationToken ct = default)
    {
        EnsureLive();
        var arr = new SurrealArray();
        foreach (var p in patches) arr.Add(new ObjectValue(p));
        var vars = new SurrealObject
        {
            ["_record_id"] = new RecordIdValue(id.ToRecordId()),
            ["_patches"] = new ArrayValue(arr),
        };
        return client.ResourceQueryAsync("UPDATE $_record_id PATCH $_patches", vars, Id, ct);
    }

    /// <summary>Insert a single record into a table inside this transaction.</summary>
    public Task<Value> InsertAsync(string table, SurrealObject content, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject
        {
            ["_table"] = new TableValue(new Table(table)),
            ["_content"] = new ObjectValue(content),
        };
        return client.ResourceQueryAsync("INSERT INTO $_table $_content", vars, Id, ct);
    }

    /// <summary>Insert multiple records into a table inside this transaction.</summary>
    public Task<Value> InsertAsync(string table, IEnumerable<SurrealObject> records, CancellationToken ct = default)
    {
        EnsureLive();
        var arr = new SurrealArray();
        foreach (var r in records) arr.Add(new ObjectValue(r));
        var vars = new SurrealObject
        {
            ["_table"] = new TableValue(new Table(table)),
            ["_records"] = new ArrayValue(arr),
        };
        return client.ResourceQueryAsync("INSERT INTO $_table $_records", vars, Id, ct);
    }

    /// <summary>Insert one graph-edge record inside this transaction.</summary>
    public Task<Value> InsertRelationAsync(string edgeTable, SurrealObject content, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject
        {
            ["_table"] = new TableValue(new Table(edgeTable)),
            ["_content"] = new ObjectValue(content),
        };
        return client.ResourceQueryAsync("INSERT RELATION INTO $_table $_content", vars, Id, ct);
    }

    /// <summary>Insert multiple graph-edge records inside this transaction.</summary>
    public Task<Value> InsertRelationAsync(string edgeTable, IEnumerable<SurrealObject> relations, CancellationToken ct = default)
    {
        EnsureLive();
        var arr = new SurrealArray();
        foreach (var r in relations) arr.Add(new ObjectValue(r));
        var vars = new SurrealObject
        {
            ["_table"] = new TableValue(new Table(edgeTable)),
            ["_records"] = new ArrayValue(arr),
        };
        return client.ResourceQueryAsync("INSERT RELATION INTO $_table $_records", vars, Id, ct);
    }

    /// <summary>
    /// Create a graph edge inside this transaction:
    /// <c>RELATE source -&gt; edgeTable -&gt; target [CONTENT { ... }]</c>.
    /// </summary>
    public Task<Value> RelateAsync(
        IRecordId source,
        string edgeTable,
        IRecordId target,
        SurrealObject? content = null,
        CancellationToken ct = default)
    {
        EnsureLive();
        if (!System.Text.RegularExpressions.Regex.IsMatch(edgeTable, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            throw new ArgumentException(
                $"'{edgeTable}' is not a valid SurrealQL identifier.", nameof(edgeTable));
        var sql = content is null
            ? $"RELATE $_source->{edgeTable}->$_target"
            : $"RELATE $_source->{edgeTable}->$_target CONTENT $_content";
        var vars = new SurrealObject
        {
            ["_source"] = new RecordIdValue(source.ToRecordId()),
            ["_target"] = new RecordIdValue(target.ToRecordId()),
        };
        if (content is not null) vars["_content"] = new ObjectValue(content);
        return client.ResourceQueryAsync(sql, vars, Id, ct);
    }

    /// <summary>Commit the transaction. Subsequent operations on this handle throw.</summary>
    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref settled, 1) != 0)
            throw new InvalidOperationException("Transaction already settled.");
        await connection.SendAsync(new CommitCommand(Id), ct).ConfigureAwait(false);
    }

    /// <summary>Cancel the transaction (rollback). Subsequent operations on this handle throw.</summary>
    public async Task CancelAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref settled, 1) != 0)
            throw new InvalidOperationException("Transaction already settled.");
        await connection.SendAsync(new CancelCommand(Id), ct).ConfigureAwait(false);
    }

    private void EnsureLive()
    {
        if (Volatile.Read(ref settled) != 0)
            throw new InvalidOperationException(
                "Transaction has already been committed or cancelled.");
    }

    /// <summary>If still pending, attempt a best-effort cancel.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref settled, 1) == 0 && connection.IsConnected)
        {
            try
            {
                await connection.SendAsync(new CancelCommand(Id), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // best-effort; the server will GC the transaction on session close
            }
        }
    }
}
