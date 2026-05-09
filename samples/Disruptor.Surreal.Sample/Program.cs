using Disruptor.Surreal;
using Disruptor.Surreal.Connection;
using Disruptor.Surreal.Values;

// One-shot connect: parse the connection string, dial WS, sign in, switch ns/db.
var connStr = Environment.GetEnvironmentVariable("SURREAL_CONN")
    ?? "Url=ws://localhost:8000;Namespace=test;Database=test;User=root;Password=root";

await using var db = await Surreal.ConnectAsync(SurrealOptions.Parse(connStr));
Console.WriteLine($"Connected and signed in. Server version: {await db.VersionAsync()}");

var jaime = new RecordId("person", "jaime");

// Create a record at a known id
await db.CreateAsync(jaime, new SurrealObject
{
    ["name"] = "Jaime",
    ["age"] = 30L,
    ["admin"] = true,
    ["joined"] = DateTimeOffset.UtcNow,
    ["balance"] = 1234.56m,
});
Console.WriteLine($"Created {jaime}");

// Query with a binding
var result = await db.QueryAsync(
    "SELECT * FROM person WHERE age >= $minAge",
    new SurrealObject { ["minAge"] = 21L });
Console.WriteLine($"Query result: {result.Take(0)}");

// Transaction: update inside, cancel, observe rollback
await using (var tx = await db.BeginTransactionAsync())
{
    await tx.UpdateAsync(jaime, new SurrealObject
    {
        ["name"] = "Jaime (updated in tx)",
        ["age"] = 31L,
        ["admin"] = false,
        ["joined"] = DateTimeOffset.UtcNow,
        ["balance"] = 0m,
    });

    Console.WriteLine($"Inside transaction {tx.Id} — about to cancel.");
    await tx.CancelAsync();
}

var afterCancel = await db.SelectAsync(jaime);
Console.WriteLine($"After cancel: {afterCancel}");

// IRecordId — anything implementing it works through the API
IRecordId asInterface = jaime;
var alsoSelected = await db.SelectAsync(asInterface);
Console.WriteLine($"Via IRecordId: {alsoSelected}");

// Cleanup
await db.DeleteAsync(jaime);
Console.WriteLine("Cleaned up.");
