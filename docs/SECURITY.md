# Security

Foundgine treats external intent as untrusted input.

The important rule is:

```text
Input → Parse → Resolve → Authorize → Plan → Execute
```

Do not allow an adapter to bypass resolution or authorization.

## Granular authorization

Authorization is evaluated at the semantic boundary for:

- entity read/write access;
- field read/write access;
- relationship read/write access;
- conditional resource predicates.

Capability discovery is not an authorization cache. It helps a caller
construct valid intent, but the policy is evaluated again before planning and
execution.

Conditional predicates are retained in the provider-independent execution plan.
Providers must lower them without discarding their runtime context. This is
important for tenant- and user-scoped data and is a prerequisite for safe plan
caching.

A particularly important fail-closed invariant is that an empty field set after
authorization must never be interpreted as "all fields". The SQL compiler
rejects an execution node with no fields rather than widening the selection.

SQL values are parameterized by the SQL provider. External GraphQL or JSON
names do not become SQL identifiers or executable provider operations without
going through the semantic and planning layers.

For AI-generated intent, capability discovery can provide the agent with the
subset of the domain it can use. Application authentication, identity,
claims/roles, rate limits, validation, approval controls, and policy management
remain application-level concerns around the Foundgine boundary.

## Warrant trust boundary: deployment responsibilities

Signed security warrants (`Foundgine.Semantics.Security.Warrants`) let a
caller present pre-authorized, time-boxed capability grants (for example, an
AI agent acting on a human's behalf). Three parts of this feature are
opt-in configuration rather than fail-closed defaults, and each has a
corresponding penetration test in
`tests/Foundgine.Security.Tests/Penetration/WarrantTrustBoundaryPenetrationTests.cs`
that demonstrates the gap when the host does not close it:

- **Issuer trust is opt-in.** `SecurityWarrantVerifier.Verify` only checks
  `Issuer` against a caller-supplied `expectedIssuer`. If
  `FoundgineOptions.ExpectedWarrantIssuer` is left `null` (its documented
  default), a warrant signed by any key your `ISecurityWarrantKeyResolver`
  will resolve is accepted regardless of its `Issuer` field, because
  `Issuer` is attacker-supplied content, not a trust anchor by itself.
  **Always set `ExpectedWarrantIssuer` (or an equivalent check in a custom
  resolver) before accepting warrant-backed requests in production.**
- **Delegation-chain trust is a separate, unwired feature.**
  `SecurityWarrantDelegationChainValidator` and
  `SecurityWarrantDelegationTrust` implement full multi-hop delegation
  validation (depth limits, attenuation, path-splice/cycle detection, issuer
  delegation authority) and are exercised by
  `tests/Foundgine.Semantics.Tests/Security/Warrants/`. `FoundgineEngine`
  and `FoundgineMutationEngine` do not call them: they verify only the
  single warrant presented with a request. If your deployment needs
  multi-hop delegated warrants, you must call the chain validator yourself
  before handing the leaf warrant to `SecurityExecutionContext`; do not
  assume delegation ancestry fields on a warrant are checked just because
  they are present.
- **Replay protection is process-local by default.**
  `MemorySecurityWarrantReplayStore` is a single-process dictionary. It does
  not share consumed-nonce state across replicas and does not survive a
  restart. A horizontally scaled deployment using the default store allows
  one replay of a given warrant per uncoordinated instance, for the
  lifetime of the warrant's validity window. **Supply a shared/distributed
  `ISecurityWarrantReplayStore` (for example, backed by your database or a
  cache with atomic compare-and-set) in any multi-instance deployment.**

## Authorization recovery control plane

The `Foundgine.Authorization` namespace (implemented in
`src/Foundgine.Authorization/Recovery/`, a provider-agnostic library) covers
failure and recovery handling for the authorization control plane itself:
publication key lifecycle and rotation, promotion/commit atomicity,
cross-instance commit and journal consensus, repair-proposer credential
authentication and replication, and transaction-journal integrity. See
`docs/security/CHANGELOG.md` for the invariant-by-invariant history of this
module, and `tests/Foundgine.Authorization.Tests/` for the
corresponding adversarial test coverage. The PostgreSQL-specific wiring
(`PostgresAuthorizationContextStore`, `PostgresAuthorizationRecoveryCoordinator`,
`PostgresAuthorizationSecurityUnitOfWork`, and the transfer-funds executor)
remains in `samples/Foundgine.HighAssurance.Postgres/`.
