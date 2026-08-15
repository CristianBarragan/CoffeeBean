# Foundgine.MCP

MCP is a transport adapter for Foundgine's semantic application contract.

It exposes two tools:

- `foundgine_capabilities` — discovers the canonical capability contract for the current caller.
- `foundgine_query` — submits provider-neutral read intent to Foundgine.

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
