using Foundgine.Runtime.ControlPlane;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Runtime.ControlPlane.Tests;

/// <summary>adversarial tests for capability scope and confused-deputy resistance.</summary>
public sealed class AuthorizationCapabilityScopeSecurityTests
{
 private static AuthorizationCapabilityScope TransferScope() =>
 AuthorizationCapabilityScope.Create(
 "BankAccount.transferFunds", 1, "transferFunds",
 ["account.debit", "account.credit", "transfer.audit"]);

 [Fact]
 public void Exact_capability_operation_and_effect_are_accepted()
 {
 TransferScope().Require("BankAccount.transferFunds", 1, "transferFunds", "account.debit");
 }

 [Fact]
 public void Different_capability_cannot_reuse_authorization()
 {
 Assert.Throws<InvalidOperationException>(() =>
 TransferScope().Require("BankAccount.closeAccount", 1, "closeAccount", "account.close"));
 }

 [Fact]
 public void Different_operation_cannot_reuse_authorization()
 {
 Assert.Throws<InvalidOperationException>(() =>
 TransferScope().Require("BankAccount.transferFunds", 1, "refundFunds", "account.debit"));
 }

 [Fact]
 public void Different_capability_version_cannot_reuse_authorization()
 {
 Assert.Throws<InvalidOperationException>(() =>
 TransferScope().Require("BankAccount.transferFunds", 2, "transferFunds", "account.debit"));
 }

 [Fact]
 public void Unauthorized_effect_is_rejected_even_when_operation_matches()
 {
 Assert.Throws<InvalidOperationException>(() =>
 TransferScope().Require("BankAccount.transferFunds", 1, "transferFunds", "account.close"));
 }

 [Fact]
 public void Scope_fingerprint_tampering_fails_closed()
 {
 var scope = TransferScope() with { ScopeFingerprint = "forged" };
 Assert.Throws<InvalidOperationException>(() =>
 scope.Require("BankAccount.transferFunds", 1, "transferFunds", "account.debit"));
 }

 [Fact]
 public void Effect_order_does_not_change_the_canonical_scope()
 {
 var first = AuthorizationCapabilityScope.Create(
 "BankAccount.transferFunds", 1, "transferFunds",
 ["account.credit", "account.debit", "transfer.audit"]);
 var second = AuthorizationCapabilityScope.Create(
 "BankAccount.transferFunds", 1, "transferFunds",
 ["transfer.audit", "account.debit", "account.credit"]);

 Assert.Equal(first.ScopeFingerprint, second.ScopeFingerprint);
 }

 [Fact]
 public void Empty_effect_set_is_rejected()
 {
 Assert.Throws<InvalidOperationException>(() =>
 AuthorizationCapabilityScope.Create("BankAccount.transferFunds", 1, "transferFunds", []));
 }
}
