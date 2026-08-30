# Foundgine.AI

`Foundgine.AI` integrates Foundgine with `Microsoft.Extensions.AI`.

The package gives an AI/LLM caller a controlled tool surface over Foundgine semantic execution without making the model responsible for database access or authorization.

## Core boundary

```text
LLM / Agent
     │
     │ tool call
     ▼
Foundgine.AI
     │
     ▼
IFoundgine
     │
     ├── semantic resolution
     ├── validation
     ├── authorization
     ├── planning
     └── execution
     │
     ▼
Provider
```

The model produces intent.

Foundgine decides whether the intent is meaningful and authorized and controls the provider execution path.

## Package role

The package contains two main components:

- `FoundgineAiToolset` — exposes Foundgine operations as `AIFunction`s;
- `FoundgineAiAgent` — runs a bounded Microsoft.Extensions.AI function-calling loop.

The package does not hard-code an OpenAI/Azure/Ollama client.

## Toolset

Create the toolset around an existing Foundgine runtime:

```csharp
var toolset = new FoundgineAiToolset(
    foundgine,
    executionContextFactory,
    securityContextFactory: securityContextFactory);

var tools = toolset.CreateTools();
```

Both `foundgine_capabilities` and `foundgine_query` require a host-supplied `securityContextFactory`. If it is omitted (or returns `null`), both tools throw `UnauthorizedAccessException` rather than falling back to an unauthenticated call — the model can never satisfy this requirement itself.

The toolset provides the model-facing operations:

```text
foundgine_capabilities
foundgine_query
```

Capability discovery describes the semantic contract available to the current execution context.

## Capability discovery is not authorization

This distinction is critical:

```text
capability contract
      ↓
helps the model construct valid intent
      ↓
Foundgine resolves the intent
      ↓
authorization runs again
      ↓
execution
```

An LLM must never be trusted because it previously saw a capability description.

## Agent loop

`FoundgineAiAgent` uses `Microsoft.Extensions.AI` function invocation to run a bounded tool-calling conversation.

Conceptually:

```text
user request
    ↓
IChatClient
    ↓
model chooses tool
    ↓
FoundgineAiToolset
    ↓
Foundgine
    ↓
tool result
    ↓
model
```

The loop is bounded so a model cannot make unbounded tool calls.

## Host-owned execution context

The most important application rule is:

> **The model controls intent, not authority.**

The execution context factory should obtain its values from the trusted host/application boundary.

Good:

```text
HTTP authentication
      ↓
application session
      ↓
ExecutionContext
      ↓
FoundgineAiToolset
```

Bad:

```text
LLM tool arguments
      ↓
tenant = "other-tenant"
role = "admin"
      ↓
database
```

Never make ordinary model-generated arguments the source of tenant, identity, warrant, or provider credentials.

## Custom `IChatClient`

The package accepts the Microsoft.Extensions.AI abstraction, so the application can supply its preferred compatible chat client.

Foundgine.AI does not own:

- model selection;
- API credentials;
- model hosting;
- prompt policy;
- agent memory;
- orchestration beyond its bounded tool-calling helper.

## Tool iteration limits

A bounded iteration limit is a safety and resource-control mechanism.

It protects against:

- accidental tool loops;
- repeated invalid requests;
- model/tool oscillation;
- unexpected token/cost growth.

The limit is not an authorization control. The application still needs normal rate limits, authentication, quotas, and operational controls.

## Error handling

Failures remain explicit across the boundary:

```text
invalid capability
      ↓
tool error

invalid semantic intent
      ↓
resolution/validation error

unauthorized intent
      ↓
authorization failure

provider failure
      ↓
execution/provider error

iteration budget exhausted
      ↓
agent loop terminates
```

Do not convert authorization failures into successful empty data.

## Security model

The AI integration inherits Foundgine's security model.

Foundgine remains responsible for:

- semantic resolution;
- authorization;
- security predicates;
- planning;
- provider conformance;
- execution.

The host remains responsible for:

- authentication;
- identity lifecycle;
- tenant selection;
- authorization policy administration;
- secrets;
- rate limits;
- deployment/network controls;
- model provider credentials.

## Why this is safer than SQL generation

Avoid:

```text
LLM → SQL → database credentials
```

Prefer:

```text
LLM → semantic intent → Foundgine → authorized plan → provider
```

The model can ask for:

```text
Customer
  fields: Id, Name
  filter: TenantId = current tenant
```

It cannot decide to bypass the semantic model and issue arbitrary SQL.

## Relationship to MCP

MCP and AI are separate adapters.

```text
AI / Microsoft.Extensions.AI
            │
            ▼
       Foundgine.AI
            │
            ▼
        Foundgine

MCP client
    │
    ▼
Foundgine.MCP
    │
    ▼
Foundgine
```

They converge on the same semantic execution boundary.

## What this package does not provide

It is not:

- a general agent framework;
- a model provider;
- an autonomous workflow engine;
- a memory system;
- a prompt-injection detector;
- an authorization server.

It provides a controlled AI tool integration.

## Related packages

- `Foundgine` — runtime facade.
- `Foundgine.Intent.Json` — structured intent adapter.
- `Foundgine.MCP` — MCP transport.
- `Foundgine.Semantics` — semantic/security contracts.

## Target framework

- .NET 9
- Microsoft.Extensions.AI
- MIT licensed
