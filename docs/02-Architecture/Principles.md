# Architecture Principles

## 1. Domain is the source of truth

Foundgine should derive as much as possible from the application's existing domain and metadata.

## 2. Semantic configuration is an exception mechanism

Do not make developers describe identities, fields and relationships twice when metadata already knows them.

Explicit semantic configuration should focus on things that cannot be safely inferred:

- fuzzy search;
- aliases;
- descriptions;
- exposed actions;
- policy overrides.

## 3. Intent is structured

Foundgine does not need to parse English.

An LLM, UI or application can produce a structured intent.

## 4. Planning is deterministic

Once intent is structured and resolved, planning should be deterministic and inspectable.

## 5. Providers are adapters

SQL is an execution target, not the logical planning model.

## 6. Agents cannot invent executable operations

Actions must be explicitly exposed.

## 7. No silent resolution

Ambiguity is a result.

It is not permission to guess.

## 8. Mutations must be constrained

Updates/deletes require filters at the lower planner level.

Future semantic mutations should add authorization and preview/approval.

## 9. Evidence is a first-class outcome

Important executions should be explainable.

## 10. Prove before generalizing

A new abstraction must earn its place through a real scenario.

## 11. Transport stays outside the core

GraphQL, REST, gRPC and MCP are adapters.

## 12. Do not optimize an unmeasured path

Benchmark before claiming performance advantages.
