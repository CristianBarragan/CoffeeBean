namespace Foundgine.Core.Abstractions;

/// <summary>
///     Small provider-independent representation of an AOT authorization predicate.
///     It contains no expression trees and no executable delegates.
/// </summary>
public sealed record AuthorizationPredicate(
    AuthorizationPredicateKind Kind,
    string? Name = null,
    string? Value = null,
    AuthorizationPredicate? Left = null,
    AuthorizationPredicate? Right = null)
{
    public static AuthorizationPredicate Parameter(string name)
    {
        return new AuthorizationPredicate(AuthorizationPredicateKind.Parameter, name);
    }

    public static AuthorizationPredicate ContextParameter(string name)
    {
        return new AuthorizationPredicate(AuthorizationPredicateKind.ContextParameter, name);
    }

    public static AuthorizationPredicate ResourceParameter(string name)
    {
        return new AuthorizationPredicate(AuthorizationPredicateKind.ResourceParameter, name);
    }

    public static AuthorizationPredicate Member(AuthorizationPredicate target, string name)
    {
        return new AuthorizationPredicate(AuthorizationPredicateKind.MemberAccess, name, Left: target);
    }

    public static AuthorizationPredicate Constant(string value)
    {
        return new AuthorizationPredicate(AuthorizationPredicateKind.Constant, Value: value);
    }

    public static AuthorizationPredicate Equal(AuthorizationPredicate left, AuthorizationPredicate right)
    {
        return new AuthorizationPredicate(AuthorizationPredicateKind.Equal, Left: left, Right: right);
    }

    public static AuthorizationPredicate NotEqual(AuthorizationPredicate left, AuthorizationPredicate right)
    {
        return new AuthorizationPredicate(AuthorizationPredicateKind.NotEqual, Left: left, Right: right);
    }

    public static AuthorizationPredicate And(AuthorizationPredicate left, AuthorizationPredicate right)
    {
        return new AuthorizationPredicate(AuthorizationPredicateKind.And, Left: left, Right: right);
    }

    public static AuthorizationPredicate Or(AuthorizationPredicate left, AuthorizationPredicate right)
    {
        return new AuthorizationPredicate(AuthorizationPredicateKind.Or, Left: left, Right: right);
    }

    public static AuthorizationPredicate Not(AuthorizationPredicate operand)
    {
        return new AuthorizationPredicate(AuthorizationPredicateKind.Not, Left: operand);
    }
}

public enum AuthorizationPredicateKind : byte
{
    Parameter,
    ContextParameter,
    ResourceParameter,
    MemberAccess,
    Constant,
    Equal,
    NotEqual,
    And,
    Or,
    Not
}