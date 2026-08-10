namespace Foundgine.Semantic;

/// <summary>
/// How many <see cref="SemanticRelationship.Target"/> instances a single
/// source entity instance can reach -- e.g. Customer -> Accounts is
/// <see cref="Many"/>, Account -> Customer (the inverse) is
/// <see cref="One"/>.
///
/// <see cref="Foundgine.Metadata.RelationshipMetadata"/> doesn't carry
/// this; it belongs at the semantic layer because it's about how the
/// domain is *talked about* ("her accounts" vs "her manager"), not about
/// how the join is physically expressed.
/// </summary>
public enum RelationshipCardinality
{
    One,
    Many
}
