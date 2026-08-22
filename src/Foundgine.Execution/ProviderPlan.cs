namespace Foundgine.Execution;

/// <summary>
/// Opaque physical plan produced by a provider compiler. The core semantic
/// model never depends on its contents.
/// </summary>
public abstract record ProviderPlan(string Provider)
{
    /// <summary>
    /// Security execution certificate. It is intentionally internal so callers
    /// cannot transplant or forge a certificate by assigning it directly.
    /// </summary>
    public SecurityInvariantProof? SecurityProof { get; internal set; }
}
