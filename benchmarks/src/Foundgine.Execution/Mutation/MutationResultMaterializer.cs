using Foundgine.Abstractions;
using Foundgine.Planning.Mutation;
using Foundgine.Semantics;

namespace Foundgine.Execution.Mutation;

/// <summary>
/// Shapes a flat mutation batch result back into the semantic nested mutation
/// tree. Provider details and operation indexes remain internal to the result;
/// callers consume entity/field values and relationship children.
/// </summary>
public sealed class MutationResultMaterializer
{
    private readonly SemanticModel _model;

    public MutationResultMaterializer(SemanticModel model) =>
        _model = model ?? throw new ArgumentNullException(nameof(model));

    public MutationMaterializedResult Materialize(
        NestedMutationIntent intent,
        MutationBatchResult result)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(result);

        var expectedOperations = Count(intent);
        if (result.Results.Count != expectedOperations)
        {
            throw new InvalidOperationException(
                $"Mutation result contains {result.Results.Count} operations, but the nested mutation contains {expectedOperations}.");
        }

        var nextOperation = 0;
        var roots = new List<MutationMaterializedNode>(1);
        AddNode(intent, result.Results, ref nextOperation, roots);

        if (nextOperation != result.Results.Count)
            throw new InvalidOperationException("Nested mutation result did not consume every operation result.");

        return new MutationMaterializedResult(roots);
    }

    private void AddNode(
        NestedMutationIntent intent,
        IReadOnlyList<MutationResult> results,
        ref int operationIndex,
        List<MutationMaterializedNode> siblings)
    {
        var currentIndex = operationIndex++;
        var entity = _model.Get(intent.Mutation.Entity);
        var result = results[currentIndex];
        var values = result.ReturnedValues is null
            ? new Dictionary<FieldId, object?>()
            : new Dictionary<FieldId, object?>(result.ReturnedValues);

        var node = new MutationMaterializedNode(currentIndex, entity.Id, values);
        siblings.Add(node);

        foreach (var child in intent.Children)
        {
            var relationship = entity.Relationships.FirstOrDefault(x => x.Id == child.Relationship)
                ?? throw new InvalidOperationException(
                    $"Entity '{entity.Name}' has no relationship '{child.Relationship.Value}'.");

            if (relationship.Target != child.Mutation.Mutation.Entity)
            {
                throw new InvalidOperationException(
                    $"Relationship '{relationship.Name}' targets '{relationship.Target.Value}', " +
                    $"but nested result targets '{child.Mutation.Mutation.Entity.Value}'.");
            }

            AddNode(
                child.Mutation,
                results,
                ref operationIndex,
                node.GetChildren(relationship.Id));
        }
    }

    private static int Count(NestedMutationIntent intent) =>
        1 + intent.Children.Sum(x => Count(x.Mutation));
}
