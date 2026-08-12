namespace Foundgine.Execution.Mutation;

public interface IMutationExecutionProvider
{
    MutationResult Execute(
        ProviderMutationPlan plan,
        ExecutionContext context);
}
