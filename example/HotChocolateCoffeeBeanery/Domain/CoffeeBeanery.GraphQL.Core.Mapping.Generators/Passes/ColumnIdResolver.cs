using System;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;
using Microsoft.CodeAnalysis;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes;

internal static class ColumnIdResolver
{
    public static string Resolve(
        INamedTypeSymbol entityType,
        string columnName)
    {
        var entityName =
            IdEmitter.StripEntitySuffix(entityType.Name);

        return Resolve(entityName, columnName);
    }


    public static string Resolve(
        string entityName,
        string columnName)
    {
        var storageEntityName =
            IdEmitter.StripEntitySuffix(entityName);


        // Primary key of storage entity
        if (string.Equals(
                columnName,
                "Id",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"ColumnId.{storageEntityName}.Id";
        }


        // EntityId -> Id FK convention should never emit EntityId
        if (string.Equals(
                columnName,
                storageEntityName + "Id",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"ColumnId.{storageEntityName}.Id";
        }


        return $"ColumnId.{storageEntityName}.{columnName}";
    }
}