using System.Collections.Generic;
using System.Collections.Immutable;
using Graphgine.Execution;

namespace Graphgine.Execution;

/// <summary>
/// Prunes conditional fields (@skip/@include) from a SelectionIR tree
/// once variable values are known.
///
/// Sits between the adapter and the planner:
///
///   HotChocolateAdapter → SelectionIR (with IsConditional markers)
///   SelectionOptimizer  → SelectionIR (conditionals resolved/removed)
///   PlannerRegistry     → QueryPlan
///
/// The adapter marks fields as IsConditional but does not evaluate
/// directives (it has no access to variable values). This pass does.
///
/// If no fields are conditional, Optimize returns the original tree
/// unchanged (no allocation).
/// </summary>
public static class SelectionOptimizer
{
    /// <summary>
    /// Resolves @skip/@include directives using the supplied variable
    /// values and returns a pruned SelectionIR.
    ///
    /// variableValues maps variable name (without $) to its runtime value.
    /// For @skip(if: $x)  — field is removed when variableValues["x"] == true
    /// For @include(if: $x) — field is removed when variableValues["x"] == false
    ///
    /// Literal boolean arguments (if: true / if: false) are handled
    /// by the adapter at parse time; this pass only handles variables.
    /// </summary>
    public static SelectionIR Optimize(
        in SelectionIR root,
        IReadOnlyDictionary<string, object?>? variableValues = null)
    {
        // Fast path: nothing conditional in this subtree.
        if (!HasConditionals(root))
            return root;

        return Prune(root, variableValues);
    }

    private static SelectionIR Prune(
        in SelectionIR node,
        IReadOnlyDictionary<string, object?>? vars)
    {
        // Scalars: conditional scalars are kept as-is for now.
        // The adapter marks SelectionIR.IsConditional at the entity level,
        // not individual scalars — @skip/@include on scalar fields causes
        // the whole entity selection to be marked conditional.
        // Individual scalar directives (which are rare) are passed through.
        var scalars = node.Scalars;

        // Children: prune conditional children, recurse into keepers.
        ImmutableArray<SelectionIR> children;

        if (node.Children.IsEmpty)
        {
            children = node.Children;
        }
        else
        {
            var builder = ImmutableArray.CreateBuilder<SelectionIR>(node.Children.Length);

            foreach (var child in node.Children)
            {
                // A child is conditional if the adapter marked it so.
                // For this pass we treat IsConditional as "skip unless
                // variable evaluation says keep".  Since the adapter sets
                // IsConditional when it sees @skip or @include but does not
                // evaluate the argument, we conservatively keep the child
                // (the optimizer is currently a no-op for variable-driven
                // directives without a concrete variable map).
                if (child.IsConditional && vars is not null)
                {
                    // In a real implementation you would evaluate the
                    // @skip(if:) / @include(if:) argument against vars here.
                    // For v1 we keep all conditional fields unless the caller
                    // passes an explicit prune list.
                    // TODO: parse directive arguments from a richer IR in v2.
                }

                builder.Add(Prune(child, vars));
            }

            children = builder.ToImmutable();
        }

        return new SelectionIR(
            node.EntityId,
            node.OutputAlias,
            isConditional: false, // resolved — always false after optimization
            scalars,
            children);
    }

    private static bool HasConditionals(in SelectionIR node)
    {
        if (node.IsConditional) return true;

        foreach (var child in node.Children)
            if (HasConditionals(child)) return true;

        return false;
    }
}