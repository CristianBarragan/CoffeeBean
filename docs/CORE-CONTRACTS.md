# Foundgine Core Contracts

## Purpose

The core contract is the boundary between application meaning and provider execution.

```text
Input Adapter
    ↓
Semantic Request
    ↓
Resolution
    ↓
Authorization
    ↓
Semantic Graph
    ↓
Execution Plan
    ↓
Provider Plan
    ↓
Provider Execution
    ↓
Result + Evidence
```

The core must remain independent of GraphQL, HTTP, AI/MCP, and any specific database provider.

## 1. Metadata

Metadata describes what exists.

It owns:

- entities
- fields
- relationships
- connections
- storage mappings
- authorization declarations

It does not execute requests.

## 2. Semantic Graph

The semantic graph describes what the application means and what the request wants to traverse.

It owns:

- entity identity
- semantic fields
- relationships
- AOT connections
- collection traversal
- semantic query options
- attached authorization predicates

It must not contain SQL, GraphQL ASTs, provider objects, or executable delegates.

## 3. Authorization Predicate

Authorization is represented as a small AOT predicate IR.

It may contain:

- context parameters
- resource parameters
- member access
- constants
- equality/inequality
- boolean composition
- negation

Expression trees and compiled delegates do not cross the runtime boundary.

## 4. Execution Plan

The execution plan is the deterministic representation of work that must be performed.

It is produced only after semantic resolution and authorization.

Providers consume the plan; they do not redefine its semantics.

## 5. Provider Plan

A provider plan is the provider-specific lowering of an execution plan.

SQL, EF, another database, or a remote service may have different implementations, but they must preserve the semantic contract.

## 6. Execution Result

Execution returns the requested result independently of the transport used to request it.

The result may include evidence.

## 7. Evidence

Evidence describes how an execution was produced without coupling the core to AI or a transport.

Evidence can identify:

- provider
- plan fingerprint
- authorized nodes
- rows returned
- execution timing
- provider-operation fingerprint

Sensitive provider internals should not become mandatory evidence.

## Architectural invariants

1. Adapters translate into Foundgine semantics.
2. Providers execute Foundgine plans.
3. Authorization is preserved into executable plans.
4. Missing authorization context fails closed.
5. AOT metadata is preferred over runtime discovery.
6. Domain semantics remain independent of physical storage.
7. Mapping uses convention first and explicit LINQ mapping only for overrides.
8. Runtime execution does not compile expression trees.
9. Collection traversal is a semantic operation, not a provider-specific feature.
10. Evidence is provider-neutral.

## Freeze rule

New features should first fit these contracts.

A new adapter, provider, or AI integration must not introduce a parallel semantic or authorization path.
