using Disruptor.Surreal;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class RetryPolicyTests
{
    [Fact]
    public async Task SucceedsOnFirstAttempt_NoRetry()
    {
        var attempts = 0;
        var result = await RetryPolicy.WithRetryAsync<int>((n, _) =>
        {
            attempts++;
            return Task.FromResult(42);
        });
        Assert.Equal(42, result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task RetriesOnConflict_UntilSuccess()
    {
        var attempts = 0;
        var result = await RetryPolicy.WithRetryAsync<string>((n, _) =>
        {
            attempts++;
            if (n < 3) throw new SurrealConflictException(0, "transaction conflict");
            return Task.FromResult("eventually");
        }, maxAttempts: 5, initialBackoff: TimeSpan.FromMilliseconds(1));

        Assert.Equal("eventually", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task PropagatesConflict_AfterMaxAttempts()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<SurrealConflictException>(() =>
            RetryPolicy.WithRetryAsync<int>((n, _) =>
            {
                attempts++;
                throw new SurrealConflictException(0, "transaction conflict");
            }, maxAttempts: 3, initialBackoff: TimeSpan.FromMilliseconds(1)));
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task DoesNotRetry_NonConflictExceptions()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RetryPolicy.WithRetryAsync<int>((n, _) =>
            {
                attempts++;
                throw new InvalidOperationException("not a conflict");
            }));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task VoidOverload_Works()
    {
        var attempts = 0;
        await RetryPolicy.WithRetryAsync((n, _) =>
        {
            attempts++;
            if (n < 2) throw new SurrealConflictException(0, "transaction conflict");
            return Task.CompletedTask;
        }, maxAttempts: 3, initialBackoff: TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, attempts);
    }
}
