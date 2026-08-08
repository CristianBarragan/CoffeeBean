using System.Linq.Expressions;

namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    // -------------------------------------------------------------------------
    // ExpressionHelper
    //
    // Extracts the member (property) name from a simple lambda expression like
    // `m => m.SomeProperty`. Used by NodeMap.AddModelToEntity so that fk/pk/alias
    // can be authored as compile-time-safe, typed lambdas, while EntityKey
    // itself only ever stores the resulting plain string member name.
    //
    // Supports the common cases:
    //   m => m.Prop                                (direct member access)
    //   m => (object)m.Prop                          (boxing convert, e.g. when T is object)
    //   m => (SomeType)m.Prop                        (any Convert/ConvertChecked wrapper)
    // -------------------------------------------------------------------------
    public static class ExpressionHelper
    {
        public static string GetMemberName<TSource, TProperty>(
            Expression<Func<TSource, TProperty>> expression)
        {
            var body = expression.Body;

            // Unwrap any boxing/conversion node (common when TProperty is object
            // or a base type), e.g. (object)m.SomeIntProperty.
            while (body is UnaryExpression unary &&
                   (unary.NodeType == ExpressionType.Convert ||
                    unary.NodeType == ExpressionType.ConvertChecked))
            {
                body = unary.Operand;
            }

            if (body is MemberExpression member)
                return member.Member.Name;

            throw new InvalidOperationException(
                $"[NodeMap] Expression '{expression}' is not a simple property access " +
                $"(e.g. 'm => m.PropertyName'). Cannot extract a member name from it.");
        }
    }
}