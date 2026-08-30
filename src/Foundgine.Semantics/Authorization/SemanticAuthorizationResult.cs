using Foundgine.Semantics.IR;

namespace Foundgine.Semantics.Authorization;

/// <summary>Authorization output that cannot be detached from its contract identity.</summary>
public sealed record SemanticAuthorizationResult(
    SemanticOperation Operation,
    SemanticAuthorizationEvidence Evidence)
{
    public void EnsureMatches(SemanticContractSnapshot contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        Evidence.EnsureMatches(contract);
    }
}
