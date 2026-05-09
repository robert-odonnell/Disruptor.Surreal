using Disruptor.Surreal.Auth;
using Disruptor.Surreal.Connection;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal;

/// <summary>
/// The SurrealDB client. Open one with <see cref="ConnectAsync(string, ConnectionConfig?, CancellationToken)"/>;
/// the instance is thread-safe and a single instance can be reused across the app.
/// </summary>
public sealed class Surreal : IAsyncDisposable
{
    private readonly IConnection _connection;

    internal Surreal(IConnection connection) => _connection = connection;

    /// <summary>Opens a WebSocket connection to <paramref name="url"/> and returns a connected client.</summary>
    public static async Task<Surreal> ConnectAsync(
        string url,
        ConnectionConfig? config = null,
        CancellationToken ct = default)
    {
        var endpoint = Endpoint.Parse(url, config);
        var conn = await WebSocketConnection.ConnectAsync(endpoint, ct).ConfigureAwait(false);
        return new Surreal(conn);
    }

    /// <summary>
    /// One-shot connect: opens the WebSocket, signs in if credentials are present in
    /// <paramref name="options"/>, and switches to the configured namespace/database.
    /// </summary>
    public static async Task<Surreal> ConnectAsync(
        SurrealOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var db = await ConnectAsync(options.Url, options.Config, ct).ConfigureAwait(false);
        try
        {
            if (options.BuildCredentials() is { } creds)
                await db.SigninAsync(creds, ct).ConfigureAwait(false);

            if (options.Namespace is not null || options.Database is not null)
                await db.UseAsync(options.Namespace, options.Database, ct).ConfigureAwait(false);

            return db;
        }
        catch
        {
            await db.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }


    // ─── Use ────────────────────────────────────────────────────────────────────

    /// <summary>Switch the namespace and/or database for the current session.</summary>
    public async Task UseAsync(string? @namespace, string? database, CancellationToken ct = default)
    {
        await _connection.SendAsync(new UseCommand(@namespace, database), ct).ConfigureAwait(false);
    }

    /// <summary>Switch the namespace for the current session.</summary>
    public Task UseNsAsync(string @namespace, CancellationToken ct = default) =>
        UseAsync(@namespace, null, ct);

    /// <summary>Switch the database for the current session.</summary>
    public Task UseDbAsync(string database, CancellationToken ct = default) =>
        UseAsync(null, database, ct);

    // ─── Auth ───────────────────────────────────────────────────────────────────

    /// <summary>Sign in with the given credentials and return the issued access token.</summary>
    /// <remarks>
    /// On success the connection captures these credentials so that any subsequent RPC
    /// failing with a token-expired auth error transparently re-signs in and retries once.
    /// Pass new credentials to override; <see cref="InvalidateAsync(CancellationToken)"/>
    /// clears them.
    /// </remarks>
    public async Task<AccessToken> SigninAsync(ICredentials credentials, CancellationToken ct = default)
    {
        var response = await _connection
            .SendAsync(new SigninCommand(credentials.ToObject()), ct)
            .ConfigureAwait(false);

        // Capture credentials for transparent re-auth on token expiry.
        _connection.ReauthHandler = async innerCt =>
        {
            await _connection
                .SendAsync(new SigninCommand(credentials.ToObject()), innerCt)
                .ConfigureAwait(false);
        };

        return ExtractToken(response);
    }

    /// <summary>
    /// Sign up a new user (typically record-scope) with the given credentials and return
    /// the issued access token. Most commonly used with <see cref="Auth.Record"/>
    /// credentials whose access method defines a <c>SIGNUP</c> query.
    /// </summary>
    public async Task<AccessToken> SignupAsync(ICredentials credentials, CancellationToken ct = default)
    {
        var response = await _connection
            .SendAsync(new SignupCommand(credentials.ToObject()), ct)
            .ConfigureAwait(false);

        // Successful signup also establishes a session — capture for transparent re-auth.
        _connection.ReauthHandler = async innerCt =>
        {
            await _connection
                .SendAsync(new SigninCommand(credentials.ToObject()), innerCt)
                .ConfigureAwait(false);
        };

        return ExtractToken(response);
    }

    /// <summary>Authenticate the current session with a previously issued JWT.</summary>
    public Task AuthenticateAsync(string token, CancellationToken ct = default) =>
        _connection.SendAsync(new AuthenticateCommand(token), ct);

    /// <summary>Invalidate the current session's authentication.</summary>
    public async Task InvalidateAsync(CancellationToken ct = default)
    {
        try
        {
            await _connection.SendAsync(new InvalidateCommand(), ct).ConfigureAwait(false);
        }
        finally
        {
            // Clear cached credentials so we don't silently re-auth after explicit invalidation.
            _connection.ReauthHandler = null;
        }
    }

    // ─── Variables ──────────────────────────────────────────────────────────────

    /// <summary>Define a session variable available to subsequent queries as <c>$key</c>.</summary>
    public Task SetAsync(string key, Value value, CancellationToken ct = default) =>
        _connection.SendAsync(new SetCommand(key, value), ct);

    /// <summary>Remove a previously-set session variable.</summary>
    public Task UnsetAsync(string key, CancellationToken ct = default) =>
        _connection.SendAsync(new UnsetCommand(key), ct);

    // ─── Health / Version ───────────────────────────────────────────────────────

    /// <summary>Returns true when the server responded to a ping.</summary>
    public async Task<bool> HealthAsync(CancellationToken ct = default)
    {
        try
        {
            await _connection.SendAsync(new HealthCommand(), ct).ConfigureAwait(false);
            return true;
        }
        catch (SurrealException)
        {
            return false;
        }
    }

    /// <summary>Returns the version string reported by the server.</summary>
    public async Task<string> VersionAsync(CancellationToken ct = default)
    {
        var response = await _connection.SendAsync(new VersionCommand(), ct).ConfigureAwait(false);
        return response is StringValue s ? s.Value : response.ToString() ?? string.Empty;
    }

    // ─── Query ──────────────────────────────────────────────────────────────────

    /// <summary>Run a (possibly multi-statement) SQL query with optional bindings.</summary>
    public async Task<QueryResponse> QueryAsync(
        string sql,
        SurrealObject? bindings = null,
        CancellationToken ct = default)
    {
        var raw = await QueryRawAsync(sql, bindings, txn: null, ct).ConfigureAwait(false);
        return QueryResponse.FromValue(raw);
    }

    internal async Task<Value> QueryRawAsync(
        string sql,
        SurrealObject? bindings,
        Guid? txn,
        CancellationToken ct)
    {
        return await _connection
            .SendAsync(new QueryCommand(sql, bindings, txn), ct)
            .ConfigureAwait(false);
    }

    // ─── Resource-shaped operations ─────────────────────────────────────────────
    // All translate to a SQL query (matching the Rust SDK's approach). One result
    // statement is unwrapped to the inner Value.

    /// <summary>Select all records from a table.</summary>
    public Task<Value> SelectAsync(string table, CancellationToken ct = default) =>
        ResourceQueryAsync($"SELECT * FROM $_table", ResourceVars(table), txn: null, ct);

    /// <summary>Select a single record by id.</summary>
    public Task<Value> SelectAsync(IRecordId id, CancellationToken ct = default) =>
        ResourceQueryAsync("SELECT * FROM $_record_id", ResourceVars(id.ToRecordId()), txn: null, ct);

    /// <summary>Create a record on a table (auto-generated id).</summary>
    public Task<Value> CreateAsync(string table, SurrealObject? content = null, CancellationToken ct = default) =>
        ResourceQueryAsync(
            content is null ? "CREATE $_table" : "CREATE $_table CONTENT $_content",
            ResourceVars(table, content),
            txn: null, ct);

    /// <summary>Create a record at a specific id.</summary>
    public Task<Value> CreateAsync(IRecordId id, SurrealObject? content = null, CancellationToken ct = default) =>
        ResourceQueryAsync(
            content is null ? "CREATE $_record_id" : "CREATE $_record_id CONTENT $_content",
            ResourceVars(id.ToRecordId(), content),
            txn: null, ct);

    /// <summary>Replace records on a table with the given content.</summary>
    public Task<Value> UpdateAsync(string table, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_table CONTENT $_content",
            ResourceVars(table, content), txn: null, ct);

    /// <summary>Replace a record with the given content.</summary>
    public Task<Value> UpdateAsync(IRecordId id, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_record_id CONTENT $_content",
            ResourceVars(id.ToRecordId(), content), txn: null, ct);

    /// <summary>Delete all records on a table.</summary>
    public Task<Value> DeleteAsync(string table, CancellationToken ct = default) =>
        ResourceQueryAsync("DELETE $_table RETURN BEFORE", ResourceVars(table), txn: null, ct);

    /// <summary>Delete a single record by id.</summary>
    public Task<Value> DeleteAsync(IRecordId id, CancellationToken ct = default) =>
        ResourceQueryAsync("DELETE $_record_id RETURN BEFORE", ResourceVars(id.ToRecordId()), txn: null, ct);

    /// <summary>Upsert records on a table (create-or-replace) with the given content.</summary>
    public Task<Value> UpsertAsync(string table, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPSERT $_table CONTENT $_content",
            ResourceVars(table, content), txn: null, ct);

    /// <summary>Upsert a record at a specific id.</summary>
    public Task<Value> UpsertAsync(IRecordId id, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPSERT $_record_id CONTENT $_content",
            ResourceVars(id.ToRecordId(), content), txn: null, ct);

    /// <summary>Merge fields into existing records on a table (partial update).</summary>
    public Task<Value> MergeAsync(string table, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_table MERGE $_content",
            ResourceVars(table, content), txn: null, ct);

    /// <summary>Merge fields into a single existing record (partial update).</summary>
    public Task<Value> MergeAsync(IRecordId id, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_record_id MERGE $_content",
            ResourceVars(id.ToRecordId(), content), txn: null, ct);

    /// <summary>Apply JSON-Patch operations to records on a table. Use <see cref="Patch"/> to author ops.</summary>
    public Task<Value> PatchAsync(string table, IEnumerable<SurrealObject> patches, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_table PATCH $_patches", PatchVars(table, patches), txn: null, ct);

    /// <summary>Apply JSON-Patch operations to a single record. Use <see cref="Patch"/> to author ops.</summary>
    public Task<Value> PatchAsync(IRecordId id, IEnumerable<SurrealObject> patches, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_record_id PATCH $_patches", PatchVars(id.ToRecordId(), patches), txn: null, ct);

    /// <summary>Insert a single record into a table. Returns the inserted record (server may auto-assign an id).</summary>
    public Task<Value> InsertAsync(string table, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("INSERT INTO $_table $_content",
            ResourceVars(table, content), txn: null, ct);

    /// <summary>Insert multiple records into a table in one call.</summary>
    public Task<Value> InsertAsync(string table, IEnumerable<SurrealObject> records, CancellationToken ct = default) =>
        ResourceQueryAsync("INSERT INTO $_table $_records", InsertManyVars(table, records), txn: null, ct);

    /// <summary>
    /// Insert one graph-edge record into <paramref name="edgeTable"/>. <paramref name="content"/> must
    /// supply <c>in</c> and <c>out</c> fields (record ids).
    /// </summary>
    public Task<Value> InsertRelationAsync(string edgeTable, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("INSERT RELATION INTO $_table $_content",
            ResourceVars(edgeTable, content), txn: null, ct);

    /// <summary>Insert multiple graph-edge records into <paramref name="edgeTable"/> in one call.</summary>
    public Task<Value> InsertRelationAsync(string edgeTable, IEnumerable<SurrealObject> relations, CancellationToken ct = default) =>
        ResourceQueryAsync("INSERT RELATION INTO $_table $_records", InsertManyVars(edgeTable, relations), txn: null, ct);

    /// <summary>
    /// Create a single graph edge: <c>RELATE source -&gt; edgeTable -&gt; target [CONTENT { ... }]</c>.
    /// </summary>
    public Task<Value> RelateAsync(
        IRecordId source,
        string edgeTable,
        IRecordId target,
        SurrealObject? content = null,
        CancellationToken ct = default)
    {
        ValidateIdentifier(edgeTable, nameof(edgeTable));
        var sql = content is null
            ? $"RELATE $_source->{edgeTable}->$_target"
            : $"RELATE $_source->{edgeTable}->$_target CONTENT $_content";
        var vars = new SurrealObject
        {
            ["_source"] = new RecordIdValue(source.ToRecordId()),
            ["_target"] = new RecordIdValue(target.ToRecordId()),
        };
        if (content is not null) vars["_content"] = new ObjectValue(content);
        return ResourceQueryAsync(sql, vars, txn: null, ct);
    }

    /// <summary>
    /// Execute a server-side function (defined via <c>DEFINE FUNCTION</c>). Returns whatever
    /// the function returns. Function name may include <c>::</c> namespace separators
    /// (e.g. <c>fn::math::add</c> is passed as <c>"math::add"</c>).
    /// </summary>
    public async Task<Value> RunAsync(
        string name,
        IReadOnlyList<Value>? args = null,
        string? version = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return await _connection
            .SendAsync(new RunCommand(name, version, args ?? System.Array.Empty<Value>()), ct)
            .ConfigureAwait(false);
    }

    // ─── Transactions ──────────────────────────────────────────────────────────

    /// <summary>
    /// Begin a server-side transaction and return a handle whose statements all run
    /// inside it. Call <see cref="Transaction.CommitAsync"/> to commit, or
    /// <see cref="Transaction.CancelAsync"/> to roll back. Disposing without committing
    /// auto-cancels.
    /// </summary>
    public async Task<Transaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var raw = await _connection.SendAsync(new BeginCommand(), ct).ConfigureAwait(false);
        // Server returns the transaction id as a Uuid.
        if (raw is not UuidValue { Value: var txnId })
            throw new SurrealProtocolException(
                $"BEGIN response was not a UUID: {raw}");
        return new Transaction(this, _connection, txnId);
    }

    // ─── Internals ─────────────────────────────────────────────────────────────

    internal async Task<Value> ResourceQueryAsync(
        string sql, SurrealObject vars, Guid? txn, CancellationToken ct)
    {
        var raw = await _connection
            .SendAsync(new QueryCommand(sql, vars, txn), ct)
            .ConfigureAwait(false);

        return UnwrapSingleStatement(raw);
    }

    /// <summary>
    /// Server returns the query() result as an <see cref="ArrayValue"/> of
    /// <c>{ status, result, ... }</c> objects. For single-statement helpers, unwrap
    /// the lone <c>result</c>.
    /// </summary>
    internal static Value UnwrapSingleStatement(Value raw)
    {
        if (raw is not ArrayValue { Array: var arr } || arr.Count != 1)
            return raw;

        if (arr[0] is not ObjectValue { Object: var stmt })
            return arr[0];

        if (stmt.TryGetValue("status", out var status)
            && status is StringValue { Value: "ERR" })
        {
            var msg = stmt.TryGetValue("result", out var r) && r is StringValue rs
                ? rs.Value
                : "Statement failed";
            throw new SurrealRpcException(0, msg);
        }

        return stmt.TryGetValue("result", out var result) ? result : Value.None;
    }

    private static SurrealObject ResourceVars(string table, SurrealObject? content = null)
    {
        var o = new SurrealObject { ["_table"] = new TableValue(new Table(table)) };
        if (content is not null) o["_content"] = new ObjectValue(content);
        return o;
    }

    private static SurrealObject ResourceVars(RecordId id, SurrealObject? content = null)
    {
        var o = new SurrealObject { ["_record_id"] = new RecordIdValue(id) };
        if (content is not null) o["_content"] = new ObjectValue(content);
        return o;
    }

    private static SurrealObject PatchVars(string table, IEnumerable<SurrealObject> patches)
    {
        var arr = new SurrealArray();
        foreach (var p in patches) arr.Add(new ObjectValue(p));
        return new SurrealObject
        {
            ["_table"] = new TableValue(new Table(table)),
            ["_patches"] = new ArrayValue(arr),
        };
    }

    private static SurrealObject PatchVars(RecordId id, IEnumerable<SurrealObject> patches)
    {
        var arr = new SurrealArray();
        foreach (var p in patches) arr.Add(new ObjectValue(p));
        return new SurrealObject
        {
            ["_record_id"] = new RecordIdValue(id),
            ["_patches"] = new ArrayValue(arr),
        };
    }

    private static SurrealObject InsertManyVars(string table, IEnumerable<SurrealObject> records)
    {
        var arr = new SurrealArray();
        foreach (var r in records) arr.Add(new ObjectValue(r));
        return new SurrealObject
        {
            ["_table"] = new TableValue(new Table(table)),
            ["_records"] = new ArrayValue(arr),
        };
    }

    /// <summary>
    /// Validates that <paramref name="value"/> is a SurrealQL identifier
    /// (<c>[a-zA-Z_][a-zA-Z0-9_]*</c>). Used where the value will be inlined into
    /// SurrealQL rather than passed as a binding (e.g. the edge-table position in
    /// <c>RELATE source-&gt;edge-&gt;target</c>).
    /// </summary>
    private static void ValidateIdentifier(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            throw new ArgumentException(
                $"'{value}' is not a valid SurrealQL identifier; must match [a-zA-Z_][a-zA-Z0-9_]*.",
                paramName);
    }

    private static AccessToken ExtractToken(Value value) => value switch
    {
        StringValue s => new AccessToken(s.Value),
        ObjectValue obj when obj.Object.TryGetValue("access", out var a)
            && a is StringValue accessStr => new AccessToken(accessStr.Value),
        _ => throw new SurrealProtocolException(
            $"Signin response was not a token (got {value.Kind})."),
    };

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
