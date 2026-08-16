# M17.7 — Cross-Provider Security Conformance

## Purpose

M17.7 makes security-provider differences explicit. A semantic capability carries required security invariants, while each execution provider exposes the subset it can preserve. Foundgine must never treat all providers as security-equivalent.

The conformance path is:

```text
Semantic capability
      ↓
Required security invariants
      ↓
Provider selection
      ↓
Provider conformance profile
      ↓
SecurityInvariantProof
      ↓
Executable plan only when satisfied
```

## Provider matrix

| Provider | Query/security invariants | High-assurance mutation invariants |
|---|---|---|
| InMemory | Authorization, runtime authorization, tenant isolation, field/relationship visibility, parameterization, cache-context isolation | Not claimed |
| Generic SQL | Authorization, runtime authorization, field/relationship visibility, parameterization, cache-context isolation | Not claimed |
| PostgreSQL TransferFunds | Required query invariants plus tenant isolation, atomicity, idempotency, replay protection, audit, execution evidence | Claimed and backed by the M16.5/M16.6 PostgreSQL execution tests |

The matrix is intentionally conservative. A provider that does not implement a consequential guarantee cannot execute a capability requiring it merely because it can execute the underlying data operation.

## Fail-closed behavior

Unknown providers fail closed.

Unknown invariants fail closed.

A provider declaring an unknown invariant is rejected during registration.

A provider missing a required invariant produces a `SecurityInvariantProof` with explicit missing requirements and cannot satisfy the execution gate.

## Architectural significance

M17.7 establishes provider capability intersection as part of plan viability:

```text
RequiredInvariants ∩ ProviderPreservedInvariants
```

A plan is executable only when:

```text
RequiredInvariants ⊆ ProviderPreservedInvariants
```

This keeps provider-specific security behavior out of semantic authorization while still making provider limitations visible to planning.

## What this proves

- Security invariants have a provider-neutral vocabulary.
- Providers expose explicit preservation capabilities.
- Provider capability differences are machine-readable.
- Generic SQL cannot claim high-assurance mutation guarantees merely because it can generate SQL.
- The PostgreSQL `TransferFunds` provider can claim the stronger mutation contract already established by M16.5/M16.6.
- Unknown providers and unknown invariants fail closed.

## What this does not prove

The matrix is a conformance contract, not a formal verification system. A provider profile must still be backed by implementation-specific structural checks and adversarial integration tests.

M17.7 therefore does not claim that every provider implementation is bug-free or that declaring an invariant makes it true.

## Security progression

```text
M17.3  Security vocabulary
   ↓
M17.4  Plan-level invariant proof
   ↓
M17.5  SQL provider conformance
   ↓
M17.6  High-assurance mutation conformance
   ↓
M17.7  Cross-provider conformance
```

The next major architectural concern is preserving these proofs through optimization and plan rewriting.
