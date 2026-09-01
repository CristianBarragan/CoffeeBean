using Foundgine.Semantics.IR;
using Foundgine.Semantics.IR.Graph;

namespace Foundgine.Semantics.Authorization;

/// <summary>
/// Authorization output for a canonical semantic operation graph. The graph and
/// evidence are produced together so a caller cannot accidentally detach the
/// authorized graph from the contract against which it was evaluated.
/// </summary>
public sealed record SemanticOperationGraphAuthorizationResult(
    SemanticOperationGraph Graph,
    SemanticAuthorizationEvidence Evidence)
{
    public void EnsureMatches(SemanticContractSnapshot contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        Evidence.EnsureMatches(contract);
    }
}
