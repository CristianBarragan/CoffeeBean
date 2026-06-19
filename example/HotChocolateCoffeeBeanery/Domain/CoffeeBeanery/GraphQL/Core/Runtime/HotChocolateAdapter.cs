using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using CoffeeBeanery.GraphQL.Core.Runtime;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

/// <summary>
/// Converts HotChocolate AST nodes into SelectionIR / MutationIR.
///
/// This is the ONLY place in the runtime that knows about HotChocolate.
/// Once the adapter returns, no HotChocolate type ever appears again.
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

        WalkSelectionSet(
            selectionSet, rootEntityId, rootOutputAlias,
            scalars, children, lookup, isConditional: false);

        return new SelectionIR(
            rootEntityId,
            rootOutputAlias,
            isConditional: false,
            scalars.ToImmutable(),
            children.ToImmutable());
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
        bool isConditional)
    {
        foreach (var selection in set.Selections)
        {
            switch (selection)
            {
                case FieldNode field:
                    WalkField(field, entityId, outputAlias,
                        scalars, children, lookup,
                        isConditional || HasConditionDirective(field));
                    break;

                case InlineFragmentNode fragment:
                    // Unwrap inline fragments transparently.
                    if (fragment.SelectionSet is not null)
                        WalkSelectionSet(fragment.SelectionSet, entityId, outputAlias,
                            scalars, children, lookup, isConditional);
                    break;

                // Named fragments (FragmentSpreadNode) are not supported in v1.
                // A FragmentExpander pass should run before the adapter.
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
        bool isConditional)
    {
        // The wire name the client used (may differ from schema name when aliased).
        var wireAlias  = field.Alias?.Value;
        var schemaName = field.Name.Value;

        // Meta-fields are handled outside the planner.
        if (MetaFields.Contains(schemaName))
            return;

        // Connection wrappers are transparent: lift their children up.
        if (ConnectionWrappers.Contains(schemaName))
        {
            if (field.SelectionSet is not null)
                WalkSelectionSet(field.SelectionSet, entityId, parentOutputAlias,
                    scalars, children, lookup, isConditional);
            return;
        }

        // Resolve the schema name → EntityId for child entities.
        if (field.SelectionSet is not null)
        {
            // This field is an object/entity selection.
            if (lookup.TryGetChildEntityId(entityId, schemaName, out var childEntityId))
            {
                // The output alias is either the client's alias or the schema name,
                // normalized to PascalCase so it matches the mapping convention.
                var childOutputAlias = wireAlias is not null
                    ? ToPascalCase(wireAlias)
                    : ToPascalCase(schemaName);

                var childScalars  = ImmutableArray.CreateBuilder<ScalarSelection>(8);
                var childChildren = ImmutableArray.CreateBuilder<SelectionIR>(4);

                WalkSelectionSet(field.SelectionSet, childEntityId, childOutputAlias,
                    childScalars, childChildren, lookup, isConditional);

                children.Add(new SelectionIR(
                    childEntityId,
                    childOutputAlias,
                    isConditional,
                    childScalars.ToImmutable(),
                    childChildren.ToImmutable()));
            }
            else
            {
                // Unknown child entity - treat as transparent wrapper and
                // walk its children in the current entity's context.
                WalkSelectionSet(field.SelectionSet, entityId, parentOutputAlias,
                    scalars, children, lookup, isConditional);
            }

            return;
        }

        // Scalar field — resolve schema name → FieldId.
        if (lookup.TryGetFieldId(entityId, schemaName, out var fieldId))
        {
            var scalarOutputAlias = wireAlias ?? schemaName;
            scalars.Add(new ScalarSelection(fieldId, scalarOutputAlias));
        }
        // Unknown scalar fields are silently skipped — they may be extension
        // fields handled by IQueryPlanContributor after the planner runs.
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
        foreach (var field in obj.Fields)
        {
            var name = field.Name.Value;

            switch (field.Value)
            {
                case ObjectValueNode childObj:
                    // Nested entity input.
                    if (lookup.TryGetChildEntityId(entityId, name, out var childEntityId))
                    {
                        var childOutputAlias = ToPascalCase(name);
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
                        // Unknown object — walk it in the current entity context.
                        WalkMutationObject(childObj, entityId, outputAlias,
                            values, children, lookup);
                    }
                    break;

                case ListValueNode listNode:
                    // List of entity inputs.
                    if (lookup.TryGetChildEntityId(entityId, name, out var listEntityId))
                    {
                        var listOutputAlias = ToPascalCase(name);
                        foreach (var item in listNode.Items)
                        {
                            if (item is not ObjectValueNode itemObj) continue;

                            var itemValues   = ImmutableArray.CreateBuilder<FieldValue>(8);
                            var itemChildren = ImmutableArray.CreateBuilder<MutationIR>(4);

                            WalkMutationObject(itemObj, listEntityId, listOutputAlias,
                                itemValues, itemChildren, lookup);

                            children.Add(new MutationIR(
                                listEntityId,
                                listOutputAlias,
                                itemValues.ToImmutable(),
                                itemChildren.ToImmutable()));
                        }
                    }
                    break;

                default:
                    // Scalar value.
                    var rawValue = field.Value.Value?.ToString();
                    if (rawValue is null) break;

                    if (lookup.TryGetFieldId(entityId, name, out var fieldId))
                        values.Add(new FieldValue(fieldId, rawValue));
                    // Unknown scalars silently skipped — extension contributor territory.
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

/// <summary>
/// Lookup table injected into the adapter.
/// Provides the adapter with EntityId and FieldId constants without
/// the adapter needing to import generated types directly — keeps the
/// adapter itself model-agnostic and testable without a generated assembly.
///
/// Build one instance at startup from the generated constants.
/// </summary>
public sealed class AdapterLookup
{
    // [parentEntityId][camelCaseFieldName] → childEntityId
    private readonly Dictionary<ushort, Dictionary<string, ushort>> _childEntityIds;

    // [entityId][camelCaseFieldName] → fieldId
    private readonly Dictionary<ushort, Dictionary<string, ushort>> _fieldIds;

    public AdapterLookup(
        Dictionary<ushort, Dictionary<string, ushort>> childEntityIds,
        Dictionary<ushort, Dictionary<string, ushort>> fieldIds)
    {
        _childEntityIds = childEntityIds;
        _fieldIds = fieldIds;
    }

    public bool TryGetChildEntityId(ushort parentEntityId, string fieldName, out ushort childEntityId)
    {
        childEntityId = 0;
        return _childEntityIds.TryGetValue(parentEntityId, out var map) &&
               map.TryGetValue(fieldName, out childEntityId);
    }

    public bool TryGetFieldId(ushort entityId, string fieldName, out ushort fieldId)
    {
        fieldId = 0;
        return _fieldIds.TryGetValue(entityId, out var map) &&
               map.TryGetValue(fieldName, out fieldId);
    }

    public static AdapterLookup BuildFromGeneratedMetadata(
        IEnumerable<(string ParentName, string FieldName, string ChildName)> childLinks,
        IEnumerable<(ushort EntityId, string FieldName, ushort FieldId)> fieldLinks,
        IReadOnlyDictionary<string, ushort> entityNameToId)  // caller provides this
    {
        var childEntityIds = new Dictionary<ushort, Dictionary<string, ushort>>();

        foreach (var (parentName, fieldName, childName) in childLinks)
        {
            if (!entityNameToId.TryGetValue(parentName, out var parentId)) continue;
            if (!entityNameToId.TryGetValue(childName,  out var childId))  continue;

            if (!childEntityIds.TryGetValue(parentId, out var map))
                childEntityIds[parentId] = map = new Dictionary<string, ushort>(
                    StringComparer.OrdinalIgnoreCase);

            map[fieldName] = childId;
        }

        var fieldIds = new Dictionary<ushort, Dictionary<string, ushort>>();

        foreach (var (entityId, fieldName, fieldId) in fieldLinks)
        {
            if (!fieldIds.TryGetValue(entityId, out var map))
                fieldIds[entityId] = map = new Dictionary<string, ushort>(
                    StringComparer.OrdinalIgnoreCase);

            map[fieldName] = fieldId;
        }

        return new AdapterLookup(childEntityIds, fieldIds);
    }
}