using Disruptor.Surreal;
using Disruptor.Surreal.Connection;
using Disruptor.Surreal.Values;

await using var db = await Surreal.ConnectAsync(SurrealOptions.Parse(
    "Url=ws://localhost:8000;Namespace=test;Database=test;User=root;Password=root"));
Console.WriteLine($"Connected. Server: {await db.VersionAsync()}");

// ─── insert (single + bulk) ───────────────────────────────────────────────
var jaime = await db.InsertAsync("person", new SurrealObject
{
    ["id"] = new RecordIdValue(new RecordId("person", "jaime")),
    ["name"] = "Jaime",
    ["age"] = 30L,
});
Console.WriteLine($"Insert single: {jaime}");

await db.InsertAsync("person",
[
    new SurrealObject { ["id"] = new RecordIdValue(new RecordId("person", "tobie")), ["name"] = "Tobie", ["age"] = 32L },
    new SurrealObject { ["id"] = new RecordIdValue(new RecordId("person", "rebecca")), ["name"] = "Rebecca", ["age"] = 28L },
]);
Console.WriteLine($"Insert bulk: {await db.SelectAsync("person")}");

// ─── upsert ───────────────────────────────────────────────────────────────
await db.UpsertAsync(new RecordId("person", "ghost"), new SurrealObject
{
    ["name"] = "Ghost",
    ["age"] = 99L,
});
Console.WriteLine($"Upsert created: {await db.SelectAsync(new RecordId("person", "ghost"))}");

// ─── merge ────────────────────────────────────────────────────────────────
await db.MergeAsync(new RecordId("person", "jaime"), new SurrealObject { ["title"] = "Founder" });
Console.WriteLine($"Merge: {await db.SelectAsync(new RecordId("person", "jaime"))}");

// ─── patch ────────────────────────────────────────────────────────────────
await db.PatchAsync(new RecordId("person", "jaime"),
[
    Patch.Replace("/age", 31L),
    Patch.Add("/email", "jaime@example.com"),
]);
Console.WriteLine($"Patch: {await db.SelectAsync(new RecordId("person", "jaime"))}");

// ─── relate ───────────────────────────────────────────────────────────────
await db.RelateAsync(
    new RecordId("person", "jaime"),
    "knows",
    new RecordId("person", "tobie"),
    new SurrealObject { ["since"] = "2020" });
Console.WriteLine($"Relate: {(await db.QueryAsync("SELECT * FROM knows")).Take(0)}");

// ─── insert_relation (bulk edges) ─────────────────────────────────────────
await db.InsertRelationAsync("knows",
[
    new SurrealObject
    {
        ["in"] = new RecordIdValue(new RecordId("person", "tobie")),
        ["out"] = new RecordIdValue(new RecordId("person", "rebecca")),
        ["since"] = "2021",
    },
]);
Console.WriteLine($"All edges: {(await db.QueryAsync("SELECT * FROM knows")).Take(0)}");

// ─── run (server-side function) ───────────────────────────────────────────
await db.QueryAsync("DEFINE FUNCTION fn::greet($name: string) { RETURN 'hello, ' + $name }");
var greet = await db.RunAsync("fn::greet", ["Jaime"]);
Console.WriteLine($"Run fn::greet('Jaime') => {greet}");

// ─── live queries ─────────────────────────────────────────────────────────
// Subscribe, mutate in another task, watch notifications stream in.
await using var live = await db.LiveAsync("SELECT * FROM person");
Console.WriteLine($"Subscribed live query {live.Id}");

var producer = Task.Run(async () =>
{
    await Task.Delay(50);
    await db.CreateAsync(new RecordId("person", "newcomer"), new SurrealObject { ["name"] = "Newcomer", ["age"] = 25L });
    await db.UpdateAsync(new RecordId("person", "ghost"), new SurrealObject { ["name"] = "Ghost", ["age"] = 100L });
    await db.DeleteAsync(new RecordId("person", "newcomer"));
});

using var liveCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
var seen = 0;
try
{
    await foreach (var n in live.WithCancellation(liveCts.Token))
    {
        Console.WriteLine($"  notif: {n.Action} {n.Record}");
        seen++;
        if (seen >= 3) break;
    }
}
catch (OperationCanceledException) { /* timeout fallback */ }
await producer;
Console.WriteLine($"Live total seen: {seen}, dropped: {live.DroppedCount}");

// Cleanup
await db.QueryAsync("REMOVE TABLE person");
await db.QueryAsync("REMOVE TABLE knows");
await db.QueryAsync("REMOVE FUNCTION fn::greet");
Console.WriteLine("Cleaned up.");
