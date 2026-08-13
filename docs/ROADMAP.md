# Roadmap

The foundation is intentionally small. The next work should improve usefulness
without weakening the boundaries.

## M39 — Semantic authorization and capability discovery

M39 establishes granular authorization as part of semantic execution:

- entity read/write access;
- field read/write access;
- relationship read/write access;
- provider-independent conditional predicates;
- capability discovery for callers such as AI agents;
- mutation write authorization;
- authorization predicates preserved into the execution plan.

M39 deliberately does **not** introduce identity management, claims parsing,
role administration, OAuth/JWT handling, policy storage, or an authorization
server. Those concerns can sit above the semantic policy contract later.

The key invariant is:

```text
Caller capability context
        ↓
Authorization policy
        ↓
Semantic graph
        ↓
Authorization predicates
        ↓
Execution plan
        ↓
Provider execution
```

Capability discovery is advisory context only. Execution always evaluates the
configured policy again.

## M40 — Authorization-aware plan caching

M40 establishes a narrow, safe cache boundary for compiled provider plans.

- semantic resolution still runs on every request;
- authorization still runs on every request;
- only the provider compilation step is cached;
- authorization predicates remain in the cached provider plan;
- runtime execution context is resolved by the provider on every execution;
- exact request values are part of the current cache fingerprint.

This deliberately establishes correctness before introducing parameterized plan
templates or distributed caching.

## Near term

- simplify public APIs where the current contracts are more complex than necessary;
- improve provider composition and real-world examples;
- measure end-to-end performance;
- keep GraphQL and JSON adapters thin;
- document only capabilities that are implemented and tested.

## Later

Potential work includes more providers, richer semantic actions, claims/roles
integration above the policy contract, and stronger AI/agent integration.

These are ideas, not current core capabilities.

## Documentation rule

The active source and tests are the source of truth. Public documentation must distinguish implemented/demonstrated capabilities from planned work and historical material. See [Documentation truth](DOCUMENTATION-TRUTH.md).
