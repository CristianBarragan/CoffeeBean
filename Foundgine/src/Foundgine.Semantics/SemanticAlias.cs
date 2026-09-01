namespace Foundgine.Semantics;

/// <summary>
/// A historical semantic name that resolves to the owning canonical declaration.
/// Aliases never change the declaration's stable identity.
/// </summary>
public sealed record SemanticAlias(string Name)
{
    public override string ToString() => Name;
}
