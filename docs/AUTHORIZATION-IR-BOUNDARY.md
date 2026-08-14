# Authorization at the Semantic IR Boundary

Authorization is applied after semantic resolution and before planning.

```text
Semantic Graph
    ↓
Semantic IR
    ↓
Semantic Authorization
    ↓
Semantic Plan
    ↓
Execution IR
    ↓
Provider
```

The authorization policy is provider-independent. It reasons about semantic
entity, field, relationship and predicate identities.

Denied child entities and relationships remove their subtrees. A denied root
entity rejects the operation. Denied fields are removed from the selected field
set. Conditional predicates remain attached to the semantic node.

Providers may lower a predicate into their physical representation, but that
lowering is not the definition of authorization.

The invariant is:

> A provider never decides whether a semantic operation is authorized.

Authorization therefore cannot be reintroduced as a provider-specific filter
or a transport-specific rule.
