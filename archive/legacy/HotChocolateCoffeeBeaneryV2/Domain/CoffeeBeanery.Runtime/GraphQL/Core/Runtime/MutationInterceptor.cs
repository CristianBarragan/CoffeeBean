using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public readonly struct MutationInterceptorContext
{
    public readonly MutationIR Node;
    public readonly MutationIR? Parent;
    public readonly ImmutableArray<object> Children;

    public MutationInterceptorContext(MutationIR node, MutationIR? parent, ImmutableArray<object> children)
    {
        Node = node;
        Parent = parent;
        Children = children;
    }
}

public interface IMutationInterceptor
{
    void Apply(object model, MutationInterceptorContext context);
}