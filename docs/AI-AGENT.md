# Foundgine AI Agent

Foundgine's AI integration is intentionally layered:

```text
LLM / Agent Framework
        |
        | tool calls
        v
Foundgine.AI
        |
        v
IFoundgine
        |
        +-- semantic resolution
        +-- authorization
        +-- provider-independent planning
        +-- execution
```

The model is an untrusted producer of intent. `ExecutionContext` remains a host-owned
runtime value. A tool call can therefore request data, but cannot choose its tenant,
identity, authorization policy, provider or connection.

`FoundgineAiAgent` uses `Microsoft.Extensions.AI.FunctionInvokingChatClient` to run a
bounded function-calling loop. This keeps the Foundgine package independent of any one
LLM or agent framework. A provider-specific application can supply an OpenAI, Azure
OpenAI, Ollama or other `IChatClient` implementation.
