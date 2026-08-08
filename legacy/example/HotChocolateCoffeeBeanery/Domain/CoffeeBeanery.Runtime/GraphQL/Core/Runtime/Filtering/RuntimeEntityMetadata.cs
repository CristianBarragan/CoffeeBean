using System;
using System.Collections.Generic;

namespace CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

public sealed class RuntimeEntityMetadata
{
    public ushort EntityId { get; }

    public string Name { get; }


    public Dictionary<ushort, RuntimeFieldMetadata> Fields { get; }


    public List<RuntimeNavigationMetadata> Navigations { get; }



    public RuntimeEntityMetadata(
        ushort entityId,
        string name,
        Dictionary<ushort, RuntimeFieldMetadata> fields,
        List<RuntimeNavigationMetadata> navigations)
    {
        EntityId = entityId;
        Name = name;
        Fields = fields;
        Navigations = navigations;
    }
}



public sealed class RuntimeFieldMetadata
{
    public ushort FieldId { get; }

    public string Name { get; }

    public ushort ColumnId { get; }

    public ushort StorageEntityId { get; }



    public RuntimeFieldMetadata(
        ushort fieldId,
        string name,
        ushort columnId,
        ushort storageEntityId)
    {
        FieldId = fieldId;
        Name = name;
        ColumnId = columnId;
        StorageEntityId = storageEntityId;
    }
}



public sealed class RuntimeNavigationMetadata
{
    public string NavigationName { get; }

    public ushort TargetEntityId { get; }


    /*
     * Later this will contain:
     *
     * NavigationJoinPath[]
     *
     * from EntityNavigationConvention.
     */
    public object? JoinInformation { get; }



    public RuntimeNavigationMetadata(
        string navigationName,
        ushort targetEntityId,
        object? joinInformation)
    {
        NavigationName = navigationName;
        TargetEntityId = targetEntityId;
        JoinInformation = joinInformation;
    }
}