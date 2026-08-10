# FAQ

## Is Foundgine a GraphQL framework?

No. GraphQL/Graphgine is historical.

## Is Foundgine an AI framework?

No. It is an application-domain semantic and execution layer that AI systems can call.

## Why not just use Python libraries?

Python has excellent LLM, agent and RAG ecosystems.

Foundgine targets a different boundary: the existing application's domain and executable semantics, with .NET as the native application environment.

It does not need to replace those Python systems. An agent built with them can call Foundgine.

## Why not just use an ORM?

ORMs solve persistence mapping and object materialization.

Foundgine's core question is:

> How does a dynamic semantic intent become a constrained executable plan over an application's domain?

The technologies can coexist.

## Why not let the LLM generate SQL?

Because SQL is not the application's business vocabulary.

Foundgine wants the model to select from an explicit domain vocabulary and then have deterministic infrastructure produce the execution plan.

## Why not put English parsing in Foundgine?

Because Foundgine should remain deterministic and model-independent.

## Does Foundgine replace MCP?

No. MCP is a potential adapter.

## Is the semantic model supposed to duplicate the domain?

No.

The direction is the opposite: infer everything possible from existing metadata and explicitly configure only semantic information that cannot be inferred.

## Is it production ready?

No.

The lower execution substrate and read-intent proof are real, but the complete AI-facing action/policy/verification/MCP platform is still under development.
