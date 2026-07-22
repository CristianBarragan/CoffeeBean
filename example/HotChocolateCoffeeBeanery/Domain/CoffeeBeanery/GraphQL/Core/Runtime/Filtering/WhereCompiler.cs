using System;
using System.Collections.Generic;
using System.Linq;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

public static class WhereCompiler
{
    public static FilterExpression? Compile(
        ObjectValueNode where)
    {
        if (where.Fields.Count == 0)
            return null;


        var expressions =
            new List<FilterExpression>();


        foreach (var field in where.Fields)
        {
            var expression =
                CompileField(
                    field.Name.Value,
                    field.Value);


            if (expression != null)
                expressions.Add(expression);
        }


        if (expressions.Count == 0)
            return null;


        if (expressions.Count == 1)
            return expressions[0];


        return new AndFilterExpression(expressions);
    }



    private static FilterExpression? CompileField(
        string fieldName,
        IValueNode value)
    {
        /*
         * Logical operators
         */

        if (fieldName.Equals(
                "and",
                StringComparison.OrdinalIgnoreCase))
        {
            if (value is not ListValueNode list)
                return null;


            var children =
                list.Items
                    .OfType<ObjectValueNode>()
                    .Select(Compile)
                    .Where(x => x != null)
                    .Cast<FilterExpression>()
                    .ToList();


            return children.Count == 0
                ? null
                : new AndFilterExpression(children);
        }


        if (fieldName.Equals(
                "or",
                StringComparison.OrdinalIgnoreCase))
        {
            if (value is not ListValueNode list)
                return null;


            var children =
                list.Items
                    .OfType<ObjectValueNode>()
                    .Select(Compile)
                    .Where(x => x != null)
                    .Cast<FilterExpression>()
                    .ToList();


            return children.Count == 0
                ? null
                : new OrFilterExpression(children);
        }



        /*
         * Collection navigation operators
         *
         * some/all/none
         */

        if (fieldName is "some" or "all" or "none")
        {
            if (value is not ObjectValueNode collectionObject)
                return null;


            var inner =
                Compile(collectionObject);


            if (inner == null)
                return null;


            return new CollectionFilterExpression(
                fieldName switch
                {
                    "some" => FilterOperator.Some,
                    "all" => FilterOperator.All,
                    "none" => FilterOperator.None,
                    _ => throw new InvalidOperationException()
                },
                inner);
        }



        /*
         * Operator node:
         *
         * firstName:{
         *    eq:"Bob"
         * }
         */

        if (value is ObjectValueNode opObject)
        {
            var operators =
                new List<FilterExpression>();


            foreach (var op in opObject.Fields)
            {
                if (!TryParseOperator(
                        op.Name.Value,
                        out var filterOperator))
                {
                    /*
                     * Navigation object:
                     *
                     * customer:{
                     *    firstName:{
                     *       eq:"Bob"
                     *    }
                     * }
                     */

                    var nested =
                        CompileField(
                            op.Name.Value,
                            op.Value);


                    if (nested != null)
                    {
                        operators.Add(
                            new NavigationFilterExpression(
                                fieldName,
                                nested));
                    }

                    continue;
                }


                operators.Add(
                    new BinaryFilterExpression(
                        fieldName,
                        filterOperator,
                        ExtractValue(op.Value)));
            }


            if (operators.Count == 1)
                return operators[0];


            if (operators.Count > 1)
                return new AndFilterExpression(operators);


            return null;
        }



        /*
         * Scalar shorthand:
         *
         * id:"123"
         */

        return new BinaryFilterExpression(
            fieldName,
            FilterOperator.Eq,
            ExtractValue(value));
    }



    private static bool TryParseOperator(
        string name,
        out FilterOperator op)
    {
        switch (name)
        {
            case "eq":
                op = FilterOperator.Eq;
                return true;

            case "neq":
                op = FilterOperator.Neq;
                return true;

            case "in":
                op = FilterOperator.In;
                return true;

            default:
                op = default;
                return false;
        }
    }



    private static object? ExtractValue(
        IValueNode node)
    {
        return node switch
        {
            StringValueNode s =>
                s.Value,

            IntValueNode i =>
                i.Value,

            FloatValueNode f =>
                f.Value,

            BooleanValueNode b =>
                b.Value,

            NullValueNode =>
                null,

            EnumValueNode e =>
                e.Value,

            ListValueNode list =>
                list.Items
                    .Select(ExtractValue)
                    .ToArray(),

            _ =>
                node.ToString()
        };
    }
}