# Glossary

## Domain model

The application's own business entities and operations.

## Metadata

Machine-readable information describing entities, fields, relationships, joins and storage.

## Semantic model

Protocol-neutral application meaning exposed to Foundgine.

## Semantic entity

An application-facing entity descriptor in `Foundgine.Semantic`.

## Resolution

Turning an ambiguous reference into an explicit domain identity.

Resolution is identity-oriented: a successful resolution identifies one concrete entity.

## Traversal

A structured path through relationships used to select related data.

Traversal may be collection-valued:

```text
Customer → Accounts* → Transactions*
```

Traversal is not the same thing as resolving every intermediate entity to one identity.

## ReadIntent

Structured representation of a read request.

It is not natural-language parsing.

## ResolvedReadPlan

The result of resolving a `ReadIntent`.

It contains resolved identity information plus the target traversal/order/limit needed to construct a query intent.

## QueryIntent

Provider-neutral logical query request consumed by `QueryPlanner`.

## QueryPlan

Provider-neutral logical execution plan.

## ProviderPlan

Provider-specific executable plan.

## Composite model

A logical/domain model whose shape spans multiple physical storage entities.

Example:

```text
Product
 → Customer
 → CustomerBankingRelationship
 → Contract
 → Account
 → Transaction
```

The logical model does not require a matching physical table.

## Repeated entity

Multiple occurrences of the same entity type in one logical query, such as `Customer → Customer` or `Employee → Manager → Manager`.

Execution rows therefore preserve entity occurrence identity rather than collapsing repeated types.

## ExecutionRow

Provider-neutral result row with occurrence-aware entity values.

## Domain action

An explicitly exposed business operation such as `IssueRefund`.

## Policy

A rule determining whether an operation is permitted.

## Evidence

Structured information explaining resolution, planning, execution and verification.

## MCP

Model Context Protocol. A possible outer adapter for exposing Foundgine to AI clients.

It is not the Foundgine core.
