namespace Foundgine.SupplyChain.Semantic.Authorization;

/// <summary>Declarative source metadata used by the sample's generated semantic surface.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SemanticEntityAttribute : Attribute;

/// <summary>Declares the semantic policy name associated with a domain model or operation.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, Inherited = false)]
public sealed class SemanticPolicyAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Policy name is required.", nameof(name))
        : name;
}

/// <summary>Marks a domain property as intentionally exposed through generated semantics.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, Inherited = false)]
public sealed class SemanticFieldAttribute : Attribute;
