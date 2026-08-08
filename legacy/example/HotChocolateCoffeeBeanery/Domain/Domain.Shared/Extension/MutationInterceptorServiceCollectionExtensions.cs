namespace CoffeeBeanery.GraphQL.Core.Runtime;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class MutationInterceptorServiceCollectionExtensions
{
    /// <summary>
    /// Registers a mutation interceptor for the given entity. Resolvable
    /// via DI as IMutationInterceptor, and additionally wired into the
    /// static MutationInterceptorRegistry at host startup so the
    /// (non-DI-aware) MutationRuntimePlanner can dispatch to it by
    /// entityId during plan construction.
    /// </summary>
    public static IServiceCollection AddMutationInterceptor<TModel>(
        this IServiceCollection services,
        ushort entityId,
        IMutationInterceptor interceptor)
    {
        services.AddSingleton(interceptor);

        services.AddSingleton<IHostedService>(
            new MutationInterceptorRegistrationHostedService(entityId, interceptor));

        return services;
    }

    private sealed class MutationInterceptorRegistrationHostedService : IHostedService
    {
        private readonly ushort _entityId;
        private readonly IMutationInterceptor _interceptor;

        public MutationInterceptorRegistrationHostedService(
            ushort entityId,
            IMutationInterceptor interceptor)
        {
            _entityId = entityId;
            _interceptor = interceptor;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            MutationInterceptorRegistry.Register(_entityId, _interceptor);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}