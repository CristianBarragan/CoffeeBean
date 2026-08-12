namespace Foundgine.Execution;

/// <summary>
/// Compiles a provider-independent execution plan into a provider-specific plan.
/// The core execution facade depends on this contract rather than on SQL or any
/// other provider.
/// </summary>
public interface IProviderPlanCompiler
{
    ProviderPlan Compile(Foundgine.Planning.ExecutionPlan plan);
}
