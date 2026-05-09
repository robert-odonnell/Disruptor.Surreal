using System.Threading.Channels;

namespace Disruptor.Surreal;

/// <summary>Per-subscription tuning for <c>Surreal.LiveAsync</c>.</summary>
public sealed record LiveQueryOptions
{
    /// <summary>
    /// Maximum number of un-consumed notifications buffered for this subscription.
    /// Default 256.
    /// </summary>
    public int Capacity { get; init; } = 256;

    /// <summary>
    /// What to do when the buffer is full and a new notification arrives. Default
    /// <see cref="BoundedChannelFullMode.DropNewest"/> — drops the just-arrived
    /// notification, increments <see cref="LiveQueryHandle.DroppedCount"/>, and keeps
    /// the older buffered notifications consumable. This avoids back-pressuring the
    /// connection-wide receive loop (which would stall every other RPC and live query
    /// on the connection); the trade-off is silent drops if you don't watch
    /// <c>DroppedCount</c>.
    /// </summary>
    /// <remarks>
    /// Other choices:
    /// <list type="bullet">
    /// <item><see cref="BoundedChannelFullMode.Wait"/>: blocks the receive loop. Foot-gun on a shared connection — only safe if you have a single live query and a fast consumer.</item>
    /// <item><see cref="BoundedChannelFullMode.DropOldest"/>: keep latest, drop oldest. Right when "current state" matters more than "every transition".</item>
    /// <item><see cref="BoundedChannelFullMode.DropWrite"/>: silently drops new arrivals without bumping <see cref="LiveQueryHandle.DroppedCount"/>. Almost never the right choice.</item>
    /// </list>
    /// </remarks>
    public BoundedChannelFullMode FullMode { get; init; } = BoundedChannelFullMode.DropNewest;
}
