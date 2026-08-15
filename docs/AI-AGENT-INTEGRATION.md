# AI Agent Integration

Foundgine exposes a provider-neutral AI tool surface through `Foundgine.AI`.

## Design

```text
AI agent
   |
   +-- foundgine_capabilities
   |
   +-- foundgine_query
            |
            v
       ReadIntent
            |
       resolution
            |
       authorization
            |
          plan
            |
        provider
```

The agent supplies only semantic intent. The host application supplies `ExecutionContext`, so tenant, identity and other runtime values never become model-controlled arguments.

`Microsoft.Extensions.AI` is used as the integration abstraction. Its `AIFunction`/`AIFunctionFactory` types are provider-neutral and can be consumed by OpenAI, Azure OpenAI, Ollama and other chat clients. `FunctionInvokingChatClient` can automatically run requested functions. 

## Example

```csharp
var tools = new FoundgineAiToolset(
    foundgine,
    contextFactory: () => currentUserExecutionContext)
    .CreateTools();

var chatOptions = new ChatOptions { Tools = tools };
```

The model first discovers capabilities and then submits JSON read intent. Foundgine parses the intent, compiles it to the canonical semantic request, re-evaluates authorization, plans it and executes it.

## Security boundary

Do not expose `ExecutionContext` as an AI function parameter.

The AI is an untrusted producer of intent. Authentication, tenant identity, authorization context and other trusted runtime values belong to the host application.
