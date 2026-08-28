# Step 5 — First-class capability definitions

A capability is now a first-class, provider-neutral semantic object. The existing `SemanticCapability` remains the compatibility contract, while `SemanticCapabilityDefinition` is the authoritative object consumed by future adapters.

```text
Semantic schema
      │
      ▼
Capability definition
 ├── semantic contract
 ├── authorization
 ├── constraints
 ├── effects
 ├── implementation binding
 └── metadata
      │
 ├── Agent Framework
 ├── MCP
 ├── GraphQL
 └── other projections
```

The mapping layer can materialize a definition without invoking application code. Runtime authorization and execution remain outside the generator.
