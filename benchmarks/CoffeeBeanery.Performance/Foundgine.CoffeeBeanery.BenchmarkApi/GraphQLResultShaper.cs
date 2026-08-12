using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.GraphQL.HotChocolate;
using Foundgine.Semantics;
using Foundgine.Planning;

namespace Foundgine.CoffeeBeanery.BenchmarkApi;

internal static class GraphQLResultShaper
{
    internal static object Shape(
        GraphQLQueryAdaptation adaptation,
        SemanticModel model,
        ExecutionResult execution,
        ExecutionPlan plan)
    {
        var materialized = new ResultMaterializer(model).Materialize(plan, execution);
        var values = materialized.Roots
            .Select(root => ShapeNode(root, adaptation.Result))
            .ToArray();

        return new Dictionary<string, object?>
        {
            ["data"] = new Dictionary<string, object?>
            {
                ["customer"] = values
            }
        };
    }

    private static object ShapeNode(MaterializedNode node, GraphQLResultShape shape)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var field in shape.Fields)
        {
            if (field.Field is { } fieldId)
            {
                result[field.Alias] = node.Values.TryGetValue(fieldId, out var value) ? value : null;
                continue;
            }

            if (field.Relationship is { } relationshipId)
            {
                var children = node.Children.TryGetValue(relationshipId, out var list)
                    ? list
                    : [];
                var childShape = field.Children ?? new GraphQLResultShape([]);
                result[field.Alias] = children.Select(x => ShapeNode(x, childShape)).ToArray();
            }
        }

        return result;
    }
}
