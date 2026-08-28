namespace Foundgine.Semantics.Capabilities;

/// <summary>
/// Registry of authoritative capability definitions. Consumers register and
/// resolve these definitions; they do not create parallel tool-specific
/// capability descriptions.
/// </summary>
public sealed class SemanticCapabilityRegistry
{
    private readonly Dictionary<string, SemanticCapability> _capabilities =
        new(StringComparer.Ordinal);

    public SemanticCapabilityRegistry Register(SemanticCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (string.IsNullOrWhiteSpace(capability.Schema))
            throw new InvalidOperationException($"Capability '{capability.Id}' must belong to a semantic schema.");

        var key = capability.QualifiedName;
        if (!_capabilities.TryAdd(key, capability with { Schema = capability.Schema.Trim() }))
            throw new InvalidOperationException($"Semantic capability '{key}' is already registered.");

        return this;
    }

    public SemanticCapabilityRegistry RegisterRange(IEnumerable<SemanticCapability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        foreach (var capability in capabilities)
            Register(capability);
        return this;
    }

    public IReadOnlyList<SemanticCapability> Capabilities =>
        _capabilities.Values.OrderBy(x => x.QualifiedName, StringComparer.Ordinal).ToArray();

    public bool TryGet(string qualifiedName, out SemanticCapability capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);
        return _capabilities.TryGetValue(qualifiedName, out capability!);
    }

    public SemanticCapability Get(string qualifiedName) =>
        TryGet(qualifiedName, out var capability)
            ? capability
            : throw new KeyNotFoundException($"Semantic capability '{qualifiedName}' is not registered.");
}
