# M17.6 — High-Assurance Mutation Conformance

## Purpose

M17.6 closes the mutation side of the security-conformance chain established by M17.3–M17.5.

The PostgreSQL `TransferFunds` provider is no longer treated as conformant merely because it declares support for high-assurance invariants. The provider exposes an executable mutation conformance contract and the integration suite exercises the consequential guarantees against PostgreSQL.

## Conformance chain

```text
Semantic capability
    ↓
Required security invariants
    ↓
Plan-level security contract
    ↓
PostgreSQL mutation conformance
    ↓
Real transaction
    ↓
Real row locks
    ↓
Real idempotency serialization
    ↓
Real debit + credit
    ↓
Real audit persistence
    ↓
Execution receipt + security proof
```

## Provider contract

`PostgresMutationSecurityConformance` requires the high-assurance mutation boundary to provide:

- tenant isolation
- execution-time authorization revalidation
- one transaction for consequential state
- deterministic account row locking
- idempotency-key serialization
- transactional idempotency persistence
- transactional audit persistence
- execution receipt generation

The contract deliberately distinguishes these mutation guarantees from ordinary SQL query guarantees.

## Runtime gate

`PostgresTransferFundsService.ExecuteAsync` validates the known invariant registry and the provider-specific mutation contract before opening the PostgreSQL transaction.

The successful receipt now carries a `SecurityInvariantProof` for the high-assurance PostgreSQL provider. This proof identifies the invariants preserved by the provider execution boundary.

## PostgreSQL evidence

The existing integration suite provides the execution evidence behind the contract:

1. successful atomic transfer changes both balances and writes exactly one idempotency and audit row;
2. replay returns the original transfer without a second debit or audit event;
3. invariant failure rolls back debit, credit, idempotency, and audit state;
4. concurrent duplicate requests serialize on the idempotency key;
5. opposing transfers acquire account locks in deterministic order;
6. tenant mismatch is rejected without mutation;
7. frozen accounts are rejected without partial state;
8. authorization is re-evaluated after current rows are locked;
9. available-funds semantics use balance minus pending transactions and regulatory holds.

## What this proves

M17.6 provides a much stronger claim than metadata-only attestation:

> The PostgreSQL high-assurance mutation boundary has an explicit security contract, and the consequential guarantees are exercised through the real transactional provider integration path.

## What this does not prove

It does not prove that:

- PostgreSQL itself is configured securely;
- credentials, network transport, or deployment infrastructure are secure;
- every possible database failure mode has been tested;
- the authorization policy is business-correct;
- the provider implementation is free of all defects;
- a security proof is equivalent to formal verification.

The security proof is therefore an **execution-contract attestation backed by provider conformance tests**, not a claim of mathematical correctness.

## Architectural significance

The security architecture now has the following progression:

```text
M17.3  Security vocabulary
   ↓
M17.4  Plan-level invariant proof
   ↓
M17.5  SQL provider conformance
   ↓
M17.6  High-assurance mutation conformance
```

This creates the foundation for the next stage: requiring optimization and rewriting passes to preserve security invariants rather than treating security as a pre-planning concern.
