namespace Foundgine.Metadata;

/// <summary>
/// Simple LIMIT/OFFSET paging — deliberately not cursor pagination yet
/// (see the root roadmap's Milestone 7). Either value may be omitted:
/// <c>Limit</c> alone caps the result set, <c>Offset</c> alone skips rows
/// with no cap, and both together is a page window.
/// </summary>
public sealed record PageSpec(
    int? Limit = null,
    int? Offset = null
);
