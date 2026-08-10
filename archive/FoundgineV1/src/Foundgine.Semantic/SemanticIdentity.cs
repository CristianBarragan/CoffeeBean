using Foundgine.Metadata;

namespace Foundgine.Semantic;

/// <summary>
/// The field that uniquely identifies an instance of a
/// <see cref="SemanticEntity"/> within the semantic domain -- e.g.
/// Customer's <c>Id</c>. Every entity must declare exactly one.
///
/// Milestone 2 (semantic resolution) is the reason this exists: mapping
/// "account 10" to an explicit domain reference means first knowing which
/// field on Account *is* "10".
/// </summary>
public sealed record SemanticIdentity(FieldId FieldId, string Name);
