namespace Disruptor.Surreal;

/// <summary>
/// Conflict-aware retry policy. Wraps a <see cref="SurrealConflictException"/>-throwing
/// operation (typically a transactional commit) with bounded retries and exponential
/// backoff.
/// </summary>
/// <remarks>
/// SurrealDB's MVCC surfaces concurrent modifications as conflict errors at COMMIT
/// time. The canonical response is to re-run the whole load → mutate → commit
/// cycle from scratch, since later attempts see the new committed state. This helper
/// codifies that pattern so consumers don't redo it for every commit point.
/// </remarks>
public static class SurrealRetryPolicy
{
    /// <summary>
    /// Run <paramref name="action"/>, retrying up to <paramref name="maxAttempts"/> times
    /// when it throws <see cref="SurrealConflictException"/>. Backoff doubles between
    /// attempts starting at <paramref name="initialBackoff"/> (default 50 ms).
    /// </summary>
    /// <param name="action">The operation to run; receives the 1-based attempt number.</param>
    /// <param name="maxAttempts">Maximum total attempts (including the first). Default 4.</param>
    /// <param name="initialBackoff">Backoff before retry #2. Doubles thereafter. Default 50 ms.</param>
    /// <param name="ct">Cancellation token, observed during backoff.</param>
    /// <exception cref="SurrealConflictException">Thrown when all attempts fail with conflicts.</exception>
    public static async Task<T> WithRetryAsync<T>(
        Func<int, CancellationToken, Task<T>> action,
        int maxAttempts = 4,
        TimeSpan? initialBackoff = null,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        var backoff = initialBackoff ?? TimeSpan.FromMilliseconds(50);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action(attempt, ct).ConfigureAwait(false);
            }
            catch (SurrealConflictException) when (attempt < maxAttempts)
            {
                await Task.Delay(backoff, ct).ConfigureAwait(false);
                backoff *= 2;
            }
        }
    }

    /// <summary>
    /// Run <paramref name="action"/>, retrying up to <paramref name="maxAttempts"/> times
    /// when it throws <see cref="SurrealConflictException"/>. Backoff doubles between
    /// attempts starting at <paramref name="initialBackoff"/> (default 50 ms).
    /// </summary>
    public static async Task WithRetryAsync(
        Func<int, CancellationToken, Task> action,
        int maxAttempts = 4,
        TimeSpan? initialBackoff = null,
        CancellationToken ct = default)
    {
        await WithRetryAsync(async (attempt, innerCt) =>
        {
            await action(attempt, innerCt).ConfigureAwait(false);
            return 0;
        }, maxAttempts, initialBackoff, ct).ConfigureAwait(false);
    }
}
