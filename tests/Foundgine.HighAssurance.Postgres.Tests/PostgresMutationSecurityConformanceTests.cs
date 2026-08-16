using Foundgine.HighAssurance.Postgres;
using Xunit;

namespace Foundgine.HighAssurance.Postgres.Tests;

public sealed class PostgresMutationSecurityConformanceTests
{
    [Fact]
    public void TransferFunds_declares_all_high_assurance_invariants()
    {
        var contract = PostgresMutationSecurityConformance.TransferFunds;

        Assert.Contains("tenant.isolation", contract.RequiredInvariants);
        Assert.Contains("authorization.runtime", contract.RequiredInvariants);
        Assert.Contains("mutation.atomic", contract.RequiredInvariants);
        Assert.Contains("mutation.idempotency", contract.RequiredInvariants);
        Assert.Contains("mutation.replay-protection", contract.RequiredInvariants);
        Assert.Contains("evidence.audit", contract.RequiredInvariants);
        Assert.Contains("evidence.execution-receipt", contract.RequiredInvariants);
        Assert.True(contract.IsSatisfied);
        Assert.Empty(contract.MissingRequirements());
    }

    [Fact]
    public void TransferFunds_contract_is_known_to_the_security_registry()
    {
        PostgresMutationSecurityConformanceGate.EnsureKnownInvariants();
    }

    [Fact]
    public void Incomplete_provider_contract_fails_closed()
    {
        var incomplete = PostgresMutationSecurityConformance.TransferFunds with
        {
            PersistsAuditInsideTransaction = false
        };

        var missing = incomplete.MissingRequirements();
        Assert.Contains("evidence.audit", missing);
        Assert.False(incomplete.IsSatisfied);
        Assert.Throws<InvalidOperationException>(incomplete.EnsureSatisfied);
    }
}
