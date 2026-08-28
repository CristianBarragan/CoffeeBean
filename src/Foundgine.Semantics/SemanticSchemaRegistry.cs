using Foundgine.Semantics.Capabilities;

namespace Foundgine.Semantics;

/// <summary>
/// Registry for generated and manually authored semantic schemas.
/// Registration is deterministic and rejects ambiguous schema/capability
/// definitions rather than silently overwriting the semantic authority.
/// </summary>
public sealed class SemanticSchemaRegistry
{
    private readonly Dictionary<string, SemanticSchema> _schemas =
        new(StringComparer.Ordinal);

    public SemanticSchemaRegistry Register(SemanticSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (_schemas.ContainsKey(schema.Name))
        {
            throw new InvalidOperationException(
                $"Semantic schema '{schema.Name}' is already registered.");
        }

        _schemas.Add(schema.Name, schema);
        return this;
    }

    /// <summary>Registers multiple schemas in declaration order.</summary>
    public SemanticSchemaRegistry RegisterRange(IEnumerable<SemanticSchema> schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);

        foreach (var schema in schemas)
            Register(schema);

        return this;
    }

    public IReadOnlyList<SemanticSchema> Schemas =>
        _schemas.Values
            .OrderBy(schema => schema.Name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Creates the unified semantic view while preserving each schema namespace.
    /// Entity identities are global semantic identities, so conflicting
    /// definitions are rejected during composition.
    /// </summary>
    public SemanticSchemaSet Build()
    {
        var schemas = Schemas;
        var entities = new Dictionary<Foundgine.Abstractions.EntityId, SemanticEntity>();
        var capabilities = new List<SemanticCapability>();
        var capabilityIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var schema in schemas)
        {
            foreach (var entity in schema.Model.Entities)
            {
                if (entities.TryGetValue(entity.Id, out var existing))
                {
                    if (!SemanticEntityCompatibility.AreEquivalent(existing, entity))
                    {
                        throw new InvalidOperationException(
                            $"Entity '{entity.Id}' is defined differently by multiple semantic schemas.");
                    }

                    continue;
                }

                entities.Add(entity.Id, entity);
            }

            foreach (var capability in schema.Capabilities)
            {
                var qualifiedId = $"{schema.Name}.{capability.Id}";
                if (!capabilityIds.Add(qualifiedId))
                {
                    throw new InvalidOperationException(
                        $"Semantic capability '{qualifiedId}' is defined more than once.");
                }

                capabilities.Add(capability with { Schema = schema.Name });
            }
        }

        var model = SemanticModel.FromEntities(entities);
        var definitions = capabilities
            .Select(SemanticCapabilityDefinition.From)
            .ToArray();

        return new SemanticSchemaSet(schemas, model, capabilities, definitions);
    }

    private static class SemanticEntityCompatibility
    {
        public static bool AreEquivalent(SemanticEntity left, SemanticEntity right) =>
            left.Id == right.Id &&
            StringComparer.Ordinal.Equals(left.Name, right.Name) &&
            left.Identity == right.Identity &&
            FieldsEqual(left.Fields, right.Fields) &&
            RelationshipsEqual(left.Relationships, right.Relationships);

        private static bool FieldsEqual(IReadOnlyList<SemanticField> left, IReadOnlyList<SemanticField> right)
        {
            if (left.Count != right.Count)
                return false;

            for (var i = 0; i < left.Count; i++)
            {
                var a = left[i];
                var b = right[i];
                if (a.Id != b.Id ||
                    !StringComparer.Ordinal.Equals(a.Name, b.Name) ||
                    a.ClrType != b.ClrType ||
                    a.EffectiveSemanticType != b.EffectiveSemanticType ||
                    a.Capabilities != b.Capabilities)
                    return false;
            }

            return true;
        }

        private static bool RelationshipsEqual(IReadOnlyList<SemanticRelationship> left, IReadOnlyList<SemanticRelationship> right)
        {
            if (left.Count != right.Count)
                return false;

            for (var i = 0; i < left.Count; i++)
            {
                var a = left[i];
                var b = right[i];
                if (a.Id != b.Id ||
                    !StringComparer.Ordinal.Equals(a.Name, b.Name) ||
                    a.Target != b.Target ||
                    a.Cardinality != b.Cardinality)
                    return false;
            }

            return true;
        }
    }
}

/// <summary>
/// Immutable composed view over all registered semantic schemas.
/// </summary>
public sealed class SemanticSchemaSet
{
    internal SemanticSchemaSet(
        IReadOnlyList<SemanticSchema> schemas,
        SemanticModel model,
        IReadOnlyList<Capabilities.SemanticCapability> capabilities,
        IReadOnlyList<Capabilities.SemanticCapabilityDefinition> definitions)
    {
        Schemas = schemas;
        Model = model;
        Capabilities = capabilities;
        Definitions = definitions;
    }

    public IReadOnlyList<SemanticSchema> Schemas { get; }

    public SemanticModel Model { get; }

    public IReadOnlyList<Capabilities.SemanticCapability> Capabilities { get; }

    /// <summary>Authoritative capability definitions shared by all downstream projections.</summary>
    public IReadOnlyList<Capabilities.SemanticCapabilityDefinition> Definitions { get; }

    public bool TryGetSchema(string name, out SemanticSchema schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        schema = Schemas.FirstOrDefault(x => StringComparer.Ordinal.Equals(x.Name, name))!;
        return schema is not null;
    }

    public SemanticSchema GetSchema(string name) =>
        TryGetSchema(name, out var schema)
            ? schema
            : throw new KeyNotFoundException($"Semantic schema '{name}' is not registered.");

    public bool TryGetCapability(string id, out Capabilities.SemanticCapability capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        capability = Capabilities.FirstOrDefault(x =>
            StringComparer.Ordinal.Equals(x.Id, id) ||
            StringComparer.Ordinal.Equals(x.QualifiedName, id))!;
        return capability is not null;
    }

    public Capabilities.SemanticCapability GetCapability(string id) =>
        TryGetCapability(id, out var capability)
            ? capability
            : throw new KeyNotFoundException($"Semantic capability '{id}' is not registered.");

    public bool TryGetDefinition(string qualifiedName, out Capabilities.SemanticCapabilityDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);
        definition = Definitions.FirstOrDefault(x => StringComparer.Ordinal.Equals(x.QualifiedName, qualifiedName))!;
        return definition is not null;
    }

    public Capabilities.SemanticCapabilityDefinition GetDefinition(string qualifiedName) =>
        TryGetDefinition(qualifiedName, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Semantic capability '{qualifiedName}' is not registered.");
}
