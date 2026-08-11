using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Runtime;
using HotChocolate.Execution.Processing;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

/// <summary>
/// Converts HotChocolate AST nodes into SelectionIR / MutationIR.
///
/// This is the ONLY place that knows about HotChocolate -- it lives in the
/// CoffeeBeanery.GraphQL project specifically so that CoffeeBeanery.Runtime
/// and CoffeeBeanery.Runtime.Postgres (ProcessService's implementation)
/// never need a HotChocolate reference at all. Once the adapter returns,
/// no HotChocolate type ever appears again -- ProcessService consumes
/// plain SelectionIR/MutationIR/EntityFilterMetadata via
/// QueryRequest/MutationRequest/PagedQueryRequest (CoffeeBeanery.Runtime).
///
/// Adapter contract:
///   - Field aliases are resolved: "innerCustomer: customer" produces
///     EntityId = Customer, OutputAlias = "InnerCustomer".
///   - Inline fragments are unwrapped transparently.
///   - Named fragments are NOT supported in v1 — call a FragmentExpander
///     pass before the adapter if needed.
///   - Fields with @skip/@include are passed through as IsConditional = true.
///     The SelectionOptimizer removes them once variable values are known.
///   - Connection wrapper fields (nodes/edges) are transparent — their
///     children are lifted into the parent selection.
///   - Meta-fields (pageInfo, totalCount, __typename) are skipped;
///     they are handled outside the planner pipeline.
/// </summary>
public static class HotChocolateAdapter
{
    // Fields that wrap a connection but carry no entity content.
    private static readonly HashSet<string> ConnectionWrappers =
        new(StringComparer.OrdinalIgnoreCase) { "nodes", "edges", "node" };

    // Fields handled outside the planner pipeline entirely.
    private static readonly HashSet<string> MetaFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "pageInfo", "totalCount", "__typename",
            "hasNextPage", "hasPreviousPage", "startCursor", "endCursor"
        };

    // Non-entity arguments a mutation field carries alongside the entity
    // input object -- everything else on selection.SyntaxNode.Arguments
    // is assumed to be the entity input.
    private static readonly HashSet<string> NonEntityMutationArgs =
        new(StringComparer.OrdinalIgnoreCase) { "where", "order", "first", "last", "after", "before" };

    // ---------------------------------------------------------------
    // Query adapter
    // ---------------------------------------------------------------

    /// <summary>
    /// Converts a HotChocolate SelectionSetNode into a SelectionIR tree
    /// rooted at rootEntityId.
    ///
    /// rootOutputAlias is the wire alias the client used for the root
    /// field (e.g. "CustomerCustomerEdge" for a top-level edge query).
    /// </summary>
    public static SelectionIR AdaptQuery(
        ushort rootEntityId,
        string rootOutputAlias,
        SelectionSetNode selectionSet,
        AdapterLookup lookup)
    {
        var scalars = ImmutableArray.CreateBuilder<ScalarSelection>(8);
        var children = ImmutableArray.CreateBuilder<SelectionIR>(4);
        var usedAliases = new HashSet<string>(StringComparer.Ordinal);

        WalkSelectionSet(
            selectionSet, rootEntityId, rootOutputAlias,
            scalars, children, lookup, isConditional: false, usedAliases);

        return new SelectionIR(
            rootEntityId,
            rootOutputAlias,
            isConditional: false,
            scalars.ToImmutable(),
            children.ToImmutable());
    }

    /// <summary>
    /// Convenience wrapper: builds a SelectionIR directly from an
    /// ISelection's SyntaxNode.SelectionSet. What QueryRequest/
    /// MutationRequest/PagedQueryRequest callers (the Wrapper*Resolver
    /// classes) should use.
    /// </summary>
    public static SelectionIR AdaptQuery(
        ushort rootEntityId,
        string rootOutputAlias,
        ISelection selection,
        AdapterLookup lookup)
    {
        var selectionSet =
            selection.SyntaxNode.SelectionSet
            ?? throw new InvalidOperationException(
                "Selection has no SelectionSet.");

        return AdaptQuery(rootEntityId, rootOutputAlias, selectionSet, lookup);
    }


    // ---------------------------------------------------------------
    // Mutation adapter
    // ---------------------------------------------------------------

    /// <summary>
    /// Converts a HotChocolate ObjectValueNode (mutation input) into a
    /// MutationIR tree rooted at rootEntityId.
    /// </summary>
    public static MutationIR AdaptMutation(
        ushort rootEntityId,
        string rootOutputAlias,
        ObjectValueNode inputNode,
        AdapterLookup lookup)
    {
        var values   = ImmutableArray.CreateBuilder<FieldValue>(8);
        var children = ImmutableArray.CreateBuilder<MutationIR>(4);

        WalkMutationObject(
            inputNode, rootEntityId, rootOutputAlias,
            values, children, lookup);

        return new MutationIR(
            rootEntityId,
            rootOutputAlias,
            values.ToImmutable(),
            children.ToImmutable());
    }

    /// <summary>
    /// Finds the mutation's entity-input argument on an ISelection (the
    /// one argument that isn't where/order/first/last/after/before) and
    /// adapts it -- handling both the single-object and list-of-objects
    /// shapes. This is exactly the argument-discovery logic that used to
    /// live inline in ProcessService.MutationProcessAsync; it moved here
    /// so ProcessService never has to look at ISelection/AST node types
    /// at all. Returns an empty list if there's no recognizable
    /// entity-input argument.
    /// </summary>
    public static List<MutationIR> AdaptMutationRequest(
        ISelection selection,
        ushort rootEntityId,
        string rootOutputAlias,
        AdapterLookup lookup)
    {
        var mutations = new List<MutationIR>();

        var mutationArg =
            selection.SyntaxNode.Arguments
                .FirstOrDefault(a => !NonEntityMutationArgs.Contains(a.Name.Value));

        if (mutationArg?.Value is not ObjectValueNode wrapperObj)
            return mutations;

        var entityFieldName =
            char.ToLowerInvariant(rootOutputAlias[0])
            + rootOutputAlias.Substring(1);

        var entityField =
            wrapperObj.Fields.FirstOrDefault(f =>
                string.Equals(
                    f.Name.Value,
                    entityFieldName,
                    StringComparison.OrdinalIgnoreCase));

        switch (entityField?.Value)
        {
            case ObjectValueNode obj:

                mutations.Add(
                    AdaptMutation(rootEntityId, rootOutputAlias, obj, lookup));

                break;

            case ListValueNode list:

                foreach (var item in list.Items)
                {
                    if (item is ObjectValueNode itemObj)
                    {
                        mutations.Add(
                            AdaptMutation(rootEntityId, rootOutputAlias, itemObj, lookup));
                    }
                }

                break;
        }

        return mutations;
    }

    // ---------------------------------------------------------------
    // Alias disambiguation
    // ---------------------------------------------------------------

    /// <summary>
    /// Ensures uniqueness among sibling OutputAliases at the same tree level.
    /// Client-supplied names only differ from HotChocolate's grammar by
    /// character set, not case — "innerCustomer" and "InnerCustomer" both
    /// PascalCase to "InnerCustomer" and would otherwise collide in the
    /// mutation row buffer, which keys rows on (EntityId, StorageEntityId,
    /// Alias). First occurrence keeps the plain name; subsequent collisions
    /// get a numeric suffix.
    /// </summary>
    private static string Disambiguate(string alias, HashSet<string> usedAliases)
    {
        if (usedAliases.Add(alias))
            return alias;

        var i = 2;
        string candidate;
        do
        {
            candidate = alias + i;
            i++;
        } while (!usedAliases.Add(candidate));

        return candidate;
    }

    // ---------------------------------------------------------------
    // Selection walking (query side)
    // ---------------------------------------------------------------

    private static void WalkSelectionSet(
        SelectionSetNode set,
        ushort entityId,
        string outputAlias,
        ImmutableArray<ScalarSelection>.Builder scalars,
        ImmutableArray<SelectionIR>.Builder children,
        AdapterLookup lookup,
        bool isConditional,
        HashSet<string> usedAliases)
    {
        foreach (var selection in set.Selections)
        {
            switch (selection)
            {
                case FieldNode field:
                    WalkField(field, entityId, outputAlias,
                        scalars, children, lookup,
                        isConditional || HasConditionDirective(field),
                        usedAliases);
                    break;

                case InlineFragmentNode fragment:
                    // Unwrapped transparently into the SAME selection — so it
                    // must share the enclosing level's usedAliases, not get its
                    // own. A field inside the fragment and a same-named field
                    // outside it are still siblings in the emitted tree.
                    if (fragment.SelectionSet is not null)
                        WalkSelectionSet(fragment.SelectionSet, entityId, outputAlias,
                            scalars, children, lookup, isConditional, usedAliases);
                    break;

                // Named fragments (FragmentSpreadNode) are not supported in v1.
            }
        }
    }

    private static void WalkField(
    FieldNode field,
    ushort entityId,
    string parentOutputAlias,
    ImmutableArray<ScalarSelection>.Builder scalars,
    ImmutableArray<SelectionIR>.Builder children,
    AdapterLookup lookup,
    bool isConditional,
    HashSet<string> usedAliases)
{
    var wireAlias  = field.Alias?.Value;
    var schemaName = field.Name.Value;

    if (MetaFields.Contains(schemaName))
        return;

    if (ConnectionWrappers.Contains(schemaName))
    {
        // Transparent like inline fragments — same reasoning, share the
        // parent's usedAliases rather than starting a fresh set.
        if (field.SelectionSet is not null)
            WalkSelectionSet(field.SelectionSet, entityId, parentOutputAlias,
                scalars, children, lookup, isConditional, usedAliases);
        return;
    }

    if (field.SelectionSet is not null)
    {
        if (lookup.TryGetChildEntityId(entityId, schemaName, out var childEntityId))
        {
            var baseAlias = wireAlias is not null
                ? ToPascalCase(wireAlias)
                : ToPascalCase(schemaName);

            var childOutputAlias = Disambiguate(baseAlias, usedAliases);

            var childScalars  = ImmutableArray.CreateBuilder<ScalarSelection>(8);
            var childChildren = ImmutableArray.CreateBuilder<SelectionIR>(4);
            var childUsedAliases = new HashSet<string>(StringComparer.Ordinal);

            WalkSelectionSet(field.SelectionSet, childEntityId, childOutputAlias,
                childScalars, childChildren, lookup, isConditional, childUsedAliases);

            children.Add(new SelectionIR(
                childEntityId,
                childOutputAlias,
                isConditional,
                childScalars.ToImmutable(),
                childChildren.ToImmutable()));
        }
        else
        {
            // Unknown child entity, transparent wrapper — same level, share aliases.
            WalkSelectionSet(field.SelectionSet, entityId, parentOutputAlias,
                scalars, children, lookup, isConditional, usedAliases);
        }

        return;
    }

    // Scalar field.
    if (lookup.TryGetFieldId(entityId, schemaName, out var fieldId))
    {
        var baseAlias = wireAlias ?? schemaName;
        var scalarOutputAlias = Disambiguate(baseAlias, usedAliases);
        scalars.Add(new ScalarSelection(fieldId, scalarOutputAlias));
    }
}


    // ---------------------------------------------------------------
    // Mutation walking
    // ---------------------------------------------------------------

    private static void WalkMutationObject(
    ObjectValueNode obj,
    ushort entityId,
    string outputAlias,
    ImmutableArray<FieldValue>.Builder values,
    ImmutableArray<MutationIR>.Builder children,
    AdapterLookup lookup)
{
    var usedAliases = new HashSet<string>(StringComparer.Ordinal);

    foreach (var field in obj.Fields)
    {
        var name = field.Name.Value;

        switch (field.Value)
        {
            case ObjectValueNode childObj:
                if (lookup.TryGetChildEntityId(entityId, name, out var childEntityId))
                {
                    var childOutputAlias = Disambiguate(ToPascalCase(name), usedAliases);
                    var childValues   = ImmutableArray.CreateBuilder<FieldValue>(8);
                    var childChildren = ImmutableArray.CreateBuilder<MutationIR>(4);

                    WalkMutationObject(childObj, childEntityId, childOutputAlias,
                        childValues, childChildren, lookup);

                    children.Add(new MutationIR(
                        childEntityId,
                        childOutputAlias,
                        childValues.ToImmutable(),
                        childChildren.ToImmutable()));
                }
                else
                {
                    WalkMutationObject(childObj, entityId, outputAlias,
                        values, children, lookup);
                }
                break;

            case ListValueNode listNode:
                if (lookup.TryGetChildEntityId(entityId, name, out var listEntityId))
                {
                    var listOutputAliasBase = ToPascalCase(name);
                    var itemIndex = 0;

                    foreach (var item in listNode.Items)
                    {
                        if (item is not ObjectValueNode itemObj) continue;

                        // List items share the field name by definition, so
                        // each item needs its own disambiguated alias too —
                        // "Items", "Items2", "Items3"... rather than every
                        // item silently colliding into one row.
                        var listOutputAlias = Disambiguate(listOutputAliasBase, usedAliases);

                        var itemValues   = ImmutableArray.CreateBuilder<FieldValue>(8);
                        var itemChildren = ImmutableArray.CreateBuilder<MutationIR>(4);

                        WalkMutationObject(itemObj, listEntityId, listOutputAlias,
                            itemValues, itemChildren, lookup);

                        children.Add(new MutationIR(
                            listEntityId,
                            listOutputAlias,
                            itemValues.ToImmutable(),
                            itemChildren.ToImmutable()));

                        itemIndex++;
                    }
                }
                break;

            default:
                var rawValue = field.Value.Value?.ToString();
                if (rawValue is null) break;

                if (lookup.TryGetFieldId(entityId, name, out var fieldId))
                    values.Add(new FieldValue(fieldId, rawValue));
                break;
        }
    }
}

    // ---------------------------------------------------------------
    // Directive helpers
    // ---------------------------------------------------------------

    private static bool HasConditionDirective(FieldNode field)
    {
        if (field.Directives is null || field.Directives.Count == 0)
            return false;

        foreach (var directive in field.Directives)
        {
            var name = directive.Name.Value;
            if (string.Equals(name, "skip",    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "include", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // ---------------------------------------------------------------
    // String helpers
    // ---------------------------------------------------------------

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (char.IsUpper(name[0])) return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}
