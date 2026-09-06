using Foundgine.Core.Abstractions;
using Foundgine.Core.Semantic.Query;

namespace Foundgine.Core.Semantic.Mutation;

/// <summary>
///     Small factory for constructing canonical semantic mutation operations.
///     Validation of schema-specific legality remains a planner concern.
/// </summary>
public static class SemanticMutationBuilder
{
    public static SemanticMutationOperation Create(
        EntityId entity,
        IReadOnlyList<SemanticMutationField> fields,
        IReadOnlyList<FieldId>? returnFields = null)
    {
        return new SemanticMutationOperation(
            entity,
            SemanticMutationKind.Create,
            fields,
            null,
            Array.Empty<FieldId>(),
            returnFields ?? Array.Empty<FieldId>(),
            BuildEffects(entity, SemanticMutationKind.Create, fields),
            Array.Empty<SemanticMutationDependency>());
    }

    public static SemanticMutationOperation Update(
        EntityId entity,
        IReadOnlyList<SemanticMutationField> fields,
        SemanticFilterExpression? filter = null,
        IReadOnlyList<FieldId>? returnFields = null)
    {
        return new SemanticMutationOperation(
            entity,
            SemanticMutationKind.Update,
            fields,
            filter,
            Array.Empty<FieldId>(),
            returnFields ?? Array.Empty<FieldId>(),
            BuildEffects(entity, SemanticMutationKind.Update, fields),
            Array.Empty<SemanticMutationDependency>());
    }

    public static SemanticMutationOperation Delete(
        EntityId entity,
        SemanticFilterExpression filter,
        IReadOnlyList<FieldId>? returnFields = null)
    {
        return new SemanticMutationOperation(
            entity,
            SemanticMutationKind.Delete,
            Array.Empty<SemanticMutationField>(),
            filter,
            Array.Empty<FieldId>(),
            returnFields ?? Array.Empty<FieldId>(),
            BuildEffects(entity, SemanticMutationKind.Delete, Array.Empty<SemanticMutationField>()),
            Array.Empty<SemanticMutationDependency>());
    }

    public static SemanticMutationOperation Upsert(
        EntityId entity,
        IReadOnlyList<SemanticMutationField> fields,
        IReadOnlyList<FieldId> conflictFields,
        IReadOnlyList<FieldId>? returnFields = null)
    {
        return new SemanticMutationOperation(
            entity,
            SemanticMutationKind.Upsert,
            fields,
            null,
            conflictFields,
            returnFields ?? Array.Empty<FieldId>(),
            BuildEffects(entity, SemanticMutationKind.Upsert, fields),
            Array.Empty<SemanticMutationDependency>());
    }

    private static IReadOnlyList<SemanticMutationEffect> BuildEffects(
        EntityId entity,
        SemanticMutationKind kind,
        IReadOnlyList<SemanticMutationField> fields)
    {
        var effects = new List<SemanticMutationEffect>
        {
            new(
                kind switch
                {
                    SemanticMutationKind.Create => SemanticMutationEffectKind.CreateEntity,
                    SemanticMutationKind.Update => SemanticMutationEffectKind.UpdateEntity,
                    SemanticMutationKind.Delete => SemanticMutationEffectKind.DeleteEntity,
                    SemanticMutationKind.Upsert => SemanticMutationEffectKind.UpsertEntity,
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                },
                entity)
        };

        effects.AddRange(fields.Select(x =>
            new SemanticMutationEffect(
                SemanticMutationEffectKind.SetField,
                entity,
                x.Field)));

        return effects;
    }
}