using System;
using System.Collections.Generic;
using CoffeeBeanery.GraphQL.Core.Runtime.Filtering;
using CoffeeBeanery.GraphQL.Core.Runtime;

namespace Domain.Shared;

public sealed class SqlFilterEmitter
{
    private readonly SqlFilterParameterBag _parameters;


    public SqlFilterEmitter(
        SqlFilterParameterBag parameters)
    {
        _parameters = parameters;
    }


    public string Emit(
        EntityFilterMetadata filter,
        string tableAlias)
    {
        return filter switch
        {
            EntityFilterMetadata.Field field =>
                EmitField(
                    field,
                    tableAlias),


            EntityFilterMetadata.Navigation navigation =>
                EmitNavigation(
                    navigation,
                    tableAlias),


            EntityFilterMetadata.Collection collection =>
                EmitCollection(
                    collection,
                    tableAlias),


            EntityFilterMetadata.And and =>
                EmitAnd(
                    and,
                    tableAlias),


            EntityFilterMetadata.Or or =>
                EmitOr(
                    or,
                    tableAlias),


            _ =>
                throw new NotSupportedException(
                    filter.GetType().Name)
        };
    }



    private string EmitField(
        EntityFilterMetadata.Field filter,
        string alias)
    {
        var column =
            ColumnNameResolver.Resolve(
                filter.FieldMetadata.StorageEntityId,
                filter.FieldMetadata.ColumnId);


        return filter.Operator switch
        {
            FilterOperator.Eq =>
                $"{alias}.\"{column}\" = @{_parameters.Add(filter.Value)}",


            FilterOperator.Neq =>
                $"{alias}.\"{column}\" <> @{_parameters.Add(filter.Value)}",


            FilterOperator.In =>
                EmitIn(
                    alias,
                    column,
                    filter.Value),


            _ =>
                throw new NotSupportedException(
                    $"Operator {filter.Operator}")
        };
    }



    private string EmitNavigation(
        EntityFilterMetadata.Navigation filter,
        string alias)
    {
        /*
         * Navigation filters are converted later into EXISTS clauses.
         *
         * Example:
         *
         * customer {
         *   product {
         *      amount { eq: 10 }
         *   }
         * }
         *
         * becomes:
         *
         * EXISTS (
         *   SELECT 1
         *   FROM Product p
         *   WHERE p.CustomerKey = c.CustomerKey
         *   AND p.Amount = @p1
         * )
         *
         * The join metadata already exists in QueryPlan.
         * For now this keeps the emitter contract intact.
         */

        throw new NotSupportedException(
            "Navigation filter emission requires QueryPlan join metadata.");
    }



    private string EmitCollection(
        EntityFilterMetadata.Collection filter,
        string alias)
    {
        return filter.Operator switch
        {
            FilterOperator.Any =>
                Emit(
                    filter.Inner,
                    alias),

            _ =>
                throw new NotSupportedException(
                    $"Collection operator {filter.Operator}")
        };
    }



    private string EmitIn(
        string alias,
        string column,
        object? value)
    {
        var values =
            FilterValue.NormalizeList(value);


        var parameters =
            new List<string>();


        foreach (var item in values)
        {
            parameters.Add(
                "@" +
                _parameters.Add(item));
        }


        return
            $"{alias}.\"{column}\" IN ({string.Join(",", parameters)})";
    }



    private string EmitAnd(
        EntityFilterMetadata.And filter,
        string alias)
    {
        var parts =
            new List<string>();


        foreach (var item in filter.Items)
        {
            parts.Add(
                Emit(
                    item,
                    alias));
        }


        return "(" +
            string.Join(
                " AND ",
                parts)
            +
            ")";
    }



    private string EmitOr(
        EntityFilterMetadata.Or filter,
        string alias)
    {
        var parts =
            new List<string>();


        foreach (var item in filter.Items)
        {
            parts.Add(
                Emit(
                    item,
                    alias));
        }


        return "(" +
            string.Join(
                " OR ",
                parts)
            +
            ")";
    }
}