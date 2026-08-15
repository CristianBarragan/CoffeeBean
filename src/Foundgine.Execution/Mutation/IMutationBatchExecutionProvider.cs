namespace Foundgine.Execution.Mutation;

/// <summary>
/// Executes an ordered mutation batch from the canonical provider-neutral
/// execution representation.
/// </summary>
public interface IMutationBatchExecutionProvider
{
    MutationBatchResult ExecuteBatch(
        ExecutionMutationIR ir,
        ExecutionContext context);
}
