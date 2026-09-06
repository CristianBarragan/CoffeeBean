using Foundgine.Core.Abstractions;
using Foundgine.Core.Semantic;

namespace Foundgine.Extensions.GraphQL.HotChocolate;

public sealed record GraphQLResultField(
    FieldId? Field,
    RelationshipId? Relationship,
    string GraphQLName,
    string Alias,
    GraphQLResultShape? Children = null);

public sealed record GraphQLResultShape(
    IReadOnlyList<GraphQLResultField> Fields);

public sealed record GraphQLQueryAdaptation(
    SemanticRequest Request,
    GraphQLResultShape Result);