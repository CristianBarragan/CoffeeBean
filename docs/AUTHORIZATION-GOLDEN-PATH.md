# Foundgine Authorization Golden Path

Authorization is one of Foundgine's defining semantic boundaries.

The important property is not that Foundgine can answer `CanAccess(...)`.
Most application frameworks can do that.

The important property is that an authorization decision becomes part of the
semantic execution model and survives all the way to the provider.

## The path

```text
caller / agent
      |
      | semantic intent
      v
capability discovery (descriptive)
      |
      v
semantic resolution
      |
      v
authorization
  |       |       |
  |       |       +--> field / relationship access
  |       +----------> conditional resource predicate
  +------------------> entity access
      |
      v
authorized semantic graph
      |
      v
provider-independent execution plan
      |
      +-------------------+
      |                   |
      v                   v
 SQL provider       In-memory provider
      |                   |
      +---------+---------+
                v
          execution result
```

## The critical distinction

Capability discovery tells a caller what the current semantic policy says it
may request. It is **not** an authorization token.

A caller can discover:

```text
Customer       read: allowed
Customer.Name  read: allowed
Customer.SSN   read: denied
Customer.Accounts read: allowed
Customer rows  conditional
```

The actual request is still authorized again before planning.

This prevents the unsafe pattern:

```text
capability snapshot -> trusted execution
```

and keeps the safe pattern:

```text
capability snapshot -> construct intent
                         |
                         v
                    authorize again
                         |
                         v
                    plan + execute
```

## Conditional authorization

A policy can express a resource/context relationship without embedding a
provider-specific filter:

```text
resource.TenantId == context.TenantId
```

The predicate remains semantic until a provider lowers it.

For SQL this becomes a parameterized storage predicate.
For the in-memory provider it is evaluated against CLR data.

The provider changes; the authorization meaning does not.

## Fail closed

The golden path deliberately treats missing authorization context as an
execution error rather than as permission to return data.

That rule matters especially for agents, where an omitted context value must
never accidentally broaden access.

## Plan caching

Authorization is evaluated before provider-plan cache lookup.

The cache may reuse a provider plan shape, but a conditional authorization
predicate remains part of that plan and receives the current execution
context at execution time.

Therefore:

```text
request A + tenant 7  ----+
                          +--> same plan shape
request B + tenant 42 ----+

                       but

        authorization predicate
                 |
                 v
        current execution context
```

The cache is an optimization. It is not an authorization boundary.

## What this proves

Foundgine's authorization story is stronger than transport-level middleware
or a collection of controller checks because authorization is attached to the
same semantic graph that is later planned and executed.

The repository's tests should preserve these invariants:

1. denied entities cannot enter an authorized graph;
2. denied fields are removed;
3. denied relationships remove unreachable subtrees;
4. conditional predicates survive planning;
5. providers enforce those predicates;
6. missing context fails closed;
7. authorization is re-evaluated before plan-cache lookup;
8. capability discovery remains descriptive rather than authoritative.
