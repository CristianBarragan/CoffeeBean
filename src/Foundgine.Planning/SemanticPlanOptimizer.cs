using System.Text;
using Foundgine.Abstractions;

namespace Foundgine.Planning;

/// <summary>
/// Performs semantics-preserving optimization of authorization predicates
/// already attached to an authorized plan.
///
/// The optimizer deliberately does not evaluate authorization, invent policy,
/// or lower predicates into SQL. Its first responsibility is to canonicalize
/// equivalent policy expressions so equivalent authorized plans can share
/// deterministic fingerprints and compiled provider plans.
/// </summary>
public sealed class SemanticPlanOptimizer : IPlanOptimizer
{
    public SemanticPlanOptimizationResult Optimize(SemanticPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var rules = new List<string>();
        var root = OptimizeNode(plan.Root, rules);
        return new SemanticPlanOptimizationResult(new SemanticPlan(root), rules);
    }

    private static SemanticPlanNode OptimizeNode(
        SemanticPlanNode node,
        ICollection<string> rules)
    {
        var authorization = NormalizePredicate(node.Authorization, rules);
        var children = node.Children
            .Select(child => OptimizeNode(child, rules))
            .ToArray();

        if (ReferenceEquals(authorization, node.Authorization) &&
            children.SequenceEqual(node.Children))
        {
            return node;
        }

        return node with
        {
            Authorization = authorization,
            Children = children
        };
    }

    private static AuthorizationPredicate? NormalizePredicate(
        AuthorizationPredicate? predicate,
        ICollection<string> rules)
    {
        if (predicate is null)
            return null;

        var normalized = Normalize(predicate, rules);
        return normalized;
    }

    private static AuthorizationPredicate Normalize(
        AuthorizationPredicate predicate,
        ICollection<string> rules)
    {
        var left = predicate.Left is null ? null : Normalize(predicate.Left, rules);
        var right = predicate.Right is null ? null : Normalize(predicate.Right, rules);
        var current = ReferenceEquals(left, predicate.Left) &&
                      ReferenceEquals(right, predicate.Right)
            ? predicate
            : predicate with { Left = left, Right = right };

        if (current.Kind == AuthorizationPredicateKind.Not &&
            current.Left?.Kind == AuthorizationPredicateKind.Not &&
            current.Left.Left is not null)
        {
            rules.Add("authorization.double-negation");
            return current.Left.Left;
        }

        if (current.Kind is AuthorizationPredicateKind.And or AuthorizationPredicateKind.Or)
        {
            var operands = new List<AuthorizationPredicate>();
            Flatten(current.Kind, current, operands);

            var before = operands.Count;
            operands = operands
                .GroupBy(StructuralKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(StructuralKey, StringComparer.Ordinal)
                .ToList();

            if (operands.Count != before)
                rules.Add("authorization.duplicate-elimination");

            if (operands.Count > 1)
            {
                var sorted = operands.Select(StructuralKey).ToArray();
                if (!AreInOriginalOrder(current.Kind, current, sorted))
                    rules.Add("authorization.commutative-canonicalization");
            }

            if (operands.Count == 1)
            {
                rules.Add("authorization.single-operand-collapse");
                return operands[0];
            }

            return RebuildBalanced(current.Kind, operands);
        }

        return current;
    }

    private static void Flatten(
        AuthorizationPredicateKind kind,
        AuthorizationPredicate node,
        ICollection<AuthorizationPredicate> destination)
    {
        if (node.Kind == kind)
        {
            if (node.Left is not null)
                Flatten(kind, node.Left, destination);
            if (node.Right is not null)
                Flatten(kind, node.Right, destination);
            return;
        }

        destination.Add(node);
    }

    private static AuthorizationPredicate RebuildBalanced(
        AuthorizationPredicateKind kind,
        IReadOnlyList<AuthorizationPredicate> operands)
    {
        AuthorizationPredicate result = operands[0];
        for (var index = 1; index < operands.Count; index++)
        {
            result = kind == AuthorizationPredicateKind.And
                ? AuthorizationPredicate.And(result, operands[index])
                : AuthorizationPredicate.Or(result, operands[index]);
        }

        return result;
    }

    private static bool AreInOriginalOrder(
        AuthorizationPredicateKind kind,
        AuthorizationPredicate original,
        IReadOnlyList<string> sortedKeys)
    {
        var originalOperands = new List<AuthorizationPredicate>();
        Flatten(kind, original, originalOperands);
        var originalKeys = originalOperands.Select(StructuralKey).ToArray();
        return originalKeys.SequenceEqual(sortedKeys, StringComparer.Ordinal);
    }

    private static string StructuralKey(AuthorizationPredicate predicate)
    {
        var builder = new StringBuilder(128);
        AppendKey(builder, predicate);
        return builder.ToString();
    }

    private static void AppendKey(StringBuilder builder, AuthorizationPredicate predicate)
    {
        builder.Append((byte)predicate.Kind).Append('(');
        AppendValue(builder, predicate.Name);
        builder.Append('|');
        AppendValue(builder, predicate.Value);
        builder.Append('|');
        if (predicate.Left is not null)
            AppendKey(builder, predicate.Left);
        builder.Append('|');
        if (predicate.Right is not null)
            AppendKey(builder, predicate.Right);
        builder.Append(')');
    }

    private static void AppendValue(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        builder.Append(value.Length).Append(':').Append(value);
    }
}
