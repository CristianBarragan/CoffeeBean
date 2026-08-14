# Multi-model boundary

Foundgine is deliberately not an ORM and does not require one model to serve every layer of an application.

A realistic application can have at least four representations:

```text
Persistence model
    EF Core entities, columns, keys, storage relationships
             |
             | semantic mapping / metadata
             v
Foundgine semantic model
    entities, fields, relationships, connections, capabilities
             |
       +-----+-----+
       |           |
       v           v
Transport       Structured intent
GraphQL         JSON / application code / AI
       |           |
       +-----+-----+
             |
             v
      resolution + authorization
             |
             v
      provider-independent plan
             |
             v
      SQL / InMemory / future provider
```

## Worked example

Suppose the persistence model contains:

```text
Customer
  Id
  TenantId
  InternalRiskScore
  Accounts
```

The application may intentionally expose a smaller semantic surface:

```text
Customer
  id
  name
  accounts
    balance
```

GraphQL can expose:

```graphql
customer {
  name
  accounts {
    balance
  }
}
```

While JSON or an AI producer can express the same request as structured intent:

```json
{
  "rootEntity": "Customer",
  "selections": ["name"],
  "relationships": ["accounts"]
}
```

These representations are not expected to be identical. They are different descriptions of the same application-level capability.

## What changes when the EF model changes?

A persistence change does not automatically become a new public capability.

For example, adding:

```text
Customer.InternalRiskScore
```

to the EF entity does not expose that field to GraphQL, JSON, or an AI caller. The semantic model must deliberately expose a field, and authorization must permit access to it before it can enter an executable plan.

Conversely, a semantic field can be backed by a projection or connection rather than a 1:1 persistence property. The semantic contract remains stable while the provider-specific mapping changes.

The intended ownership is therefore:

| Layer | Authority |
|---|---|
| Persistence | EF/database schema and storage relationships |
| Semantic | Application capabilities and externally executable meaning |
| Transport | GraphQL/JSON representation of requests and results |
| Intent | What an untrusted or trusted caller asks to do |
| Authorization | What the caller may do with the semantic model |
| Plan | The authorized operation to execute |
| Provider | How the authorized operation is physically executed |

## Why this is not free

A semantic layer is additional architecture. It is not justified for every CRUD application.

The trade-off becomes useful when:

- multiple transports expose the same application capabilities;
- the public model intentionally differs from persistence;
- authorization needs to survive into execution rather than remain a transport check;
- AI or other untrusted producers need structured access to the application model; or
- multiple physical execution strategies should consume the same logical plan.

If an application has a simple 1:1 CRUD model and no need for these boundaries, an ORM and conventional API layer may be simpler.

## Design invariant

The external representation must never become the authority for capabilities.

```text
external request
      ↓
parse
      ↓
resolve against semantic model
      ↓
authorize
      ↓
plan
      ↓
execute
```

This is the reason Foundgine keeps semantics separate from transport and provider concerns.
