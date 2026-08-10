using Foundgine.Semantics;

namespace Foundgine.Planning;

/// <summary>
/// Provider-independent execution intent produced from an authorized semantic
/// graph. This is deliberately small until a real provider requires more.
/// </summary>
public sealed record ExecutionPlan(SemanticGraph Graph);
