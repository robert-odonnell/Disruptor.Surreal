using System.Globalization;
using Disruptor.Surreal.Auth;

namespace Disruptor.Surreal.Connection;

/// <summary>
/// All-in-one connection settings: where to connect, who to sign in as, which
/// namespace/database to use. Pass to <see cref="Surreal.ConnectAsync(SurrealOptions, CancellationToken)"/>
/// to do connect + signin + use_ns/db in a single call.
/// </summary>
public sealed record SurrealOptions
{
    /// <summary>The endpoint URL. Accepts <c>ws://</c>, <c>wss://</c>, or bare <c>host:port</c> (defaults to ws).</summary>
    public required string Url { get; init; }

    /// <summary>Optional namespace to switch to after signin. <c>null</c> skips <c>USE NS</c>.</summary>
    public string? Namespace { get; init; }

    /// <summary>Optional database to switch to after signin. <c>null</c> skips <c>USE DB</c>.</summary>
    public string? Database { get; init; }

    /// <summary>Username for root-level signin. When set, <see cref="Password"/> must be set too.</summary>
    public string? User { get; init; }

    /// <summary>Password for root-level signin.</summary>
    public string? Password { get; init; }

    /// <summary>Underlying connection-level configuration. Defaults are sane.</summary>
    public ConnectionConfig Config { get; init; } = new();

    /// <summary>Returns the credentials to pass to <c>SigninAsync</c>, or <c>null</c> when no signin is requested.</summary>
    internal ICredentials? BuildCredentials() =>
        User is not null && Password is not null ? new Root(User, Password) : null;

    /// <summary>
    /// Parse an ADO-style key/value connection string. Recognised keys (case-insensitive):
    /// <c>Url</c>, <c>Server</c> (alias), <c>Namespace</c>/<c>NS</c>, <c>Database</c>/<c>DB</c>,
    /// <c>User</c>/<c>Username</c>, <c>Password</c>/<c>Pwd</c>,
    /// <c>RequestTimeout</c> (seconds), <c>PingInterval</c> (seconds),
    /// <c>MaxMessageSize</c> (bytes).
    /// </summary>
    public static SurrealOptions Parse(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        string? url = null;
        string? ns = null;
        string? db = null;
        string? user = null;
        string? pwd = null;
        TimeSpan? requestTimeout = null;
        TimeSpan? pingInterval = null;
        int? maxMessageSize = null;

        foreach (var rawPair in connectionString.Split(';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = rawPair.IndexOf('=');
            if (eq < 1)
                throw new FormatException($"Invalid connection-string segment '{rawPair}'.");

            var key = rawPair[..eq].Trim();
            var value = rawPair[(eq + 1)..].Trim();
            // Allow values to be wrapped in single or double quotes for spaces / special chars.
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];

            switch (key.ToLowerInvariant())
            {
                case "url":
                case "server":
                case "endpoint":
                    url = value;
                    break;
                case "ns":
                case "namespace":
                    ns = value;
                    break;
                case "db":
                case "database":
                    db = value;
                    break;
                case "user":
                case "username":
                case "uid":
                    user = value;
                    break;
                case "pwd":
                case "password":
                    pwd = value;
                    break;
                case "requesttimeout":
                    requestTimeout = TimeSpan.FromSeconds(double.Parse(value, CultureInfo.InvariantCulture));
                    break;
                case "pinginterval":
                    pingInterval = TimeSpan.FromSeconds(double.Parse(value, CultureInfo.InvariantCulture));
                    break;
                case "maxmessagesize":
                    maxMessageSize = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new FormatException($"Unrecognised connection-string key '{key}'.");
            }
        }

        if (url is null)
            throw new FormatException("Connection string is missing the required 'Url' key.");

        var config = new ConnectionConfig
        {
            RequestTimeout = requestTimeout ?? new ConnectionConfig().RequestTimeout,
            PingInterval = pingInterval ?? new ConnectionConfig().PingInterval,
            MaxMessageSize = maxMessageSize ?? new ConnectionConfig().MaxMessageSize,
        };

        return new SurrealOptions
        {
            Url = url,
            Namespace = ns,
            Database = db,
            User = user,
            Password = pwd,
            Config = config,
        };
    }
}
