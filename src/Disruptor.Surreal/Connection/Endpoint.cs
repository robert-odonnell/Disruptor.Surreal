namespace Disruptor.Surreal.Connection;

/// <summary>The supported wire schemes for v1.</summary>
public enum EndpointKind
{
    /// <summary>Plain WebSocket (<c>ws://</c>).</summary>
    Ws,
    /// <summary>TLS WebSocket (<c>wss://</c>).</summary>
    Wss,
}

/// <summary>Configuration knobs for a SurrealDB connection.</summary>
public sealed record ConnectionConfig
{
    /// <summary>How long to wait for a single RPC reply before failing the request.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Cadence at which the client sends an RPC ping to keep the connection alive.</summary>
    public TimeSpan PingInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum size of an inbound or outbound CBOR payload, in bytes.</summary>
    public int MaxMessageSize { get; init; } = 64 * 1024 * 1024;

    /// <summary>
    /// Skip the version-compat check after connect. Mirrors the Rust client's behaviour of
    /// only running the check for remote endpoints and tolerating dev / unreleased servers.
    /// Default false: the check runs and rejects servers outside <see cref="SupportedVersion.Range"/>.
    /// </summary>
    public bool SkipVersionCheck { get; init; }
}

/// <summary>An immutable, parsed SurrealDB endpoint.</summary>
public sealed record Endpoint(Uri Url, EndpointKind Kind, ConnectionConfig Config)
{
    /// <summary>Parses a URL string and returns the corresponding endpoint.</summary>
    /// <remarks>
    /// Accepts <c>ws://host:port</c> and <c>wss://host:port</c>. Bare <c>host:port</c> is treated as <c>ws://</c>.
    /// </remarks>
    public static Endpoint Parse(string url, ConnectionConfig? config = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        config ??= new ConnectionConfig();

        if (!url.Contains("://", StringComparison.Ordinal))
            url = "ws://" + url;

        var uri = new Uri(url);
        var kind = uri.Scheme switch
        {
            "ws" => EndpointKind.Ws,
            "wss" => EndpointKind.Wss,
            _ => throw new ArgumentException(
                $"Unsupported scheme '{uri.Scheme}'. v1 supports ws/wss only.", nameof(url)),
        };

        // Append /rpc if not already present, matching the Rust client's PATH constant.
        if (!uri.AbsolutePath.TrimEnd('/').EndsWith("/rpc", StringComparison.Ordinal))
        {
            var ub = new UriBuilder(uri)
            {
                Path = uri.AbsolutePath.TrimEnd('/') + "/rpc",
            };
            uri = ub.Uri;
        }

        return new Endpoint(uri, kind, config);
    }
}
