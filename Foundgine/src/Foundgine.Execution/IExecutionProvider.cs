namespace Foundgine.Execution;

public interface IExecutionProvider
{
    Task<ExecutionResult> ExecuteAsync(
        ProviderPlan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default);
}
