using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public static class MutationOptimizer
{
    public static MutationIR Optimize(in MutationIR mutation)
    {
        var children = ImmutableArray.CreateBuilder<MutationIR>(
            mutation.Children.Length);

        foreach (var child in mutation.Children)
        {
            var optimized = Optimize(child);

            if (HasWork(optimized))
                children.Add(optimized);
        }

        return new MutationIR(
            mutation.EntityId,
            mutation.OutputAlias,
            mutation.Values,
            children.ToImmutable());
    }

    public static bool HasWork(in MutationIR mutation)
    {
        return mutation.Values.Length > 0 ||
               mutation.Children.Length > 0;
    }
}