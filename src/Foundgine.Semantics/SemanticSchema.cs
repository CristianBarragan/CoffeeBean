using Foundgine.Semantics.Capabilities;

namespace Foundgine.Semantics;

/// <summary>
/// A named semantic namespace containing a semantic model and the capabilities
/// exposed from that model. Schemas are the consumer-neutral boundary from
/// which adapters such as Agent Framework, MCP and GraphQL can be generated.
/// </summary>
public sealed class SemanticSchema
{
    public SemanticSchema(
        string name,
        SemanticModel model,
        IEnumerable<SemanticCapability>? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(model);

        Name = name.Trim();
        Model = model;
        Capabilities = (capabilities ?? [])
            .Select(capability => capability with { Schema = Name })
            .ToArray();

        ValidateCapabilities();
    }

    public string Name { get; }

    public SemanticModel Model { get; }

    public IReadOnlyList<SemanticCapability> Capabilities { get; }

    private void ValidateCapabilities()
    {
        var duplicate = Capabilities
            .GroupBy(capability => capability.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
            throw new InvalidOperationException(
                $"Semantic schema '{Name}' contains duplicate capability '{duplicate.Key}'.");

        foreach (var capability in Capabilities)
        {
            if (!Model.TryGet(capability.TargetEntityId, out _))
            {
                throw new InvalidOperationException(
                    $"Semantic capability '{capability.Id}' targets unknown entity '{capability.TargetEntityId}' in schema '{Name}'.");
            }
        }
    }
}
