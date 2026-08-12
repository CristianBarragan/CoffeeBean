namespace Foundgine.Execution.Mutation;

/// <summary>
/// Executes an ordered mutation batch atomically.
/// </summary>
public interface IMutationBatchExecutionProvider
{
    MutationBatchResult ExecuteBatch(
        ProviderMutationBatchPlan plan,
        ExecutionContext context);
}
