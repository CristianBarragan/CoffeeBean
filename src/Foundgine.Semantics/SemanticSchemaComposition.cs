using Foundgine.Semantics.Capabilities;

namespace Foundgine.Semantics;

/// <summary>
/// Composes generated and manually-authored semantic schemas into the single
/// authoritative semantic view consumed by Foundgine. Generated metadata is
/// treated as authoritative for the domain entities it contributes; manual
/// schemas extend the graph with concepts that cannot be inferred from CLR types.
/// </summary>
public static class SemanticSchemaComposition
{
    /// <summary>
    /// Creates a registry containing the generated schema and any manual schemas,
    /// then materializes one immutable semantic view.
    /// </summary>
    public static SemanticSchemaSet Compose(
        string generatedSchemaName,
        SemanticModel generatedModel,
        IEnumerable<SemanticSchema>? manualSchemas = null,
        IEnumerable<SemanticCapability>? generatedCapabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedSchemaName);
        ArgumentNullException.ThrowIfNull(generatedModel);

        var registry = new SemanticSchemaRegistry()
            .Register(new SemanticSchema(
                generatedSchemaName,
                generatedModel,
                generatedCapabilities));

        if (manualSchemas is not null)
        {
            foreach (var schema in manualSchemas)
                registry.Register(schema);
        }

        return registry.Build();
    }

    /// <summary>
    /// Composes already materialized generated and manual schemas.
    /// </summary>
    public static SemanticSchemaSet Compose(
        SemanticSchema generatedSchema,
        IEnumerable<SemanticSchema>? manualSchemas = null)
    {
        ArgumentNullException.ThrowIfNull(generatedSchema);

        var registry = new SemanticSchemaRegistry().Register(generatedSchema);

        if (manualSchemas is not null)
        {
            foreach (var schema in manualSchemas)
                registry.Register(schema);
        }

        return registry.Build();
    }
}
