using Foundgine.Metadata;

namespace Foundgine.Semantic;

/// <summary>
/// Declares that an entity can be located by ambiguous human language --
/// e.g. Customer is searchable by <c>Name</c>. Absence of a
/// <see cref="SearchCapability"/> on a <see cref="SemanticEntity"/> means
/// Milestone 2's resolver may only reach that entity through an explicit
/// identity or a relationship, never through free text.
/// </summary>
public sealed record SearchCapability(
    IReadOnlyList<FieldId> SearchableFields,
    SearchStrategy Strategy = SearchStrategy.Exact);
