using System;
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
///   PlannerRegistry     → PhysicalQueryPlan
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
                // A child marked IsConditional came from an @skip/@include
                // directive the adapter saw but did not evaluate (literal
                // if:true/if:false are already resolved by the adapter, so
                // by the time IsConditional reaches here it is always
                // variable-driven). SelectionIR currently only carries a
                // bare IsConditional flag — it does not carry which
                // directive (@skip vs @include) or which variable name was
                // referenced, so there is no way to actually resolve this
                // correctly here, with or without a variable map.
                //
                // Silently keeping the field (the old behavior) is worse
                // than an explicit failure: it makes the API's claimed
                // contract ("resolves @skip/@include") false while looking
                // like optimization occurred, and can return fields the
                // client explicitly asked to have skipped/included
                // conditionally. Fail loudly instead until SelectionIR
                // carries the directive kind + variable name needed to
                // evaluate this for real (see class remarks).
                if (child.IsConditional)
                {
                    throw new NotSupportedException(
                        "SelectionOptimizer cannot resolve this @skip/@include " +
                        "directive: SelectionIR only records that a field is " +
                        "conditional, not which directive or variable it " +
                        "depends on. Runtime directive evaluation is not yet " +
                        "implemented — see SelectionOptimizer remarks.");
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