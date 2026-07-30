using System;
using System.Collections.Immutable;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;
using Microsoft.CodeAnalysis;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;

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


        // Resolve actual property name first.
        // Do not invent XxxId names here.
        var resolvedColumn =
            ResolveForeignKeyConvention(
                storageEntityName,
                columnName);


        return $"ColumnId.{storageEntityName}.{resolvedColumn}";
    }


    private static string ResolveForeignKeyConvention(
        string entityName,
        string columnName)
    {
        if (string.Equals(
                columnName,
                "Id",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Id";
        }


        if (columnName.EndsWith(
                "Id",
                StringComparison.OrdinalIgnoreCase))
        {
            var prefix =
                columnName.Substring(
                    0,
                    columnName.Length - 2);


            if (string.Equals(
                    prefix,
                    entityName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Id";
            }
        }


        return columnName;
    }
}