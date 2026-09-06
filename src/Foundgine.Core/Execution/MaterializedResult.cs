using Foundgine.Core.Semantic.Results;

namespace Foundgine.Core.Execution;

/// <summary>
///     Compatibility name for the semantic result tree. New code should consume
///     <see cref="SemanticResult" /> directly.
/// </summary>
[Obsolete("Use Foundgine.Core.Semantic.Results.SemanticResult.")]
public sealed record MaterializedResult(
    IReadOnlyList<SemanticResultNode> Roots,
    SemanticResultPageInfo? PageInfo = null,
    SemanticResultEvidence? Evidence = null)
{
    public static implicit operator SemanticResult(MaterializedResult value)
    {
        return new SemanticResult(value.Roots, value.PageInfo, value.Evidence);
    }
}