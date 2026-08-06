using System;
using CoffeeBeanery.GraphQL.Core.Foundation;
using CoffeeBeanery.GraphQL.Core.Runtime;

namespace CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

/// <summary>
/// Resolves which SQL alias a storage entity maps to within a specific
/// QueryPlan -- needed anywhere a field belonging to a joined (not root)
/// entity needs to be referenced in hand-written SQL (ordering; later,
/// navigation filters). QueryPlan itself doesn't index this by entity, so
/// this is a linear scan of Joins, matching QueryPlanTranslator's own
/// alias-per-join convention.
///
/// Throws if the entity is absent (not joined into this particular query)
/// or ambiguous (joined in more than once -- e.g. the same entity reached
/// via two different navigation paths in one query). An ambiguous match
/// has no single correct alias to pick, so this fails loudly rather than
/// silently choosing one and risking a wrong-but-plausible result.
/// </summary>
public static class QueryPlanAliasResolver
{
    public static string ResolveAlias(
        in QueryPlan plan,
        ushort storageEntityId)
    {
        var found =
            (string? Alias, int Count)
            (null, 0);

        if (plan.RootStorageEntityId == storageEntityId)
        {
            found = (plan.RootAlias, found.Count + 1);
        }

        foreach (var join in plan.Joins)
        {
            if (join.ChildStorageEntityId == storageEntityId)
            {
                found = (join.ChildAlias, found.Count + 1);
            }
        }

        if (found.Count == 0)
        {
            throw new InvalidOperationException(
                $"Storage entity id {storageEntityId} is not part of this " +
                "query's plan -- it isn't the root and wasn't joined in, " +
                "so it has no SQL alias to reference here.");
        }

        if (found.Count > 1)
        {
            throw new InvalidOperationException(
                $"Storage entity id {storageEntityId} is joined into this " +
                "query more than once (e.g. reached via two different " +
                "navigation paths) -- there is no single correct alias to " +
                "use, and picking one arbitrarily risks a silently wrong " +
                "result.");
        }

        return found.Alias!;
    }
}
