# Disruptor.Surreal

An .NET 10 client for [SurrealDB](https://surrealdb.com), modeled on the
official Rust client. CBOR over WebSocket. Single package. No embedded mode.

> **Unofficial.** Not affiliated with the SurrealDB project. The
> `Disruptor.Surreal` name keeps that unambiguous. Apache-2.0; contributions
> welcome.

## Why this exists

Targeted to our design goals — not intended as a general-purpose drop-in
replacement for the official SDK.

*(yet. evil laugh.)*

Built alongside [`Disruptor.Surface`](https://github.com/robert-odonnell/Disruptor.Surface), an ORM/source-generator
project whose transport needs drive the v1 surface here: CBOR over WS, typed
bindings, server-side transactions, faithful `Value`-tree round-tripping for
SurrealDB's wire types (`RecordId`, `decimal`, `DateTimeOffset`, `Guid`,
`Datetime` / `Duration` with full nanosecond precision). Embedded is
permanently out — we trust the database.

## Install

The project targets `net10.0`. Add a project reference (no NuGet package yet):

```xml
<ProjectReference Include="path/to/src/Disruptor.Surreal/Disruptor.Surreal.csproj" />
```

## Quick start

```csharp
using Disruptor.Surreal;
using Disruptor.Surreal.Auth;
using Disruptor.Surreal.Connection;
using Disruptor.Surreal.Values;

// One-shot connect: parse the connection string, dial WS, signin, switch ns/db.
await using var db = await Surreal.ConnectAsync(SurrealOptions.Parse(
    "Url=ws://localhost:8000;Namespace=test;Database=test;User=root;Password=root"));

var jaime = new RecordId("person", "jaime");

// Create a record at a known id
await db.CreateAsync(jaime, new SurrealObject
{
    ["name"] = "Jaime",
    ["age"] = 30L,
    ["joined"] = DateTimeOffset.UtcNow,   // CBOR tag 12
    ["balance"] = 1234.56m,                // CBOR tag 10
    ["session"] = Guid.NewGuid(),          // CBOR tag 37
});

// Multi-statement query with bindings
var response = await db.QueryAsync(
    "SELECT * FROM person WHERE age >= $minAge",
    new SurrealObject { ["minAge"] = 21L });
var rows = response.Take(0);  // Value (an ArrayValue of ObjectValue)

// Server-side transaction with rollback
await using var tx = await db.BeginTransactionAsync();
await tx.UpdateAsync(jaime, new SurrealObject { ["balance"] = 9999m });
await tx.CommitAsync();   // or tx.CancelAsync() to roll back
```

## Wire format

CBOR over WebSocket using the SurrealDB-flavoured tag scheme:

| Tag | Meaning                            | .NET surface                |
|----:|------------------------------------|-----------------------------|
| 0   | Spec datetime (text)               | decode-only                 |
| 6   | `NONE` sentinel                    | `Value.None`                |
| 7   | Table reference                    | `Table` / `TableValue`      |
| 8   | RecordId `[table, key]`            | `RecordId` / `RecordIdKey`  |
| 9   | UUID (text)                        | decode-only                 |
| 10  | Decimal (text, canonical)          | `decimal`                   |
| 12  | Datetime `[seconds, nanos]`        | `Datetime` ↔ `DateTimeOffset` |
| 13  | Duration (text)                    | decode-only                 |
| 14  | Duration `[secs?, nanos?]`         | `Duration` ↔ `TimeSpan`     |
| 37  | UUID (16-byte big-endian)          | `Guid`                      |

Datetimes preserve full nanosecond precision via an explicit `Nanos` field
(since `DateTimeOffset` only resolves to 100ns ticks).

## Feature matrix

Compared against the [official Rust client](https://github.com/surrealdb/surrealdb/tree/main/crates/sdk)
as the de-facto reference implementation. *(Status legend: **yes** = supported,
**no** = not supported today, **partial** = some sub-features only, **out** =
permanently out of scope.)*

### Transports

| Transport                | Rust    | Disruptor.Surreal | Notes |
|--------------------------|---------|-------------------|-------|
| WebSocket (`ws`, `wss`)  | yes     | **yes**           | CBOR sub-protocol |
| HTTP / HTTPS             | yes     | no                | Planned; CBOR `application/cbor` POST `/rpc` |
| Embedded `mem`           | yes     | **out**           | We trust the database |
| Embedded `rocksdb`       | yes     | **out**           | — |
| Embedded `surrealkv`     | yes     | **out**           | — |
| Embedded `file`          | yes     | **out**           | — |
| Embedded `indxdb` (WASM) | yes     | **out**           | — |
| Distributed `tikv`       | yes     | **out**           | — |

### Wire format

| Format       | Rust         | Disruptor.Surreal | Notes |
|--------------|--------------|-------------------|-------|
| CBOR         | yes          | **yes**           | Via `System.Formats.Cbor` |
| JSON         | partial      | no                | Server supports it; lossy for record-id / datetime / decimal types |
| Flatbuffers  | yes (default)| no                | Rust client's current default; we don't intend to follow |

### RPC methods

| Method                                     | Rust | Disruptor.Surreal |
|--------------------------------------------|------|-------------------|
| `use_ns` / `use_db`                        | yes  | **yes**           |
| `signin` / `signup`                        | yes  | **yes**           |
| `authenticate` / `invalidate`              | yes  | **yes**           |
| `set` / `unset` (session vars)             | yes  | **yes**           |
| `query` (with bindings)                    | yes  | **yes**           |
| `select` (table or RecordId)               | yes  | **yes**           |
| `create` (table or RecordId, with content) | yes  | **yes**           |
| `update` (table or RecordId)               | yes  | **yes**           |
| `delete` (table or RecordId)               | yes  | **yes**           |
| `upsert` (table or RecordId)               | yes  | **yes**           |
| `merge` (table or RecordId)                | yes  | **yes**           |
| `patch` (JSON-Patch ops)                   | yes  | **yes** — see `Patch.Add/Replace/Remove/Move/Copy/Test/Change` helpers |
| `insert` (single or bulk)                  | yes  | **yes**           |
| `insert_relation` (single or bulk edges)   | yes  | **yes**           |
| `relate` (graph edge)                      | yes  | **yes**           |
| `run` (server-side function, optional version) | yes | **yes**          |
| `version` / `ping` (health)                | yes  | **yes**           |
| `begin` / `commit` / `cancel` (txn id)     | yes  | **yes**           |
| `live` / `kill` (live queries)             | yes  | no                |
| `export` / `import`                        | yes  | no                |
| ML model export                            | yes  | **out**           |

### Auth credentials

| Credential                    | Rust | Disruptor.Surreal |
|-------------------------------|------|-------------------|
| Root                          | yes  | **yes**           |
| Namespace                     | yes  | **yes**           |
| Database                      | yes  | **yes**           |
| Record (scope, generic params)| yes  | **yes**           |
| Access token (bearer)         | yes  | **yes**           |
| Refresh token / rotation      | yes  | **yes** (`Token { Access, Refresh? }`, `RefreshAsync`) |

### Value tree

| Variant                                | CBOR tag | Rust | Disruptor.Surreal |
|----------------------------------------|----------|------|-------------------|
| None / Null / Bool                     | 6 / —    | yes  | **yes**           |
| Number (Int / Float / Decimal)         | — / — / 10 | yes | **yes**           |
| String / Bytes                         | — / —    | yes  | **yes**           |
| Datetime (full nanosecond precision)   | 0 / 12   | yes  | **yes**           |
| Duration                               | 13 / 14  | yes  | **yes**           |
| Uuid                                   | 9 / 37   | yes  | **yes**           |
| Table                                  | 7        | yes  | **yes**           |
| RecordId (string / int / uuid keys)    | 8        | yes  | **yes**           |
| Array / Object                         | — / —    | yes  | **yes**           |
| Set                                    | 56       | yes  | **yes** (`SurrealSet`, `SetValue`) |
| Range / RecordIdKeyRange               | 49 / 50 / 51 | yes | **yes** (`SurrealRange`, `Bound<T>`, `RecordIdKeyRange`, `RangeRecordIdKey`) |
| Geometry (Point/Line/Polygon/Multi*/Collection) | 88–94 | yes | **yes** (`Geometry.Point/Line/Polygon/MultiPoint/MultiLine/MultiPolygon/Collection`) |
| File (bucket reference)                | 55       | yes  | **yes** (`SurrealFile`, `FileValue`) |
| Regex                                  | —        | yes  | n/a — Rust source explicitly errors on CBOR encoding for regex (`convert.rs:450`); no wire shape exists |

### Connection lifecycle

| Feature                                            | Rust | Disruptor.Surreal |
|----------------------------------------------------|------|-------------------|
| Connection-string parsing                          | yes  | **yes** (ADO-style) |
| One-shot connect + signin + use_ns/db              | n/a  | **yes**           |
| Auto re-auth on token expiry (transparent retry)   | yes  | **yes**           |
| Reconnect with session replay (outside txn)        | yes  | no                |
| Server version compatibility check                 | yes  | **yes** (`>=3.0.0-alpha.1, <4.0.0`; opt out via `ConnectionConfig.SkipVersionCheck`) |
| Multi-session per connection                       | yes  | no                |

### Error / diagnostics

| Feature                                            | Rust | Disruptor.Surreal |
|----------------------------------------------------|------|-------------------|
| Typed exception hierarchy                          | yes  | **yes** (Auth / Conflict / TransactionAborted / Constraint / Connection / Protocol / Rpc) |
| Token-expiry signal                                | yes  | **yes** (`SurrealAuthException.IsTokenExpired`) |
| Retry-on-conflict helper                           | n/a  | **yes** (`RetryPolicy.WithRetryAsync`) |

### Consumer-side mapping

| Feature                                            | Rust | Disruptor.Surreal |
|----------------------------------------------------|------|-------------------|
| `IRecordId` interop interface                      | yes (`Sealed` trait) | **yes**  |
| POCO mapping (attribute / source-gen / reflection) | yes (`SurrealValue` derive) | **out** — consumer brings the mapper |

## Layout

```
src/Disruptor.Surreal/         — the library (one package)
  Auth/                          — credentials + AccessToken
  Cbor/                          — tag table, reader, writer
  Connection/                    — Endpoint, Command, Rpc{Request,Response},
                                   IConnection, WebSocketConnection,
                                   SurrealOptions
  Errors/                        — exception hierarchy
  Query/                         — QueryResponse, QueryStatement
  Values/                        — Value tree + scalar wrappers (Datetime,
                                   Duration, etc.) + IRecordId
  Surreal.cs                     — main client class
  Transaction.cs                 — transaction handle (auto-cancel on dispose)
tests/Disruptor.Surreal.Tests/   — xUnit (CBOR roundtrip, endpoint parsing,
                                   value semantics, error classifier,
                                   options parsing)
samples/Disruptor.Surreal.Sample/ — runnable demo
```

## Run the tests

```sh
dotnet test
```

## Run the sample (needs a local server)

```sh
docker run -d --rm --name surrealdb -p 8000:8000 surrealdb/surrealdb:latest \
  start --user root --pass root memory
dotnet run --project samples/Disruptor.Surreal.Sample
```

## License

Apache-2.0.
