using Foundgine.Planning.Mutation;

namespace Foundgine.Sql.Query;

public sealed record SqlParameterBinding(
    string Name,
    object? Value,
    MutationValueReference? Source = null,
    string? ContextPath = null);
