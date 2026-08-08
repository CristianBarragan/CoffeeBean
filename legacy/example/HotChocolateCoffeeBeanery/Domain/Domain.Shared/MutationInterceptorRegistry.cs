namespace CoffeeBeanery.GraphQL.Core.Runtime;

/// <summary>
/// Array-indexed dispatch for mutation interceptors. Populated at host
/// startup via DI (see MutationInterceptorServiceCollectionExtensions) —
/// NOT hardcoded here, since interceptor wiring is a customer/consumer
/// concern, not something the core runtime should know about statically.
/// </summary>
public static class MutationInterceptorRegistry
{
    private static IMutationInterceptor?[] _interceptors = new IMutationInterceptor?[MutationMetadataRegistry.Count];

    public static void Register(ushort entityId, IMutationInterceptor interceptor)
    {
        if (entityId >= _interceptors.Length)
            System.Array.Resize(ref _interceptors, entityId + 1);

        _interceptors[entityId] = interceptor;
    }

    public static void Apply(ushort entityId, object model, MutationInterceptorContext context)
    {
        var interceptor = entityId < _interceptors.Length ? _interceptors[entityId] : null;
        interceptor?.Apply(model, context);
    }
}