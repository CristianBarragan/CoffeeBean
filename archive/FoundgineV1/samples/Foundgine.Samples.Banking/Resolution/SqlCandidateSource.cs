using Foundgine.Metadata;
using Foundgine.Semantic;
using Foundgine.Semantic.Resolution;
using Microsoft.Data.Sqlite;

namespace Foundgine.Samples.Banking.Resolution;

/// <summary>
/// The real <see cref="ICandidateSource"/> for the Banking sample: it
/// answers every resolution lookup with a query against the same SQLite
/// database <c>Foundgine.Providers.SqlExecutionProvider</c> executes
/// against in Program.cs -- no fakes, no in-memory duplicate of the data,
/// consistent with every other proof in this sample.
///
/// It is deliberately generic over the domain rather than hardcoding
/// "Customer"/"Account"/"Transaction": table and column names come from
/// <see cref="MetadataRegistry"/>, and relationship traversal comes from
/// <see cref="JoinGraph"/> -- the same dynamic-discovery approach
/// <c>Foundgine.Planning.QueryPlanner</c> uses, applied to resolution
/// instead of planning.
///
/// One convention this class relies on, because nothing in
/// <see cref="Foundgine.Metadata"/> formally binds the two today: a
/// <see cref="Semantic.SemanticField"/> or
/// <see cref="Semantic.SemanticIdentity"/>'s <see cref="FieldId"/> is
/// assumed to share its numeric <c>Value</c> with the
/// <see cref="ColumnId"/> it corresponds to -- true for every field
/// <see cref="BankingSemanticModel"/> declares today. A domain compiler
/// (Milestone 10) or a formal <see cref="FieldBinding"/> would remove this
/// assumption; hand-authoring it here is the same trade-off Milestone 1
/// already made for the rest of this sample.
/// </summary>
public sealed class SqlCandidateSource : ICandidateSource
{
    private readonly string _connectionString;
    private readonly SemanticModel _semanticModel;
    private readonly MetadataRegistry _registry;
    private readonly JoinGraph _joins;

    public SqlCandidateSource(
        string connectionString,
        SemanticModel semanticModel,
        MetadataRegistry registry,
        JoinGraph joins)
    {
        _connectionString = connectionString;
        _semanticModel = semanticModel;
        _registry = registry;
        _joins = joins;
    }

    public IReadOnlyList<IdentityCandidate> FindByIdentity(EntityId entityType, string identityValue)
    {
        var semanticEntity = _semanticModel.Get(entityType);
        var metadata = _registry.Get(entityType);
        var idColumn = ResolveColumnName(metadata, semanticEntity.Identity.FieldId);
        var labelColumn = ResolveDisplayColumn(semanticEntity, metadata);

        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText =
            $"SELECT {Quote(idColumn)}, {Quote(labelColumn)} " +
            $"FROM {Quote(metadata.EffectiveStorageName)} " +
            $"WHERE {Quote(idColumn)} = @value";

        command.Parameters.AddWithValue("@value", identityValue);

        return ReadCandidates(command);
    }

    public IReadOnlyList<IdentityCandidate> FindByField(
        EntityId entityType, FieldId fieldId, string text, SearchStrategy strategy)
    {
        var semanticEntity = _semanticModel.Get(entityType);
        var metadata = _registry.Get(entityType);
        var idColumn = ResolveColumnName(metadata, semanticEntity.Identity.FieldId);
        var searchColumn = ResolveColumnName(metadata, fieldId);
        var labelColumn = ResolveDisplayColumn(semanticEntity, metadata);

        var pattern = strategy switch
        {
            SearchStrategy.Exact => text,
            SearchStrategy.Prefix => text + "%",
            SearchStrategy.Fuzzy => "%" + text + "%",
            _ => throw new NotSupportedException($"{nameof(SqlCandidateSource)} does not know {strategy}.")
        };

        using var connection = Open();
        using var command = connection.CreateCommand();

        // SQLite's LIKE is case-insensitive for ASCII by default, which is
        // exactly what SearchStrategy.Fuzzy/Prefix want; Exact reuses the
        // same operator with no wildcard characters in the pattern, which
        // still matches only the whole value.
        command.CommandText =
            $"SELECT {Quote(idColumn)}, {Quote(labelColumn)} " +
            $"FROM {Quote(metadata.EffectiveStorageName)} " +
            $"WHERE {Quote(searchColumn)} LIKE @pattern";

        command.Parameters.AddWithValue("@pattern", pattern);

        return ReadCandidates(command);
    }

    public IReadOnlyList<IdentityCandidate> FindByRelationship(RelationshipId relationshipId, string sourceIdentityValue)
    {
        var (sourceEntity, relationship) = FindRelationshipOwner(relationshipId);
        var targetEntity = _semanticModel.Get(relationship.Target);

        if (!_joins.TryGetJoin(sourceEntity.Id, relationship.Target, out var join))
        {
            throw new InvalidOperationException(
                $"No join is registered between {sourceEntity.Name} and {targetEntity.Name} for " +
                $"relationship '{relationship.Name}' -- Foundgine.Metadata.JoinGraph and " +
                "Foundgine.Semantic have drifted apart for this domain.");
        }

        // The JoinGraph invariant this relies on: Condition.Left always
        // names the "to" (target) entity's column, Condition.Right always
        // names the "from" (source) entity's column -- true for both a
        // directly-registered edge and JoinGraph's auto-added reverse edge.
        var targetMetadata = _registry.Get(relationship.Target);
        var foreignKeyColumn = ResolveColumnName(join.Condition.Left);
        var targetIdColumn = ResolveColumnName(targetMetadata, targetEntity.Identity.FieldId);
        var targetLabelColumn = ResolveDisplayColumn(targetEntity, targetMetadata);

        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText =
            $"SELECT {Quote(targetIdColumn)}, {Quote(targetLabelColumn)} " +
            $"FROM {Quote(targetMetadata.EffectiveStorageName)} " +
            $"WHERE {Quote(foreignKeyColumn)} = @value";

        command.Parameters.AddWithValue("@value", sourceIdentityValue);

        return ReadCandidates(command);
    }

    private (SemanticEntity Owner, SemanticRelationship Relationship) FindRelationshipOwner(RelationshipId relationshipId)
    {
        foreach (var entity in _semanticModel.Entities)
        {
            var relationship = entity.Relationships.FirstOrDefault(r => r.Id == relationshipId);
            if (relationship is not null)
                return (entity, relationship);
        }

        throw new InvalidOperationException(
            $"No entity in the semantic model declares relationship {relationshipId}.");
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static IReadOnlyList<IdentityCandidate> ReadCandidates(SqliteCommand command)
    {
        var results = new List<IdentityCandidate>();

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var id = reader.IsDBNull(0) ? "" : reader.GetValue(0).ToString() ?? "";
            var label = reader.IsDBNull(1) ? id : reader.GetValue(1).ToString() ?? id;
            results.Add(new IdentityCandidate(id, label));
        }

        return results;
    }

    /// <summary>
    /// Prefers the entity's declared <see cref="SearchCapability"/> field
    /// as the human-readable label (e.g. Customer.Name); falls back to the
    /// identity column itself for entities with nothing more descriptive
    /// (e.g. Account, Transaction).
    /// </summary>
    private static string ResolveDisplayColumn(SemanticEntity entity, EntityMetadata metadata)
    {
        var labelFieldId = entity.Search?.SearchableFields.FirstOrDefault() ?? entity.Identity.FieldId;
        return ResolveColumnName(metadata, labelFieldId);
    }

    private static string ResolveColumnName(EntityMetadata metadata, FieldId fieldId)
    {
        var column = metadata.Columns.FirstOrDefault(c => c.Id.Value == fieldId.Value);

        if (column is null)
        {
            throw new InvalidOperationException(
                $"Entity '{metadata.Name}' has no column whose ColumnId matches semantic field " +
                $"{fieldId} by numeric value -- SqlCandidateSource assumes FieldId and ColumnId " +
                "values are aligned for this hand-authored sample.");
        }

        return column.EffectiveStorageName;
    }

    private static string ResolveColumnName(ColumnReference reference) =>
        reference.Entity.Columns.First(c => c.Id.Value == reference.ColumnId).EffectiveStorageName;

    private static string Quote(string identifier) => $"\"{identifier}\"";
}
