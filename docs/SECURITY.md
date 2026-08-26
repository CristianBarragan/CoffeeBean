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

## Warrant trust boundary

Signed security warrants (`Foundgine.Semantics.Security.Warrants`) let a caller
present pre-authorized, time-boxed capability grants. The execution boundary is
fail-closed for issuer and delegation trust:

- **Issuer trust is mandatory.** `SecurityWarrantVerifier.Verify` requires an
  explicit trusted issuer. `FoundgineEngine` also rejects configuration that
  omits `FoundgineOptions.ExpectedWarrantIssuer`.
- **Delegated warrants require ancestry.** A delegated warrant must be accompanied
  by its complete root-to-leaf chain in `SecurityExecutionContext.DelegationChain`.
  `SecurityWarrantExecutionTrust` verifies every signature, parent binding,
  delegation depth/path, attenuation rule, audience and the explicit
  `ISecurityWarrantDelegationTrustResolver` policy before execution.
- **Replay protection is deployment-scoped.** `MemorySecurityWarrantReplayStore`
  is intentionally process-local and is suitable for tests or single-process
  scenarios only. `FileSecurityWarrantReplayStore` provides atomic cross-process
  consumption when all instances share a filesystem. Horizontally scaled cloud
  deployments should provide a shared transactional implementation of
  `ISecurityWarrantReplayStore` (for example Redis/SQL with atomic consume-if-absent).

The penetration tests in
`tests/Foundgine.Security.Tests/Penetration/WarrantTrustBoundaryPenetrationTests.cs`
assert these fail-closed properties and verify that durable replay state is shared
across independent store instances.

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

## Security guard-rail coverage

The security test suite also exercises adversarial properties across the
execution boundary, rather than only individual authorization decisions.
Current guard-rail coverage includes:

- fail-closed behavior when a tenant or resource constraint has no runtime context;
- exact capability and operation matching, including Unicode confusables;
- canonicalization stability and digest sensitivity to security-semantic changes;
- cryptographic binding of warrant signatures to constraints and authority metadata;
- concurrent single-use replay protection;
- monotonic warrant attenuation and prevention of authority recovery in descendants;
- authority cache partition isolation across subject, audience, tenant, resource and warrant digest;
- delimiter/canonicalization collision resistance in authority cache partitions;
- exact provider-plan and Execution IR binding of security proofs;
- rejection of provider substitution, plan cloning and changed security obligations;
- rejection of unknown or empty security obligations at the certification boundary.

These tests are intended as **safe rails**: transformations, caches, delegation,
serialization and provider boundaries must preserve or reduce effective
authority, never increase it or silently remove a security obligation.
