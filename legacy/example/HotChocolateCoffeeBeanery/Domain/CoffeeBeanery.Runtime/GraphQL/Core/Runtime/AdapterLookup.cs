using System;
using System.Collections.Generic;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

/// <summary>
/// Lookup table injected into HotChocolateAdapter (which lives in the
/// CoffeeBeanery.GraphQL project). Deliberately kept here in Runtime and
/// NOT moved alongside the adapter: it has zero HotChocolate dependency,
/// it's pure generated-constant lookup data, and ProcessService's mutation
/// row-building path (MutationOperationBuilder) doesn't need it -- only
/// the AST-walking adapter code does. Provides the adapter with EntityId
/// and FieldId constants without the adapter needing to import generated
/// types directly -- keeps the adapter itself model-agnostic and testable
/// without a generated assembly.
///
/// Build one instance at startup from the generated constants (see
/// AdapterEmitter's generated `AdapterTables.Build()`).
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
