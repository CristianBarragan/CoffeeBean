# Foundgine.Security.Authority

`Foundgine.Security.Authority` contains optional, provider-agnostic infrastructure for managing and recovering authorization authority.

It is intentionally **outside the Foundgine execution core**.

## Boundary

Foundgine's core consumes a validated security execution context:

```text
External identity / authority system
            |
            v
    Validated security context
            |
            v
        Foundgine
            |
            v
   Semantic authorization
            |
            v
         Execution
```

This package owns the authority-side concerns that are not required to execute a semantic request, including witness quorum, credential lifecycle, journal reconciliation, promotion/failover, and recovery evidence.

The core Foundgine runtime does **not** depend on this package to perform semantic authorization or provider execution.

## What belongs here

- Authorization authority recovery
- Witness quorum and authority anchors
- Credential lifecycle and revocation
- Publication and journal integrity
- Cross-instance reconciliation and failover
- Recovery evidence and freshness checks

## What does not belong here

- GraphQL/MCP/AI transport handling
- Semantic request resolution
- Core authorization policy evaluation
- Query planning or optimization
- SQL generation
- Provider execution

Keeping this boundary explicit prevents Foundgine from becoming a distributed authorization control-plane product when its primary role is a semantic execution boundary.
