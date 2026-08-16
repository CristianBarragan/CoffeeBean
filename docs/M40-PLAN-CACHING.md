# M40 — Authorization-aware plan caching

M40 adds a deliberately narrow cache boundary: Foundgine caches **compiled provider plans**, not authorization decisions.

## Safety invariant

Every request still performs:

```text
Semantic request
    ↓
Resolution
    ↓
Authorization
    ↓
Execution plan
    ↓
Cache lookup
    ↓
Provider compilation (miss only)
    ↓
Execution
```

A cache hit therefore never bypasses authorization.

## Authorization is part of the cached plan

The execution-plan fingerprint includes the complete authorization predicate attached to every execution node. Runtime context values are not folded into the predicate or removed during caching.

For example:

```text
Employee.TenantId == ExecutionContext.user.TenantId
```

remains part of the plan. A SQL provider can bind `ExecutionContext.user.TenantId` when the cached provider plan executes.

## Exact-plan caching first

M40 intentionally caches **exact execution plans**, including request filter values. This is conservative: two requests with different semantic filter values do not share a compiled plan.

That avoids pretending that provider plans are parameter templates when the current provider compiler still embeds request parameter bindings in its plan representation.

A future template cache can safely generalize this only after parameter binding is explicitly separated from plan shape.

## What M40 does not do

- It does not cache authorization decisions.
- It does not cache `DescribeCapabilities()`.
- It does not include runtime authorization context values in the cache key.
- It does not introduce distributed caching.
- It does not introduce LRU complexity; eviction is bounded and best-effort.

The purpose of M40 is to establish the security invariant before optimizing the cache model further.
