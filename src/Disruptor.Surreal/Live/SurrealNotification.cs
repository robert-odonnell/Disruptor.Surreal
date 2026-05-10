using Disruptor.Surreal.Values;

namespace Disruptor.Surreal;

/// <summary>The kind of change a live notification reports.</summary>
public enum SurrealNotificationAction
{
    /// <summary>A new record matching the live query was created.</summary>
    Create,

    /// <summary>An existing record was updated and still matches the live query.</summary>
    Update,

    /// <summary>A record was deleted (or no longer matches the live query).</summary>
    Delete,
}

/// <summary>
/// One change-notification from a live query. Mirrors the wire shape SurrealDB emits
/// for unsolicited live frames: <c>{ id, session?, action, record, result }</c>.
/// </summary>
/// <param name="LiveQueryId">The id of the live query this notification belongs to.</param>
/// <param name="Action">The kind of change.</param>
/// <param name="Record">The affected record id (typically a <see cref="SurrealRecordIdValue"/>).</param>
/// <param name="Result">
/// The row payload — current state for <see cref="SurrealNotificationAction.Create"/> /
/// <see cref="SurrealNotificationAction.Update"/>; before-image for
/// <see cref="SurrealNotificationAction.Delete"/>.
/// </param>
public sealed record SurrealNotification(
    Guid LiveQueryId,
    SurrealNotificationAction Action,
    SurrealValue Record,
    SurrealValue Result);
