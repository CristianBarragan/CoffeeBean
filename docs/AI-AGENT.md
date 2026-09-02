# AI agent integration

Foundgine's AI integration is deliberately small: it exposes semantic execution as tools without making Foundgine depend on a particular LLM provider or agent framework.


## AI agents use the same canonical lifecycle

An agent is another untrusted caller. It does not receive a special execution path:

```plantuml
@startuml
start
:AI Agent;
:Intent;
:Semantic Model;
:Semantic Operation Graph;
:Retrieval / Resolution;
:Authorization;
:Plan Binding;
:Execution IR → Provider → Execution → Evidence;
stop
@enduml
```

Capability discovery is descriptive. Search results, model output and tool arguments remain untrusted until the normal semantic and authorization stages accept them.

## Architecture

```plantuml
@startuml
start
:LLM / Agent Framework;
:Microsoft.Extensions.AI;
:Foundgine.Providers.Models;
:IFoundgine;
:semantic resolution;
:authorization;
:planning;
:provider execution;
stop
@enduml
```

The model is an untrusted producer of intent.

## What `Foundgine.Providers.Models` provides

The package contains:

- `FoundgineAiToolset`;
- `FoundgineAiAgent`.

The toolset exposes semantic capability discovery and query execution as `AIFunction`s.

The agent helper runs a bounded function-calling loop using `Microsoft.Extensions.AI`.

## Capability discovery

An agent can first discover the semantic capabilities available to its execution context.

The flow is:

```plantuml
@startuml
start
:host context;
:capability contract;
:LLM;
:structured intent;
stop
@enduml
```

Discovery is descriptive.

The request is still authorized when executed:

```plantuml
@startuml
start
:LLM intent;
:Foundgine resolution;
:authorization;
:plan;
:provider;
stop
@enduml
```

## Security boundary

Never let model-generated tool arguments choose:

- identity;
- tenant;
- audience;
- warrant;
- provider;
- connection string;
- authorization policy.

The host supplies the trusted execution context.

## Example shape

```csharp
var toolset = new FoundgineAiToolset(
    foundgine,
    executionContextFactory);

var tools = toolset.CreateTools();
```

The application supplies its preferred `IChatClient` and can then use the returned tools in its agent loop.

## `FoundgineAiAgent`

The agent helper uses Microsoft.Extensions.AI function invocation to support:

```plantuml
@startuml
start
:user request;
:chat client;
:tool call;
:FoundgineAiToolset;
:Foundgine;
:tool result;
:chat client;
stop
@enduml
```

The loop is bounded by tool-iteration/resource controls.

It is not a general autonomous-agent runtime.

## What remains application-owned

The host/application owns:

- model/provider credentials;
- model selection;
- system prompts;
- conversation memory;
- business approval;
- authentication;
- identity/tenant context;
- rate limiting;
- deployment;
- observability.

Foundgine only owns the semantic execution boundary.

## Prompt injection

Foundgine cannot make arbitrary natural-language instructions trustworthy.

The security strategy is instead to make the execution authority independent of the model:

```plantuml
@startuml
start
:untrusted text;
:model-generated intent;
:semantic resolution;
:host-backed authorization;
:controlled provider execution;
stop
@enduml
```

Malicious text in data must not be able to grant the model new Foundgine authority.

Applications still need model/application-level prompt-injection defenses.

## Why not SQL generation?

The intended pattern is:

```plantuml
@startuml
start
:AI;
:semantic intent;
:Foundgine;
:provider;
stop
@enduml
```

not:

```plantuml
@startuml
start
:AI;
:SQL;
:database;
stop
@enduml
```

This keeps database credentials and physical schema outside the model's control.

## Relationship to MCP

AI and MCP are independent adapters over the same runtime:

```plantuml
@startuml
start
:Microsoft.Extensions.AI;
:Foundgine.Providers.Models;
:Foundgine;
stop
@enduml
```

```plantuml
@startuml
start
:MCP;
:Foundgine.Providers.Tools.MCP;
:Foundgine;
stop
@enduml
```

An application can use either or both.

## Relationship to GraphQL/JSON

GraphQL and JSON are also intent adapters.

The important property is convergence:

```plantuml
@startuml
card GraphQL
card JSON
card MCP
card AI
card "C#" as CSharp
card "semantic intent" as SI
card "same authorization/planning/execution" as Exec
GraphQL --> SI
JSON --> SI
MCP --> SI
AI --> SI
CSharp --> SI
SI --> Exec
@enduml
```

## Current scope

This integration does not claim to be:

- an agent framework;
- an LLM gateway;
- an autonomous workflow engine;
- a memory/vector system;
- a model safety platform.

It is a controlled AI tool integration.

## Related source package

See `src/Foundgine.Providers/Foundgine.Providers.Models/README.md` for the package-level API and security contract.

---

Next: [PostgreSQL E2E](POSTGRES-E2E.md)
