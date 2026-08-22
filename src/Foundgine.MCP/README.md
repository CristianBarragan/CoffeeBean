# Foundgine.MCP

MCP is a transport adapter for Foundgine's semantic application contract.

It exposes two tools:

- `foundgine_capabilities` — discovers the canonical capability contract visible to the host-authenticated, warrant-backed caller.
- `foundgine_query` — submits provider-neutral read intent to Foundgine with host-owned security context.

MCP does not perform authorization, semantic resolution, planning, SQL generation, or provider execution. Those remain inside Foundgine.

## Hosting

The host owns the MCP transport. With the official C# SDK, a stdio server can register `FoundgineMcpTools` with `WithTools<FoundgineMcpTools>()`. HTTP hosting can use the official `ModelContextProtocol.AspNetCore` package.

Example shape:

```csharp
builder.Services.AddFoundgine(...);
builder.Services.AddFoundgineMcp(() => new ExecutionContext());
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<FoundgineMcpTools>();
```

For authenticated applications, the `ExecutionContext` factory should be backed by the host's authenticated request/session context. Do not let MCP tool arguments supply tenant or authorization context.

## Safe mutations

Foundgine.MCP can expose semantic mutation dry-run, approval, and exact-plan execution through `FoundgineMcpMutationTools` when the host configures `FoundgineOptions.MutationSchema` and `MutationProvider`.

The MCP layer does not authorize or execute mutations itself. It delegates to `IFoundgineMutations`, which re-plans and re-authorizes before executing an approved plan.


## Security-aware discovery

Capability discovery is now part of the security boundary. A host-supplied `SecurityExecutionContext` is required. Foundgine verifies the warrant before returning the capability contract and filters the contract to capabilities granted by that warrant. Discovery never consumes replay state; execution still verifies authorization and consumes the warrant according to the execution policy.

## Hostile-agent boundary

MCP payloads are treated as hostile input. Security/provider control properties are rejected by the JSON boundary, structural complexity is bounded before planning, and host-owned `SecurityExecutionContext` remains the sole source of subject, tenant, audience, resource scope, and warrant authority.
