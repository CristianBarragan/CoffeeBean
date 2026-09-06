using Foundgine.Core.Abstractions;

namespace Foundgine.Core.Semantic.Planning.Tests;

public sealed class RewriteRuleCompositionTests
{
    private static SemanticPlan Plan()
    {
        return new SemanticPlan(new SemanticPlanNode(
            1, ExecutionOperation.Scan, new EntityId(1), [new FieldId(1)], null, null, [],
            Authorization: AuthorizationPredicate.Not(AuthorizationPredicate.Not(
                AuthorizationPredicate.Equal(
                    AuthorizationPredicate.Member(AuthorizationPredicate.ResourceParameter("resource"), "TenantId"),
                    AuthorizationPredicate.Member(AuthorizationPredicate.ContextParameter("user"), "TenantId"))))));
    }

    [Fact]
    public void Composer_respects_dependency_order()
    {
        var first = new NamedRule("test.first", priority: 10);
        var second = new NamedRule("test.second", after: ["test.first"], priority: -10);
        var result = new RewriteRuleComposer([second, first]).Compose(Plan());

        Assert.Equal(["test.first", "test.second"], result.AppliedRules);
        Assert.True(result.TerminatedNormally);
    }

    [Fact]
    public void Composer_rejects_ordering_cycles()
    {
        var a = new NamedRule("test.a", after: ["test.b"]);
        var b = new NamedRule("test.b", after: ["test.a"]);

        Assert.Throws<InvalidOperationException>(() => new RewriteRuleComposer([a, b]));
    }

    [Fact]
    public void Composer_rejects_unknown_dependencies()
    {
        var a = new NamedRule("test.a", after: ["missing"]);

        Assert.Throws<InvalidOperationException>(() => new RewriteRuleComposer([a]));
    }

    [Fact]
    public void Composer_rejects_mutual_conflicts()
    {
        var a = new NamedRule("test.a", conflicts: ["test.b"]);
        var b = new NamedRule("test.b", conflicts: ["test.a"]);

        Assert.Throws<InvalidOperationException>(() => new RewriteRuleComposer([a, b]));
    }

    [Fact]
    public void Idempotent_rule_is_applied_at_most_once()
    {
        var rule = new NamedRule("test.idempotent");
        var result = new RewriteRuleComposer([rule]).Compose(Plan());

        Assert.Single(result.Applications);
        Assert.Equal(5d, result.TotalCostImpact);
    }

    [Fact]
    public void Composition_detects_non_idempotent_cycles_by_plan_fingerprint()
    {
        var rule = new OscillatingRule();

        Assert.Throws<InvalidOperationException>(() => new RewriteRuleComposer(
            [rule], new RewriteRuleCompositionOptions(MaxRuleApplications: 8, MaxPlanVisits: 8)).Compose(Plan()));
    }

    [Fact]
    public void Composition_accumulates_cost_and_preserves_proofs()
    {
        var first = new NamedRule("test.first", cost: 1.5);
        var second = new NamedRule("test.second", after: ["test.first"], cost: 2.5);
        var result = new RewriteRuleComposer([first, second]).Compose(Plan());

        Assert.Equal(4d, result.TotalCostImpact);
        Assert.All(result.Applications, application =>
        {
            Assert.True(application.SecurityProof.IsSatisfied);
            Assert.True(application.SemanticProof.IsSatisfied);
        });
    }

    private sealed class NamedRule : IPlanRewriteRule
    {
        public NamedRule(string name, IReadOnlyList<string>? after = null, IReadOnlyList<string>? conflicts = null,
            double cost = 5d, int priority = 0)
        {
            Name = name;
            MustRunAfter = after ?? [];
            ConflictsWith = conflicts ?? [];
            CostImpact = cost;
            Priority = priority;
        }

        public string Name { get; }

        public IReadOnlyList<string> Preconditions => ["test plan"];
        public IReadOnlyList<string> SecurityObligations => ["authorization.required"];
        public double CostImpact { get; }

        public IReadOnlyList<string> MustRunAfter { get; }

        public IReadOnlyList<string> ConflictsWith { get; }

        public int Priority { get; }

        public bool CanApply(SemanticPlan plan)
        {
            return true;
        }

        public SemanticPlan Apply(SemanticPlan plan)
        {
            return plan with
            {
                Root = plan.Root with
                {
                    Authorization =
                    AuthorizationPredicate.Not(AuthorizationPredicate.Not(plan.Root.Authorization!))
                }
            };
        }
    }

    private sealed class OscillatingRule : IPlanRewriteRule
    {
        private bool _toggle;
        public string Name => "test.oscillating";
        public IReadOnlyList<string> Preconditions => ["test plan"];
        public IReadOnlyList<string> SecurityObligations => ["authorization.required"];
        public double CostImpact => 1d;
        public bool IsIdempotent => false;

        public bool CanApply(SemanticPlan plan)
        {
            return true;
        }

        public SemanticPlan Apply(SemanticPlan plan)
        {
            _toggle = !_toggle;
            return plan with { Root = plan.Root with { Id = _toggle ? 2 : 1 } };
        }
    }
}

public sealed class RewriteRuleSelectionTests
{
    private static SemanticPlan SelectionPlan()
    {
        return new SemanticPlan(new SemanticPlanNode(
            1, ExecutionOperation.Scan, new EntityId(1), [new FieldId(1)], null, null, [],
            Authorization: AuthorizationPredicate.Equal(
                AuthorizationPredicate.Member(AuthorizationPredicate.ResourceParameter("resource"), "TenantId"),
                AuthorizationPredicate.Member(AuthorizationPredicate.ContextParameter("user"), "TenantId"))));
    }

    [Xunit.Fact]
    public void Selector_prefers_higher_benefit_when_cost_is_equal()
    {
        var low = new SelectionRule("low", 1d, 1d, 10);
        var high = new SelectionRule("high", 3d, 1d, -10);
        var selected = new RewriteRuleSelector().Select(SelectionPlan(), [low, high]);

        Xunit.Assert.Equal("high", selected!.RuleName);
        Xunit.Assert.Equal(1.5d, selected.Score);
    }

    [Xunit.Fact]
    public void Selector_penalizes_expensive_rewrites()
    {
        var cheap = new SelectionRule("cheap", 2d, 0d);
        var expensive = new SelectionRule("expensive", 10d, 10d);
        var selected = new RewriteRuleSelector().Select(SelectionPlan(), [cheap, expensive]);

        Xunit.Assert.Equal("cheap", selected!.RuleName);
    }

    [Xunit.Fact]
    public void Composer_records_selection_history()
    {
        var low = new SelectionRule("test.low", 1d, 1d, 0);
        var high = new SelectionRule("test.high", 4d, 1d, 0);
        var result = new RewriteRuleComposer([low, high]).Compose(SelectionPlan());

        Xunit.Assert.NotEmpty(result.Candidates);
        Xunit.Assert.Equal("test.high", result.Candidates[0].RuleName);
    }

    private sealed class SelectionRule : IPlanRewriteRule
    {
        public SelectionRule(string name, double benefit, double cost, int priority = 0)
        {
            Name = name;
            BenefitEstimate = benefit;
            CostImpact = cost;
            Priority = priority;
        }

        public string Name { get; }

        public IReadOnlyList<string> Preconditions => ["selection test plan"];
        public IReadOnlyList<string> SecurityObligations => ["authorization.required"];
        public double CostImpact { get; }

        public double BenefitEstimate { get; }

        public int Priority { get; }

        public bool CanApply(SemanticPlan plan)
        {
            return true;
        }

        public SemanticPlan Apply(SemanticPlan plan)
        {
            return plan;
        }
    }
}