# M5.83 — Security Execution Certificate & Provider Conformance Boundary Hardening

## Purpose

Harden the security execution boundary identified during review of the security-add-ons branch.

The previous model treated `SecurityInvariantProof` as public attestation data that could be constructed independently of the provider plan being executed. M5.83 changes that into an execution certificate with explicit provenance and exact-plan binding.

## Implemented

- `SecurityInvariantProof` can no longer be constructed by normal application callers.
- Public conformance/profile results use `SecurityInvariantAttestation` rather than execution proof terminology.
- Execution certificates are issued only by `SecurityInvariantProofGate`.
- Certificates are bound to the exact returned `ProviderPlan` instance.
- Certificates contain a deterministic SHA-256 fingerprint of the exact `ExecutionIR` used during certification.
- Execution requires both the provider plan and the corresponding `ExecutionIR`.
- Provider identity is checked at the final execution boundary.
- Record cloning/transplanting a certified provider plan invalidates the certificate binding.
- Replaying a certificate against a different `ExecutionIR` fails closed.
- Provider capability declarations are no longer sufficient for security-critical invariants.
- Security-critical invariants require `IProviderSecurityConformanceEvaluator`.
- Concrete evaluator results, rather than declared profiles, are used as certificate evidence when available.
- Conformance failures now report both explicit violations and required invariants missing from the evaluator result.
- Added adversarial tests for:
  - provider-profile-only certification
  - exact plan-instance binding
  - cross-IR certificate replay
  - provider identity mismatch
  - concrete evaluator violations

## Security boundary

The execution path is now:

`ExecutionIR → ProviderCompiler → Concrete ProviderPlan → Concrete Conformance Evaluation → Execution Certificate → Execution Gate → Provider`

The execution gate verifies:

1. a certificate exists;
2. the certificate provider matches the provider plan;
3. the certificate is bound to the exact provider plan instance;
4. the certificate's `ExecutionIR` fingerprint matches the IR being executed;
5. all required invariants are satisfied.

A satisfied claim detached from its original plan is therefore not executable evidence.

## Terminology

`SecurityInvariantAttestation` remains appropriate for provider capability/profile evidence.

`SecurityInvariantProof` is reserved for the internal execution certificate because it carries provenance and exact-plan/IR binding that ordinary public attestation data does not.

## Validation

The environment used to produce this artifact does not have the `.NET` SDK installed, so the .NET test suite could not be executed here.
