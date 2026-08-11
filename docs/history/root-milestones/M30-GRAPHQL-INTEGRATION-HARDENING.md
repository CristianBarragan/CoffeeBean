# M30 — GraphQL / Foundgine Integration Hardening

**Status: IMPLEMENTED**

M30 is a hardening milestone rather than another GraphQL syntax feature.

The goal is to prove that M22–M29 compose at the GraphQL adapter boundary while
the provider-neutral Foundgine contracts remain unchanged.

## Acceptance matrix

| Capability | Must compose with |
|---|---|
| Variables | fragments, aliases, directives, operation selection |
| Fragments | aliases, directives, nested selections |
| Aliases | fragments and mutation result shaping |
| Directives | variables and fragment expansion |
| Input coercion | mutation variables and defaults |
| Error semantics | variable/input validation |
| Multiple operations | queries and mutations |
| Schema generation | semantic model without GraphQL runtime dependencies |

## Boundary invariant

```text
GraphQL document
    |
    +-- operation selection
    +-- variable coercion
    +-- fragment expansion
    +-- directive evaluation
    +-- alias/result projection
    +-- GraphQL error mapping
    |
    v
Provider-neutral Foundgine contracts
    |
    +-- SemanticRequest
    +-- MutationIntent / NestedMutationIntent
    +-- SemanticQueryOptions
    |
    v
Planning -> Execution -> Provider
```

No GraphQL AST nodes, variable references, aliases, directives, operation
definitions, or GraphQL error objects are introduced into the semantic,
planning, execution, or SQL layers.

## Tests

`M30IntegrationHardeningTests` verifies:

1. query variables + fragments + aliases + directives + operation selection;
2. mutation variables + fragments + aliases + directives + operation selection;
3. client-facing error mapping through `TryAdapt`;
4. aliases remain result-shaping data rather than mutation intent data.

## What M30 deliberately does not do

M30 does not add another GraphQL feature. It is the checkpoint before further
GraphQL expansion.

After M30, the next feature should be selected based on actual Graphgine
requirements rather than continuing the milestone list mechanically.
