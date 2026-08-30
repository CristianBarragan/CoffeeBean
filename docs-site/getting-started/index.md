# Get started with Foundgine

The canonical `Foundgine.SupplyChain` sample is the fastest way to understand the architecture in a real application.

## What you will run

```text
Agent / MCP client
      ↓
API
      ↓
Application capability
      ↓
Domain + AOT metadata
      ↓
Semantics
      ↓
Planning / ExecutionIR
      ↓
Foundgine.Sql
      ↓
PostgreSQL
```

## Prerequisites

- .NET 9 SDK
- Docker / Docker Compose
- a clone of the Foundgine repository

## Start PostgreSQL

Use the repository's supplied PostgreSQL Compose configuration.

```bash
docker compose -f docker-compose.postgres.yml up -d
```

## Run the sample

The exact command and configuration are maintained in `samples/Foundgine.SupplyChain/GUIDE.md`. The important part of the exercise is following one request through the layers rather than memorizing a command sequence.

## Layer-by-layer

### API

Transport handling only. It should not construct SQL or become the authorization authority.

### Application

Business capabilities and use-case orchestration. This is where application ownership of the operation remains visible.

### Domain

Domain types and business concepts.

### AOT metadata

`Foundgine.Aot.Generator` turns compile-time declarations into generated metadata, reducing runtime discovery and supporting Native AOT-friendly applications.

### Semantics

Structural metadata becomes application meaning: semantic entities, fields, relationships, capabilities and authorization.

### Planning

Semantic operations become provider-independent plans and `ExecutionIR`. Physical SQL is not part of this layer.

### Execution / provider

`Foundgine.Execution` owns the final execution boundary. `Foundgine.Sql` lowers the work to parameterized SQL and executes it through ADO.NET/PostgreSQL.

### MCP

MCP exposes capabilities to an external caller. It remains an adapter; host-owned identity and authorization stay outside the protocol.

### Testing

The repository tests each seam independently, then composes them into PostgreSQL and end-to-end scenarios.

## Next

Read [What is Foundgine](../what-is-foundgine.html) next.
