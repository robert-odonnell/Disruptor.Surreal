using System.Text.RegularExpressions;
using System.Threading.Channels;
using Disruptor.Surreal.Auth;
using Disruptor.Surreal.Connection;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal;

/// <summary>
/// The SurrealDB client. Open one with <see cref="ConnectAsync(string, SurrealConnectionConfig?, CancellationToken)"/>;
/// the instance is thread-safe and a single instance can be reused across the app.
/// </summary>
public sealed partial class SurrealClient(ISurrealConnection surrealConnection) : IAsyncDisposable
{
    /// <summary>
    /// Create a client around a caller-supplied <see cref="ISurrealConnection"/>. The intended
    /// use is testing — supply a fake/mock connection so unit tests can drive the
    /// SDK without standing up a real WebSocket. Production callers should prefer
    /// <see cref="ConnectAsync(string, SurrealConnectionConfig?, CancellationToken)"/>.
    /// </summary>
    private readonly ISurrealConnection surrealConnection = surrealConnection ?? throw new ArgumentNullException(nameof(surrealConnection));
    
    /// <summary>Opens a WebSocket connection to <paramref name="url"/> and returns a connected client.</summary>
    /// <remarks>
    /// After the WebSocket handshake the client calls <c>version</c> and verifies the
    /// reported version against <see cref="SupportedVersion.Range"/>. Mismatches throw
    /// <see cref="SurrealConnectionException"/>. Set <see cref="SurrealConnectionConfig.SkipVersionCheck"/>
    /// to opt out (rare; mostly for development builds).
    /// </remarks>
    public static async Task<SurrealClient> ConnectAsync(
        string url,
        SurrealConnectionConfig? config = null,
        CancellationToken ct = default)
    {
        var endpoint = SurrealEndpoint.Parse(url, config);
        var conn = await SurrealWebSocketConnection.ConnectAsync(endpoint, ct).ConfigureAwait(false);
        var client = new SurrealClient(conn);
        if (!endpoint.Config.SkipVersionCheck)
        {
            try
            {
                await client.EnsureSupportedServerVersionAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await client.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        return client;
    }

    /// <summary>
    /// Calls the <c>version</c> RPC and throws <see cref="SurrealConnectionException"/>
    /// if the server's major version is outside <see cref="SupportedVersion"/>.
    /// </summary>
    private async Task EnsureSupportedServerVersionAsync(CancellationToken ct)
    {
        var raw = await VersionAsync(ct).ConfigureAwait(false);
        ServerVersion parsed;
        try
        {
            parsed = ServerVersion.Parse(raw);
        }
        catch (FormatException ex)
        {
            throw new SurrealConnectionException(
                $"Could not parse server version '{raw}'.", ex);
        }
        if (!SupportedVersion.IsSupported(parsed))
        {
            throw new SurrealConnectionException(
                $"Server version {parsed} is outside the supported range {SupportedVersion.Range}.");
        }
    }

    /// <summary>
    /// One-shot connect: opens the WebSocket, signs in if credentials are present in
    /// <paramref name="options"/>, and switches to the configured namespace/database.
    /// </summary>
    public static async Task<SurrealClient> ConnectAsync(
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
        await surrealConnection.SendAsync(new UseCommand(@namespace, database), ct).ConfigureAwait(false);
    }

    /// <summary>Switch the namespace for the current session.</summary>
    public Task UseNsAsync(string @namespace, CancellationToken ct = default) =>
        UseAsync(@namespace, null, ct);

    /// <summary>Switch the database for the current session.</summary>
    public Task UseDbAsync(string database, CancellationToken ct = default) =>
        UseAsync(null, database, ct);

    // ─── Auth ───────────────────────────────────────────────────────────────────

    /// <summary>The most recently issued auth token, or <c>null</c> when not signed in.</summary>
    /// <remarks>
    /// Updated by <see cref="SigninAsync"/>, <see cref="SignupAsync"/>, and <see cref="RefreshAsync"/>;
    /// cleared by <see cref="InvalidateAsync"/>.
    /// </remarks>
    public SurrealToken? CurrentToken { get; private set; }

    /// <summary>
    /// Sign in with the given credentials and return the issued <see cref="SurrealToken"/>
    /// (access token, plus an optional refresh token when the access method has refresh
    /// rotation enabled).
    /// </summary>
    /// <remarks>
    /// On success the connection captures these credentials so that any subsequent RPC
    /// failing with a token-expired auth error transparently refreshes (when a refresh
    /// token is held) or re-signs in (otherwise) and retries the original request once.
    /// Pass new credentials to override; <see cref="InvalidateAsync(CancellationToken)"/>
    /// clears them.
    /// </remarks>
    public async Task<SurrealToken> SigninAsync(ISurrealCredentials surrealCredentials, CancellationToken ct = default)
    {
        var response = await surrealConnection
            .SendAsync(new SigninCommand(surrealCredentials.ToObject()), ct)
            .ConfigureAwait(false);

        var token = SurrealToken.FromValue(response);
        CurrentToken = token;
        InstallReauthHandler(surrealCredentials);
        return token;
    }

    /// <summary>
    /// Sign up a new user (typically record-scope) with the given credentials and return
    /// the issued <see cref="SurrealToken"/>. Most commonly used with <see cref="SurrealRecord"/>
    /// credentials whose access method defines a <c>SIGNUP</c> query.
    /// </summary>
    public async Task<SurrealToken> SignupAsync(ISurrealCredentials surrealCredentials, CancellationToken ct = default)
    {
        var response = await surrealConnection
            .SendAsync(new SignupCommand(surrealCredentials.ToObject()), ct)
            .ConfigureAwait(false);

        var token = SurrealToken.FromValue(response);
        CurrentToken = token;
        InstallReauthHandler(surrealCredentials);
        return token;
    }

    /// <summary>
    /// Exchange a refresh token for a new <see cref="SurrealToken"/>. The argument must include
    /// both <c>access</c> and <c>refresh</c> components — the server-side refresh flow
    /// requires the full token shape.
    /// </summary>
    /// <remarks>
    /// Mirrors the Rust client's <c>Command::Refresh</c> (<c>src/conn/cmd.rs</c>):
    /// sends the entire token structure as <c>params[0]</c> to the <c>refresh</c> RPC.
    /// </remarks>
    public async Task<SurrealToken> RefreshAsync(SurrealToken surrealToken, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(surrealToken);
        if (surrealToken.Refresh is null)
            throw new InvalidOperationException(
                "RefreshAsync requires a Token carrying both access and refresh components.");

        var response = await surrealConnection
            .SendAsync(new RefreshCommand(surrealToken), ct)
            .ConfigureAwait(false);

        var renewed = SurrealToken.FromValue(response);
        CurrentToken = renewed;
        return renewed;
    }

    /// <summary>
    /// Install the reauth handler used by the connection on token-expiry errors.
    /// Prefers the refresh-token flow when available; falls back to re-signin.
    /// </summary>
    private void InstallReauthHandler(ISurrealCredentials surrealCredentials)
    {
        surrealConnection.ReauthHandler = async innerCt =>
        {
            // Prefer refresh-token rotation if the most recent token carries one.
            var existing = CurrentToken;
            if (existing?.Refresh is not null)
            {
                try
                {
                    var renewed = await surrealConnection
                        .SendAsync(new RefreshCommand(existing), innerCt)
                        .ConfigureAwait(false);
                    CurrentToken = SurrealToken.FromValue(renewed);
                    return;
                }
                catch (SurrealAuthException)
                {
                    // Refresh failed — fall through to credentials-based re-signin.
                }
            }

            var response = await surrealConnection
                .SendAsync(new SigninCommand(surrealCredentials.ToObject()), innerCt)
                .ConfigureAwait(false);
            CurrentToken = SurrealToken.FromValue(response);
        };
    }

    /// <summary>Authenticate the current session with a previously issued JWT.</summary>
    public Task AuthenticateAsync(string token, CancellationToken ct = default) =>
        surrealConnection.SendAsync(new AuthenticateCommand(token), ct);

    /// <summary>Invalidate the current session's authentication.</summary>
    public async Task InvalidateAsync(CancellationToken ct = default)
    {
        try
        {
            await surrealConnection.SendAsync(new InvalidateCommand(), ct).ConfigureAwait(false);
        }
        finally
        {
            // Clear cached credentials and the captured token so we don't silently
            // re-auth after explicit invalidation.
            surrealConnection.ReauthHandler = null;
            CurrentToken = null;
        }
    }

    // ─── Variables ──────────────────────────────────────────────────────────────

    /// <summary>Define a session variable available to subsequent queries as <c>$key</c>.</summary>
    public Task SetAsync(string key, SurrealValue surrealValue, CancellationToken ct = default) =>
        surrealConnection.SendAsync(new SetCommand(key, surrealValue), ct);

    /// <summary>Remove a previously-set session variable.</summary>
    public Task UnsetAsync(string key, CancellationToken ct = default) =>
        surrealConnection.SendAsync(new UnsetCommand(key), ct);

    // ─── Health / Version ───────────────────────────────────────────────────────

    /// <summary>Returns true when the server responded to a ping.</summary>
    public async Task<bool> HealthAsync(CancellationToken ct = default)
    {
        try
        {
            await surrealConnection.SendAsync(new HealthCommand(), ct).ConfigureAwait(false);
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
        var response = await surrealConnection.SendAsync(new VersionCommand(), ct).ConfigureAwait(false);
        return response is StringSurrealValue s ? s.Value : response.ToString() ?? string.Empty;
    }

    // ─── Query ──────────────────────────────────────────────────────────────────

    /// <summary>Run a (possibly multi-statement) SQL query with optional bindings.</summary>
    public async Task<SurrealQueryResponse> QueryAsync(
        string sql,
        SurrealObject? bindings = null,
        CancellationToken ct = default)
    {
        var raw = await QueryRawAsync(sql, bindings, txn: null, ct).ConfigureAwait(false);
        return SurrealQueryResponse.FromValue(raw);
    }

    internal async Task<SurrealValue> QueryRawAsync(
        string sql,
        SurrealObject? bindings,
        Guid? txn,
        CancellationToken ct)
    {
        return await surrealConnection
            .SendAsync(new QueryCommand(sql, bindings, txn), ct)
            .ConfigureAwait(false);
    }

    // ─── Resource-shaped operations ─────────────────────────────────────────────
    // All translate to a SQL query (matching the Rust SDK's approach). One result
    // statement is unwrapped to the inner Value.

    /// <summary>Select all records from a table.</summary>
    public Task<SurrealValue> SelectAsync(string table, CancellationToken ct = default) =>
        ResourceQueryAsync($"SELECT * FROM $_table", ResourceVars(table), txn: null, ct);

    /// <summary>Select a single record by id.</summary>
    public Task<SurrealValue> SelectAsync(ISurrealRecordId id, CancellationToken ct = default) =>
        ResourceQueryAsync("SELECT * FROM $_record_id", ResourceVars(id.ToRecordId()), txn: null, ct);

    /// <summary>Create a record on a table (auto-generated id).</summary>
    public Task<SurrealValue> CreateAsync(string table, SurrealObject? content = null, CancellationToken ct = default) =>
        ResourceQueryAsync(
            content is null ? "CREATE $_table" : "CREATE $_table CONTENT $_content",
            ResourceVars(table, content),
            txn: null, ct);

    /// <summary>Create a record at a specific id.</summary>
    public Task<SurrealValue> CreateAsync(ISurrealRecordId id, SurrealObject? content = null, CancellationToken ct = default) =>
        ResourceQueryAsync(
            content is null ? "CREATE $_record_id" : "CREATE $_record_id CONTENT $_content",
            ResourceVars(id.ToRecordId(), content),
            txn: null, ct);

    /// <summary>Replace records on a table with the given content.</summary>
    public Task<SurrealValue> UpdateAsync(string table, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_table CONTENT $_content",
            ResourceVars(table, content), txn: null, ct);

    /// <summary>Replace a record with the given content.</summary>
    public Task<SurrealValue> UpdateAsync(ISurrealRecordId id, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_record_id CONTENT $_content",
            ResourceVars(id.ToRecordId(), content), txn: null, ct);

    /// <summary>Delete all records on a table.</summary>
    public Task<SurrealValue> DeleteAsync(string table, CancellationToken ct = default) =>
        ResourceQueryAsync("DELETE $_table RETURN BEFORE", ResourceVars(table), txn: null, ct);

    /// <summary>Delete a single record by id.</summary>
    public Task<SurrealValue> DeleteAsync(ISurrealRecordId id, CancellationToken ct = default) =>
        ResourceQueryAsync("DELETE $_record_id RETURN BEFORE", ResourceVars(id.ToRecordId()), txn: null, ct);

    /// <summary>Upsert records on a table (create-or-replace) with the given content.</summary>
    public Task<SurrealValue> UpsertAsync(string table, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPSERT $_table CONTENT $_content",
            ResourceVars(table, content), txn: null, ct);

    /// <summary>Upsert a record at a specific id.</summary>
    public Task<SurrealValue> UpsertAsync(ISurrealRecordId id, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPSERT $_record_id CONTENT $_content",
            ResourceVars(id.ToRecordId(), content), txn: null, ct);

    /// <summary>Merge fields into existing records on a table (partial update).</summary>
    public Task<SurrealValue> MergeAsync(string table, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_table MERGE $_content",
            ResourceVars(table, content), txn: null, ct);

    /// <summary>Merge fields into a single existing record (partial update).</summary>
    public Task<SurrealValue> MergeAsync(ISurrealRecordId id, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_record_id MERGE $_content",
            ResourceVars(id.ToRecordId(), content), txn: null, ct);

    /// <summary>Apply JSON-Patch operations to records on a table. Use <see cref="Patch"/> to author ops.</summary>
    public Task<SurrealValue> PatchAsync(string table, IEnumerable<SurrealObject> patches, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_table PATCH $_patches", PatchVars(table, patches), txn: null, ct);

    /// <summary>Apply JSON-Patch operations to a single record. Use <see cref="Patch"/> to author ops.</summary>
    public Task<SurrealValue> PatchAsync(ISurrealRecordId id, IEnumerable<SurrealObject> patches, CancellationToken ct = default) =>
        ResourceQueryAsync("UPDATE $_record_id PATCH $_patches", PatchVars(id.ToRecordId(), patches), txn: null, ct);

    /// <summary>Insert a single record into a table. Returns the inserted record (server may auto-assign an id).</summary>
    public Task<SurrealValue> InsertAsync(string table, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("INSERT INTO $_table $_content",
            ResourceVars(table, content), txn: null, ct);

    /// <summary>Insert multiple records into a table in one call.</summary>
    public Task<SurrealValue> InsertAsync(string table, IEnumerable<SurrealObject> records, CancellationToken ct = default) =>
        ResourceQueryAsync("INSERT INTO $_table $_records", InsertManyVars(table, records), txn: null, ct);

    /// <summary>
    /// Insert one graph-edge record into <paramref name="edgeTable"/>. <paramref name="content"/> must
    /// supply <c>in</c> and <c>out</c> fields (record ids).
    /// </summary>
    public Task<SurrealValue> InsertRelationAsync(string edgeTable, SurrealObject content, CancellationToken ct = default) =>
        ResourceQueryAsync("INSERT RELATION INTO $_table $_content",
            ResourceVars(edgeTable, content), txn: null, ct);

    /// <summary>Insert multiple graph-edge records into <paramref name="edgeTable"/> in one call.</summary>
    public Task<SurrealValue> InsertRelationAsync(string edgeTable, IEnumerable<SurrealObject> relations, CancellationToken ct = default) =>
        ResourceQueryAsync("INSERT RELATION INTO $_table $_records", InsertManyVars(edgeTable, relations), txn: null, ct);

    /// <summary>
    /// Create a single graph edge: <c>RELATE source -&gt; edgeTable -&gt; target [CONTENT { ... }]</c>.
    /// </summary>
    public Task<SurrealValue> RelateAsync(
        ISurrealRecordId source,
        string edgeTable,
        ISurrealRecordId target,
        SurrealObject? content = null,
        CancellationToken ct = default)
    {
        ValidateIdentifier(edgeTable, nameof(edgeTable));
        var sql = content is null
            ? $"RELATE $_source->{edgeTable}->$_target"
            : $"RELATE $_source->{edgeTable}->$_target CONTENT $_content";
        var vars = new SurrealObject
        {
            ["_source"] = new SurrealRecordIdValue(source.ToRecordId()),
            ["_target"] = new SurrealRecordIdValue(target.ToRecordId()),
        };
        if (content is not null) vars["_content"] = new SurrealObjectValue(content);
        return ResourceQueryAsync(sql, vars, txn: null, ct);
    }

    /// <summary>
    /// Execute a server-side function (defined via <c>DEFINE FUNCTION</c>). Returns whatever
    /// the function returns. Function name may include <c>::</c> namespace separators
    /// (e.g. <c>fn::math::add</c> is passed as <c>"math::add"</c>).
    /// </summary>
    public async Task<SurrealValue> RunAsync(
        string name,
        IReadOnlyList<SurrealValue>? args = null,
        string? version = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return await surrealConnection
            .SendAsync(new RunCommand(name, version, args ?? []), ct)
            .ConfigureAwait(false);
    }

    // ─── Live queries ──────────────────────────────────────────────────────────

    /// <summary>
    /// Subscribe to a live query and return a handle whose <c>await foreach</c> yields
    /// <see cref="SurrealNotification"/>s as the server pushes them.
    /// </summary>
    /// <remarks>
    /// <paramref name="sql"/> is auto-prefixed with <c>LIVE </c> when it doesn't already
    /// start with the keyword. Disposing the handle (or letting the connection drop)
    /// stops the subscription — disposal sends a best-effort <c>kill</c> RPC.
    /// </remarks>
    public async Task<SurrealLiveQueryHandle> LiveAsync(
        string sql,
        SurrealObject? bindings = null,
        SurrealLiveQueryOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        options ??= new SurrealLiveQueryOptions();

        // Auto-prefix LIVE if absent so callers can just write a SELECT.
        var trimmed = sql.TrimStart();
        var liveSql = trimmed.StartsWith("LIVE ", StringComparison.OrdinalIgnoreCase)
            ? sql
            : "LIVE " + sql;

        // Run the LIVE SELECT through the standard query pipeline; the server returns
        // the assigned live-query UUID as the single statement's result.
        var raw = await surrealConnection
            .SendAsync(new QueryCommand(liveSql, bindings, Txn: null), ct)
            .ConfigureAwait(false);

        var unwrapped = UnwrapSingleStatement(raw);
        if (unwrapped is not SurrealUuidValue { Value: var liveQueryId })
            throw new SurrealProtocolException(
                $"LIVE SELECT result was not a UUID; got {unwrapped.Kind}.");

        // Build the per-subscription channel + register so the receive loop can
        // dispatch incoming notifications to it. Inner channel is always Wait-mode;
        // the wrapper writer drives the user-chosen FullMode (DropNewest by default)
        // so we can observe and count drops (which BCL drop-modes don't expose).
        var channel = Channel.CreateBounded<SurrealNotification>(
            new BoundedChannelOptions(options.Capacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait,
            });
        var dropped = new DroppedCounter();
        var observingWriter = new DroppedCountingChannelWriter(channel, dropped, options.FullMode);

        try
        {
            surrealConnection.RegisterLiveSubscription(liveQueryId, observingWriter);
        }
        catch
        {
            channel.Writer.TryComplete();
            // Best-effort kill so the server doesn't keep buffering for a query nobody owns.
            try { await surrealConnection.SendAsync(new KillCommand(liveQueryId), ct).ConfigureAwait(false); }
            catch { /* swallow */ }
            throw;
        }

        return new SurrealLiveQueryHandle(surrealConnection, liveQueryId, channel.Reader, dropped);
    }

    // ─── Transactions ──────────────────────────────────────────────────────────

    /// <summary>
    /// Begin a server-side transaction and return a handle whose statements all run
    /// inside it. Call <see cref="SurrealTransaction.CommitAsync"/> to commit, or
    /// <see cref="SurrealTransaction.CancelAsync"/> to roll back. Disposing without committing
    /// auto-cancels.
    /// </summary>
    public async Task<SurrealTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var raw = await surrealConnection.SendAsync(new BeginCommand(), ct).ConfigureAwait(false);
        // Server returns the transaction id as a Uuid.
        if (raw is not SurrealUuidValue { Value: var txnId })
            throw new SurrealProtocolException(
                $"BEGIN response was not a UUID: {raw}");
        return new SurrealTransaction(this, surrealConnection, txnId);
    }

    // ─── Internals ─────────────────────────────────────────────────────────────

    internal async Task<SurrealValue> ResourceQueryAsync(
        string sql, SurrealObject vars, Guid? txn, CancellationToken ct)
    {
        var raw = await surrealConnection
            .SendAsync(new QueryCommand(sql, vars, txn), ct)
            .ConfigureAwait(false);

        return UnwrapSingleStatement(raw);
    }

    /// <summary>
    /// Server returns the query() result as an <see cref="SurrealListValue"/> of
    /// <c>{ status, result, ... }</c> objects. For single-statement helpers, unwrap
    /// the lone <c>result</c>.
    /// </summary>
    internal static SurrealValue UnwrapSingleStatement(SurrealValue raw)
    {
        if (raw is not SurrealListValue { List: var arr } || arr.Count != 1)
            return raw;

        if (arr[0] is not SurrealObjectValue { Object: var stmt })
            return arr[0];

        if (stmt.TryGetValue("status", out var status)
            && status is StringSurrealValue { Value: "ERR" })
        {
            var msg = stmt.TryGetValue("result", out var r) && r is StringSurrealValue rs
                ? rs.Value
                : "Statement failed";
            throw new SurrealRpcException(0, msg);
        }

        return stmt.TryGetValue("result", out var result) ? result : SurrealValue.None;
    }

    private static SurrealObject ResourceVars(string table, SurrealObject? content = null)
    {
        var o = new SurrealObject { ["_table"] = new SurrealTableValue(new SurrealTable(table)) };
        if (content is not null) o["_content"] = new SurrealObjectValue(content);
        return o;
    }

    private static SurrealObject ResourceVars(SurrealRecordId id, SurrealObject? content = null)
    {
        var o = new SurrealObject { ["_record_id"] = new SurrealRecordIdValue(id) };
        if (content is not null) o["_content"] = new SurrealObjectValue(content);
        return o;
    }

    private static SurrealObject PatchVars(string table, IEnumerable<SurrealObject> patches)
    {
        var arr = new SurrealList();
        foreach (var p in patches) arr.Add(new SurrealObjectValue(p));
        return new SurrealObject
        {
            ["_table"] = new SurrealTableValue(new SurrealTable(table)),
            ["_patches"] = new SurrealListValue(arr),
        };
    }

    private static SurrealObject PatchVars(SurrealRecordId id, IEnumerable<SurrealObject> patches)
    {
        var arr = new SurrealList();
        foreach (var p in patches) arr.Add(new SurrealObjectValue(p));
        return new SurrealObject
        {
            ["_record_id"] = new SurrealRecordIdValue(id),
            ["_patches"] = new SurrealListValue(arr),
        };
    }

    private static SurrealObject InsertManyVars(string table, IEnumerable<SurrealObject> records)
    {
        var arr = new SurrealList();
        foreach (var r in records) arr.Add(new SurrealObjectValue(r));
        return new SurrealObject
        {
            ["_table"] = new SurrealTableValue(new SurrealTable(table)),
            ["_records"] = new SurrealListValue(arr),
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
        if (!IdentifierRegex().IsMatch(value))
            throw new ArgumentException(
                $"'{value}' is not a valid SurrealQL identifier; must match [a-zA-Z_][a-zA-Z0-9_]*.",
                paramName);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => surrealConnection.DisposeAsync();

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex IdentifierRegex();
}
