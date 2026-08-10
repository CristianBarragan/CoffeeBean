using Foundgine.Metadata;

namespace Foundgine.Semantic;

/// <summary>
/// A named path from one <see cref="SemanticEntity"/> to another --
/// e.g. Customer.Accounts, Account.Transactions. This is what
/// Milestone 2 walks to resolve phrases like "her checking account"
/// (Customer -> Accounts, filtered) and what Milestone 3 walks to build
/// a read plan ("resolve Account through Customer relationship").
/// </summary>
public sealed record SemanticRelationship(
    RelationshipId Id,
    string Name,
    EntityId Target,
    RelationshipCardinality Cardinality);
