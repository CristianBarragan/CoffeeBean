using Foundgine.Core.Semantic.Mutation;
using Foundgine.Core.Semantic.Query;

namespace Foundgine.Providers.Aot;

/// <summary>
///     Fluent semantic operations for source-generated application fields.
/// </summary>
public static class GeneratedSemanticFieldExtensions
{
    public static SemanticFieldFilter Eq(this GeneratedSemanticField field, object? value)
    {
        return new SemanticFieldFilter(field.Id, SemanticFilterOperator.Eq, value);
    }

    public static SemanticFieldFilter Neq(this GeneratedSemanticField field, object? value)
    {
        return new SemanticFieldFilter(field.Id, SemanticFilterOperator.Neq, value);
    }

    public static SemanticFieldFilter In(this GeneratedSemanticField field, params object?[] values)
    {
        return new(field.Id, SemanticFilterOperator.In, values);
    }

    public static SemanticMutationField Set(this GeneratedSemanticField field, object? value)
    {
        return new SemanticMutationField(field.Id, value);
    }

    public static SemanticOrderTerm Asc(this GeneratedSemanticField field)
    {
        return new SemanticOrderTerm(field.Id, SemanticSortDirection.Asc);
    }

    public static SemanticOrderTerm Desc(this GeneratedSemanticField field)
    {
        return new SemanticOrderTerm(field.Id, SemanticSortDirection.Desc);
    }
}