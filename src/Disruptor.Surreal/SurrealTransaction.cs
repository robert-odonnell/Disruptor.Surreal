using System.Text.RegularExpressions;
using Disruptor.Surreal.Connection;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal;

/// <summary>
/// Handle to a server-side transaction obtained from
/// <see cref="SurrealClient.BeginTransactionAsync(CancellationToken)"/>.
/// </summary>
/// <remarks>
/// Operations invoked via this handle run inside the transaction. Callers MUST
/// either <see cref="CommitAsync(CancellationToken)"/> or
/// <see cref="CancelAsync(CancellationToken)"/>; disposing without explicit settlement
/// triggers a best-effort cancel.
/// </remarks>
public sealed partial class SurrealTransaction : IAsyncDisposable
{
    private readonly SurrealClient client;
    private readonly ISurrealConnection surrealConnection;
    private int settled;

    /// <summary>The server-issued transaction identifier.</summary>
    public Guid Id { get; }

    internal SurrealTransaction(SurrealClient client, ISurrealConnection surrealConnection, Guid id)
    {
        this.client = client;
        this.surrealConnection = surrealConnection;
        Id = id;
    }

    /// <summary>Run a query inside this transaction.</summary>
    public async Task<SurrealQueryResponse> QueryAsync(
        string sql, SurrealObject? bindings = null, CancellationToken ct = default)
    {
        EnsureLive();
        var raw = await client.QueryRawAsync(sql, bindings, Id, ct).ConfigureAwait(false);
        return SurrealQueryResponse.FromValue(raw);
    }

    /// <summary>Select from a table inside this transaction.</summary>
    public Task<SurrealValue> SelectAsync(string table, CancellationToken ct = default)
    {
        EnsureLive();
        return client.ResourceQueryAsync("SELECT * FROM $_table",
            new SurrealObject { ["_table"] = new SurrealTableValue(new SurrealTable(table)) }, Id, ct);
    }

    /// <summary>Select a record by id inside this transaction.</summary>
    public Task<SurrealValue> SelectAsync(ISurrealRecordId id, CancellationToken ct = default)
    {
        EnsureLive();
        return client.ResourceQueryAsync("SELECT * FROM $_record_id",
            new SurrealObject { ["_record_id"] = new SurrealRecordIdValue(id.ToRecordId()) }, Id, ct);
    }

    /// <summary>Create a record at the given id inside this transaction.</summary>
    public Task<SurrealValue> CreateAsync(ISurrealRecordId id, SurrealObject? content = null, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject { ["_record_id"] = new SurrealRecordIdValue(id.ToRecordId()) };
        var sql = "CREATE $_record_id";
        if (content is not null)
        {
            vars["_content"] = new SurrealObjectValue(content);
            sql = "CREATE $_record_id CONTENT $_content";
        }
        return client.ResourceQueryAsync(sql, vars, Id, ct);
    }

    /// <summary>Update a record by id inside this transaction.</summary>
    public Task<SurrealValue> UpdateAsync(ISurrealRecordId id, SurrealObject content, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject
        {
            ["_record_id"] = new SurrealRecordIdValue(id.ToRecordId()),
            ["_content"] = new SurrealObjectValue(content),
        };
        return client.ResourceQueryAsync("UPDATE $_record_id CONTENT $_content", vars, Id, ct);
    }

    /// <summary>Delete a record by id inside this transaction.</summary>
    public Task<SurrealValue> DeleteAsync(ISurrealRecordId id, CancellationToken ct = default)
    {
        EnsureLive();
        return client.ResourceQueryAsync("DELETE $_record_id RETURN BEFORE",
            new SurrealObject { ["_record_id"] = new SurrealRecordIdValue(id.ToRecordId()) }, Id, ct);
    }

    /// <summary>Upsert a record by id inside this transaction.</summary>
    public Task<SurrealValue> UpsertAsync(ISurrealRecordId id, SurrealObject content, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject
        {
            ["_record_id"] = new SurrealRecordIdValue(id.ToRecordId()),
            ["_content"] = new SurrealObjectValue(content),
        };
        return client.ResourceQueryAsync("UPSERT $_record_id CONTENT $_content", vars, Id, ct);
    }

    /// <summary>Merge fields into a record by id inside this transaction.</summary>
    public Task<SurrealValue> MergeAsync(ISurrealRecordId id, SurrealObject content, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject
        {
            ["_record_id"] = new SurrealRecordIdValue(id.ToRecordId()),
            ["_content"] = new SurrealObjectValue(content),
        };
        return client.ResourceQueryAsync("UPDATE $_record_id MERGE $_content", vars, Id, ct);
    }

    /// <summary>Apply JSON-Patch operations to a record by id inside this transaction.</summary>
    public Task<SurrealValue> PatchAsync(ISurrealRecordId id, IEnumerable<SurrealObject> patches, CancellationToken ct = default)
    {
        EnsureLive();
        var arr = new SurrealList();
        foreach (var p in patches) arr.Add(new SurrealObjectValue(p));
        var vars = new SurrealObject
        {
            ["_record_id"] = new SurrealRecordIdValue(id.ToRecordId()),
            ["_patches"] = new SurrealListValue(arr),
        };
        return client.ResourceQueryAsync("UPDATE $_record_id PATCH $_patches", vars, Id, ct);
    }

    /// <summary>Insert a single record into a table inside this transaction.</summary>
    public Task<SurrealValue> InsertAsync(string table, SurrealObject content, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject
        {
            ["_table"] = new SurrealTableValue(new SurrealTable(table)),
            ["_content"] = new SurrealObjectValue(content),
        };
        return client.ResourceQueryAsync("INSERT INTO $_table $_content", vars, Id, ct);
    }

    /// <summary>Insert multiple records into a table inside this transaction.</summary>
    public Task<SurrealValue> InsertAsync(string table, IEnumerable<SurrealObject> records, CancellationToken ct = default)
    {
        EnsureLive();
        var arr = new SurrealList();
        foreach (var r in records) arr.Add(new SurrealObjectValue(r));
        var vars = new SurrealObject
        {
            ["_table"] = new SurrealTableValue(new SurrealTable(table)),
            ["_records"] = new SurrealListValue(arr),
        };
        return client.ResourceQueryAsync("INSERT INTO $_table $_records", vars, Id, ct);
    }

    /// <summary>Insert one graph-edge record inside this transaction.</summary>
    public Task<SurrealValue> InsertRelationAsync(string edgeTable, SurrealObject content, CancellationToken ct = default)
    {
        EnsureLive();
        var vars = new SurrealObject
        {
            ["_table"] = new SurrealTableValue(new SurrealTable(edgeTable)),
            ["_content"] = new SurrealObjectValue(content),
        };
        return client.ResourceQueryAsync("INSERT RELATION INTO $_table $_content", vars, Id, ct);
    }

    /// <summary>Insert multiple graph-edge records inside this transaction.</summary>
    public Task<SurrealValue> InsertRelationAsync(string edgeTable, IEnumerable<SurrealObject> relations, CancellationToken ct = default)
    {
        EnsureLive();
        var arr = new SurrealList();
        foreach (var r in relations) arr.Add(new SurrealObjectValue(r));
        var vars = new SurrealObject
        {
            ["_table"] = new SurrealTableValue(new SurrealTable(edgeTable)),
            ["_records"] = new SurrealListValue(arr),
        };
        return client.ResourceQueryAsync("INSERT RELATION INTO $_table $_records", vars, Id, ct);
    }

    /// <summary>
    /// Create a graph edge inside this transaction:
    /// <c>RELATE source -&gt; edgeTable -&gt; target [CONTENT { ... }]</c>.
    /// </summary>
    public Task<SurrealValue> RelateAsync(
        ISurrealRecordId source,
        string edgeTable,
        ISurrealRecordId target,
        SurrealObject? content = null,
        CancellationToken ct = default)
    {
        EnsureLive();
        if (!IdentifierRegex().IsMatch(edgeTable))
            throw new ArgumentException(
                $"'{edgeTable}' is not a valid SurrealQL identifier.", nameof(edgeTable));
        var sql = content is null
            ? $"RELATE $_source->{edgeTable}->$_target"
            : $"RELATE $_source->{edgeTable}->$_target CONTENT $_content";
        var vars = new SurrealObject
        {
            ["_source"] = new SurrealRecordIdValue(source.ToRecordId()),
            ["_target"] = new SurrealRecordIdValue(target.ToRecordId()),
        };
        if (content is not null) vars["_content"] = new SurrealObjectValue(content);
        return client.ResourceQueryAsync(sql, vars, Id, ct);
    }

    /// <summary>Commit the transaction. Subsequent operations on this handle throw.</summary>
    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref settled, 1) != 0)
            throw new InvalidOperationException("Transaction already settled.");
        await surrealConnection.SendAsync(new CommitCommand(Id), ct).ConfigureAwait(false);
    }

    /// <summary>Cancel the transaction (rollback). Subsequent operations on this handle throw.</summary>
    public async Task CancelAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref settled, 1) != 0)
            throw new InvalidOperationException("Transaction already settled.");
        await surrealConnection.SendAsync(new CancelCommand(Id), ct).ConfigureAwait(false);
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
        if (Interlocked.Exchange(ref settled, 1) == 0 && surrealConnection.IsConnected)
        {
            try
            {
                await surrealConnection.SendAsync(new CancelCommand(Id), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // best-effort; the server will GC the transaction on session close
            }
        }
    }

    [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex IdentifierRegex();
}
