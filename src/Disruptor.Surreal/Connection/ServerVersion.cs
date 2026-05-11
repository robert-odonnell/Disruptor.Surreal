using System.Text.RegularExpressions;

namespace Disruptor.Surreal.Connection;

/// <summary>
/// A parsed SurrealDB server version. Sourced from the <c>version</c> RPC, which
/// returns strings like <c>"surrealdb-3.0.5"</c> or <c>"surrealdb-3.0.0-alpha.1"</c>.
/// </summary>
public readonly partial record struct ServerVersion(int Major, int Minor, int Patch, string? PreRelease)
    : IComparable<ServerVersion>
{
    /// <summary>
    /// Parses a server-reported version. Tolerates an optional <c>surrealdb-</c> prefix
    /// and a SemVer pre-release suffix (which is preserved on the parsed value but
    /// ignored for compatibility comparisons).
    /// </summary>
    public static ServerVersion Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var m = VersionPattern().Match(text);
        if (!m.Success)
            throw new FormatException($"Could not parse SurrealDB version: '{text}'.");
        return new ServerVersion(
            Major: int.Parse(m.Groups[1].Value),
            Minor: int.Parse(m.Groups[2].Value),
            Patch: int.Parse(m.Groups[3].Value),
            PreRelease: m.Groups[4].Success ? m.Groups[4].Value : null);
    }

    /// <summary>True when this version is within the <c>[lower, upper)</c> half-open major range.</summary>
    public bool IsInMajorRange(int minMajor, int maxMajorExclusive) =>
        Major >= minMajor && Major < maxMajorExclusive;

    /// <summary>Lexicographic comparison on major/minor/patch (pre-release ignored).</summary>
    public int CompareTo(ServerVersion other)
    {
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        return Patch.CompareTo(other.Patch);
    }

    public override string ToString() => PreRelease is null
        ? $"{Major}.{Minor}.{Patch}"
        : $"{Major}.{Minor}.{Patch}-{PreRelease}";

    [GeneratedRegex(@"^(?:surrealdb-)?(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z\-.]+))?\s*$", RegexOptions.Compiled)]
    private static partial Regex VersionPattern();
}

/// <summary>
/// The supported SurrealDB version range for the current SDK build. Mirrors the
/// official Rust client's <c>SUPPORTED_VERSIONS</c> constant (<c>src/lib.rs</c>):
/// <c>"&gt;=3.0.0-alpha.1, &lt;4.0.0"</c>. Pre-release segments are stripped before
/// the range check so 3.x betas/rcs match cleanly.
/// </summary>
public static class SupportedVersion
{
    /// <summary>Inclusive lower bound on the major version.</summary>
    public const int MinMajor = 3;

    /// <summary>Exclusive upper bound on the major version.</summary>
    public const int MaxMajorExclusive = 4;

    /// <summary>Human-readable description of the supported range, matching the Rust client.</summary>
    public const string Range = ">=3.0.0-alpha.1, <4.0.0";

    /// <summary>
    /// True when <paramref name="v"/> is supported. Strips any pre-release segment
    /// before checking, matching the Rust client's <c>version.pre = Default::default()</c>
    /// step prior to <c>check_server_version</c>.
    /// </summary>
    public static bool IsSupported(ServerVersion v) =>
        v.IsInMajorRange(MinMajor, MaxMajorExclusive);
}
