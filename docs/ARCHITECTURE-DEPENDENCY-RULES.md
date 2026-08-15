# Architecture Dependency Rules

## Allowed direction

```text
Adapters -> Core
Planning -> Semantics
Authorization -> Semantics / Planning
Execution -> Planning / Semantics
Providers -> Execution contracts
Tests -> Anything under test
```

## Forbidden direction

Core semantic contracts must not depend on:

- MCP
- GraphQL
- Hot Chocolate
- EF Core
- SQL providers
- HTTP transport
- AI SDKs

Adapters must not define competing semantic models.

Providers must not authorize requests.

Optimizers must not grant permissions.

Receipts must not grant permissions.
