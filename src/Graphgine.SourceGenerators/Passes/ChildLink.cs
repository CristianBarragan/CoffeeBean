using System.Collections.Generic;

namespace Graphgine.SourceGenerators.Passes;

internal sealed class ChildLink
{
    public required string NavigationName { get; init; }
    public required string ChildModelName { get; init; }
    public required string ChildEntityName { get; init; } 
    public required List<ChildLinkHop> Hops { get; init; }
    public bool IsCollection { get; init; }
}

internal sealed class ChildLinkHop
{
    public required string FromEntityName { get; init; }
    public required string FromColumn { get; init; }
    public required string ToEntityName { get; init; }
    public required string ToColumn { get; init; }
}
