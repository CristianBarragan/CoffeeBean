namespace Foundgine.Foundation.CQRS;

public interface IQuery<in TQueryParameters, TQueryResult>
{
    public Task<TQueryResult> ExecuteAsync(TQueryParameters parameters, CancellationToken cancellationToken);
}