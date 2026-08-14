# Security

Foundgine treats external intent as untrusted input.

The important rule is:

```text
Input → Parse → Resolve → Authorize → Plan → Execute
```

Do not allow an adapter to bypass resolution or authorization.

## Security assurance level

Foundgine 0.1.x provides **repository-level security evidence**, not an independent security certification.

The repository currently tests:

- semantic entity, field, and relationship authorization;
- conditional authorization predicates carried into executable plans;
- fail-closed behaviour for missing authorization context;
- fail-closed behaviour when authorization removes the requested field set;
- hostile/unknown entities, fields, relationships, and unsupported operations;
- bounded intent depth/counts; and
- SQL parameterization of external values.

Foundgine 0.1.x has **not** undergone an independent security audit, penetration test, or formal verification. The project therefore makes no claim of security certification.

## Threat model

The primary boundary is an external producer that can construct structured intent but must not be able to define application capabilities or physical execution.

```text
untrusted caller / AI / API payload
              |
              v
       intent adapter
              |
              v
     semantic resolution
              |
              v
        authorization
              |
              v
    provider-independent plan
              |
              v
          provider
```

The model assumes that the application is still responsible for:

- authentication and identity;
- claims/roles and application policy management;
- transport security;
- rate and resource limits;
- database permissions;
- approval controls for sensitive operations;
- logging and monitoring; and
- dependency and infrastructure security.

Foundgine does not replace those controls.

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

## What a security review should still examine

Before production use in a security-sensitive system, an independent review should specifically examine:

1. authorization across nested relationship and connection traversal;
2. authorization predicate lowering and runtime context binding;
3. query and mutation plan caching;
4. provider-specific identifier handling;
5. resource exhaustion limits and large intent payloads;
6. GraphQL adapter behaviour at the transport boundary;
7. mutation authorization and dependency ordering; and
8. dependency, database, container, and deployment configuration.

The repository's tests are evidence for the current invariants; they are not a substitute for that review.
