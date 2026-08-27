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

## Security boundary

The authority/recovery subsystem is deliberately optional. Foundgine core consumes a validated security execution context; it does not require the distributed authority recovery implementation to resolve semantics, authorize requests, plan execution, or execute providers. This keeps the semantic execution boundary small and auditable.

## Authorization recovery control plane

The `Foundgine.Security.Authority` namespace (implemented in
`src/Foundgine.Security.Authority/Recovery/`, a provider-agnostic library) covers
failure and recovery handling for the authorization control plane itself:
publication key lifecycle and rotation, promotion/commit atomicity,
cross-instance commit and journal consensus, repair-proposer credential
authentication and replication, and transaction-journal integrity. See
`docs/security/CHANGELOG.md` for the invariant-by-invariant history of this
module, and `tests/Foundgine.Security.Authority.Tests/` for the
corresponding adversarial test coverage. The PostgreSQL-specific wiring
(`PostgresAuthorizationContextStore`, `PostgresAuthorizationRecoveryCoordinator`,
`PostgresAuthorizationSecurityUnitOfWork`, and the transfer-funds executor)
remains in `samples/Foundgine.HighAssurance.Postgres/`.


## GraphQL mutation security boundary

GraphQL mutation translation is not an authorization boundary. Applications that execute Foundgine mutations from Hot Chocolate should use `Foundgine.GraphQL.HotChocolate.MutationExecution.FoundgineHotChocolateMutationExecutor`. The executor requires a host-owned `ISecurityExecutionContextProvider`, converts the GraphQL nested intent into the canonical `SemanticMutationOperationGraph`, and invokes `IFoundgineMutations.ExecuteAsync`.

The resulting path is:

```text
GraphQL payload
    ↓
Host security context
    ↓
GraphQL mutation adapter
    ↓
Canonical semantic mutation graph
    ↓
Warrant + tenant + audience + resource checks
    ↓
Semantic authorization
    ↓
Security-invariant certification
    ↓
Provider execution
```

Identity, tenant, audience, and warrant material must never be accepted from GraphQL arguments or variables. The final mutation execution gate remains authoritative even if an application accidentally bypasses earlier transport-level checks.
