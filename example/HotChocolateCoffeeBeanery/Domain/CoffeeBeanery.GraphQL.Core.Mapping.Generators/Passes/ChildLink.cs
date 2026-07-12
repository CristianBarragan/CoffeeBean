namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes;

internal sealed class ChildLink
{
    public string NavigationName   { get; init; } = "";
    public string ChildModelName   { get; init; } = "";
    public string ParentEntityName { get; init; } = "";
    public string ChildEntityName  { get; init; } = "";
    public string ParentJoinColumn { get; init; } = "";
    public string ChildJoinColumn  { get; init; } = "";
    public bool   IsCollection     { get; init; }
}