# Foundgine MCP Adapter

## Boundary

MCP is a transport and interoperability layer. It is not a second semantic runtime.

```text
MCP client / agent
       |
       v
Foundgine.MCP
       |
       v
Foundgine Intent
       |
       v
semantic resolution
       |
       v
authorization
       |
       v
planning
       |
       v
provider execution
       |
       v
execution evidence / receipt
```

The adapter exposes the same semantic capability contract used by Foundgine's other machine-facing surfaces.

## Tools

### `foundgine_capabilities`

Returns the canonical `SemanticCapabilityContract` for the current authorization context.

The result is discovery information. It is not an authorization grant.

### `foundgine_query`

Accepts the existing JSON `ReadIntent` representation and sends it through the normal Foundgine execution pipeline.

The model must not supply:

- tenant IDs
- identity IDs
- authorization predicates
- SQL
- provider names
- connection strings
- database credentials

Those belong to the host execution context.

## Security invariant

```text
MCP arguments
    != authorization context

MCP capability discovery
    != authorization decision

MCP execution
    -> Foundgine authorization
    -> Foundgine planning
    -> Foundgine provider
```

A caller cannot bypass Foundgine by changing the MCP payload.

## Transport ownership

The package deliberately does not own the server transport. Hosts may choose stdio, Streamable HTTP, or another transport supported by the official MCP C# SDK.

This keeps `Foundgine.MCP` focused on protocol adaptation rather than server hosting infrastructure.
