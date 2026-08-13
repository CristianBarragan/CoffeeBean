# Context-safe plan caching

Foundgine caches **compiled provider plans**, not authorization decisions.
The cache boundary must never become an authorization boundary bypass.

## Required request order

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
Provider execution with runtime context
```

A cache hit is therefore safe only because authorization has already produced the
same authorized execution shape, including any provider-independent authorization
predicate.

## Runtime context is execution data

A predicate such as:

```text
resource.TenantId == context.user.TenantId
```

belongs to the cached provider plan as structure. The value `context.user.TenantId`
does not belong in the cache key. Different users can therefore reuse the same
compiled plan while the provider evaluates the predicate against each request's
runtime context.

## What must make plans different

The cache key must distinguish anything that changes the compiled provider plan,
including:

- selected fields;
- relationships and traversal structure;
- filters and filter values when the current compiler embeds them;
- ordering;
- pagination shape;
- authorization predicates.

Changing the authorization predicate must result in a different provider-plan key.

## What must not be cached

Foundgine must not cache:

- authorization decisions;
- capability discovery results as authorization decisions;
- runtime `ExecutionContext` values;
- provider execution results.

## Security invariant

An unauthorized request must fail **before** provider-plan lookup or compilation.
A cached plan is never permission to execute it.

## Current implementation

The current engine performs authorization before calculating the provider-plan cache
key. `ExecutionPlanFingerprint.CreateShapeKey` includes authorization predicates and
excludes runtime pagination values that are bound during execution.

This is intentionally conservative. A future parameterized template cache should
only be introduced after request-value binding is explicitly separated from provider
plan shape.
