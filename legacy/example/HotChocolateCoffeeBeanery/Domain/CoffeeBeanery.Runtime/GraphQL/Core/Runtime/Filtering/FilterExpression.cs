using System.Collections.Generic;

namespace CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

public abstract record FilterExpression;


public sealed record BinaryFilterExpression(
    string FieldName,
    FilterOperator Operator,
    object? Value)
    : FilterExpression;


public sealed record NavigationFilterExpression(
    string NavigationName,
    FilterExpression Inner)
    : FilterExpression;


public sealed record CollectionFilterExpression(
    FilterOperator Operator,
    FilterExpression Inner)
    : FilterExpression;


public sealed record AndFilterExpression(
    IReadOnlyList<FilterExpression> Expressions)
    : FilterExpression;


public sealed record OrFilterExpression(
    IReadOnlyList<FilterExpression> Expressions)
    : FilterExpression;
    
public enum FilterOperator
{
    Eq,
    Neq,
    In,
    Any,
    Some,
    All,
    None
}