# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-07-03

First stable release. Unofficial .NET 10 client for SurrealDB — CBOR over
WebSocket, modeled on the official Rust client. See the [README](README.md)
for the full feature matrix.

### Added

- **Transport** — WebSocket (`ws` / `wss`) with the CBOR sub-protocol. HTTP and
  embedded engines are intentionally out of scope.
- **Wire format** — CBOR encode/decode using the SurrealDB tag scheme, with
  faithful round-tripping of `RecordId`, `decimal` (tag 10), `DateTimeOffset`
  (tag 12), `Guid` (tag 37), and `Datetime` / `Duration` at full nanosecond
  precision.
- **RPC methods** — `use_ns`/`use_db`, `signin`/`signup`, `authenticate`/
  `invalidate`, `refresh`, `set`/`unset`, `query` (with bindings), `select`,
  `create`, `update`, `upsert`, `merge`, `patch`, `insert`, `insert_relation`,
  `relate`, `run`, `version`/`ping`, `begin`/`commit`/`cancel`, and
  `live`/`kill`.
- **Value tree** — full `SurrealValue` model: None/Null/Bool, Number
  (Int/Float/Decimal), String/Bytes, `SurrealDateTime`, `SurrealDuration`, Uuid,
  `SurrealTable`, `SurrealRecordId` (string / integer / uuid / ulid / list /
  object / range keys), `SurrealList`, `SurrealObject`, `SurrealSet`,
  `SurrealRange`, `SurrealGeometry` (point/line/polygon/multi-*/collection), and
  `SurrealFile`.
- **Auth** — Root, Namespace, Database, and Record credentials; bearer access
  tokens; refresh-token rotation (`SurrealToken`, `RefreshAsync`); transparent
  re-auth on token expiry.
- **Live queries** — `LiveAsync` returns a `SurrealLiveQueryHandle`
  (`IAsyncEnumerable<SurrealNotification>`) with `DroppedCount` back-pressure
  reporting.
- **Transactions** — server-side `BeginTransactionAsync` handle with
  auto-cancel on dispose, `CommitAsync`, and `CancelAsync`.
- **Connection** — ADO-style connection-string parsing, one-shot connect +
  signin + `use_ns`/`use_db`, and a server-version compatibility check
  (`>=3.0.0-alpha.1, <4.0.0`, opt-out via `SurrealConnectionConfig.SkipVersionCheck`).
- **Diagnostics** — typed exception hierarchy (Auth / Conflict /
  TransactionAborted / Constraint / Connection / Protocol / Rpc), a token-expiry
  signal (`SurrealAuthException.IsTokenExpired`), and a retry-on-conflict helper
  (`SurrealRetryPolicy.WithRetryAsync`).

[1.0.0]: https://github.com/robert-odonnell/Disruptor.Surreal/releases/tag/v1.0.0
