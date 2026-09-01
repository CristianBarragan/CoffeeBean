using Foundgine.Abstractions;
using Foundgine.Semantics.Results;

namespace Foundgine.Execution;

/// <summary>
/// Compatibility name for the semantic result tree. New code should consume
/// <see cref="SemanticResult"/> directly.
/// </summary>
[Obsolete("Use Foundgine.Semantics.Results.SemanticResult.")]
public sealed record MaterializedResult(
    IReadOnlyList<SemanticResultNode> Roots,
    SemanticResultPageInfo? PageInfo = null,
    SemanticResultEvidence? Evidence = null)
{
    public static implicit operator SemanticResult(MaterializedResult value) =>
        new(value.Roots, value.PageInfo, value.Evidence);
}
