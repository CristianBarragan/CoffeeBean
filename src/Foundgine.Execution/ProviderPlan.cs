namespace Foundgine.Execution;

/// <summary>
/// Opaque physical plan produced by a provider compiler. The core semantic
/// model never depends on its contents.
/// </summary>
public abstract record ProviderPlan(string Provider);
