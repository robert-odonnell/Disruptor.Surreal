using Disruptor.Surreal.Values;

namespace Disruptor.Surreal;

/// <summary>The status of a single statement in a multi-statement query.</summary>
public enum QueryStatementStatus
{
    Ok,
    Error,
}

/// <summary>The outcome of one statement in a multi-statement query.</summary>
public sealed record QueryStatement(
    QueryStatementStatus Status,
    Value? Result,
    string? ErrorMessage,
    TimeSpan? ExecutionTime)
{
    /// <summary>True when this statement completed successfully.</summary>
    public bool IsOk => Status == QueryStatementStatus.Ok;

    /// <summary>True when this statement returned an error.</summary>
    public bool IsError => Status == QueryStatementStatus.Error;
}

/// <summary>
/// The decoded response from <see cref="Surreal.QueryAsync"/>. Contains one
/// <see cref="QueryStatement"/> per statement in the original SQL.
/// </summary>
public sealed class QueryResponse
{
    /// <summary>One entry per statement, in source order.</summary>
    public IReadOnlyList<QueryStatement> Statements { get; }

    /// <summary>Number of statements in the response.</summary>
    public int Count => Statements.Count;

    internal QueryResponse(IReadOnlyList<QueryStatement> statements) => Statements = statements;

    /// <summary>
    /// Returns the result of the statement at <paramref name="index"/>.
    /// Throws <see cref="SurrealRpcException"/> if the statement errored.
    /// </summary>
    public Value Take(int index)
    {
        if ((uint)index >= (uint)Statements.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var s = Statements[index];
        if (s.IsError)
            throw new SurrealRpcException(0, s.ErrorMessage ?? "Statement failed.");
        return s.Result ?? Value.None;
    }

    /// <summary>Throws if any statement returned an error; otherwise no-op.</summary>
    public void EnsureSuccess()
    {
        foreach (var s in Statements)
            if (s.IsError)
                throw new SurrealRpcException(0, s.ErrorMessage ?? "Statement failed.");
    }

    internal static QueryResponse FromValue(Value value)
    {
        if (value is not ArrayValue { Array: var arr })
        {
            // Single-statement responses are sometimes returned as a bare value.
            return new QueryResponse(
            [
                new QueryStatement(QueryStatementStatus.Ok, value, null, null)
            ]
            );
        }

        var list = new List<QueryStatement>(arr.Count);
        foreach (var item in arr)
        {
            if (item is not ObjectValue { Object: var obj })
            {
                list.Add(new QueryStatement(QueryStatementStatus.Ok, item, null, null));
                continue;
            }

            var status = QueryStatementStatus.Ok;
            if (obj.TryGetValue("status", out var s) && s is StringValue ss && ss.Value == "ERR")
                status = QueryStatementStatus.Error;

            obj.TryGetValue("result", out var resultValue);
            obj.TryGetValue("time", out var timeValue);

            string? errorMessage = null;
            Value? result = null;

            if (status == QueryStatementStatus.Error)
                errorMessage = (resultValue as StringValue)?.Value ?? "Unknown error";
            else
                result = resultValue;

            TimeSpan? execTime = null;
            if (timeValue is StringValue tStr && TryParseHumanDuration(tStr.Value, out var ts))
                execTime = ts;

            list.Add(new QueryStatement(status, result, errorMessage, execTime));
        }

        return new QueryResponse(list);
    }

    private static bool TryParseHumanDuration(string text, out TimeSpan value)
    {
        // Accept Rust's Debug-format durations: "1.234ms", "42.5µs", "12s", "1m30s500ms" etc.
        // Best-effort — we don't care if exotic units round-trip exactly.
        value = TimeSpan.Zero;
        var i = 0;
        while (i < text.Length)
        {
            var start = i;
            while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
            if (start == i) return false;
            if (!double.TryParse(text[start..i], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
                return false;

            var unitStart = i;
            while (i < text.Length && !char.IsDigit(text[i])) i++;
            var unit = text[unitStart..i];

            value += unit switch
            {
                "ns" => TimeSpan.FromTicks((long)(num / 100.0)),
                "us" or "µs" => TimeSpan.FromTicks((long)(num * 10.0)),
                "ms" => TimeSpan.FromMilliseconds(num),
                "s" => TimeSpan.FromSeconds(num),
                "m" => TimeSpan.FromMinutes(num),
                "h" => TimeSpan.FromHours(num),
                _ => TimeSpan.Zero,
            };
        }
        return true;
    }
}
