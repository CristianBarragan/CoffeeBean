# Execution Pipeline

> The Execution Pipeline is the heart of CoffeeBeanery. It defines the complete lifecycle of a request—from the moment it enters the framework until the final objects are returned to the caller. Every layer has a single responsibility, and every transition between layers is explicit, deterministic, and testable.

Unlike traditional ORMs that continuously discover metadata and construct queries at runtime, CoffeeBeanery executes precomputed knowledge produced during compilation.

---

# Philosophy

Execution follows one rule:

> **Plans execute. They are never interpreted.**

Compilation creates knowledge.

Runtime executes knowledge.

---

# High-Level Pipeline

Every request follows the same lifecycle.

```
Request

↓

Planner

↓

Execution Plan

↓

Runtime

↓

Provider

↓

Database

↓

Materializer

↓

Response
```

Each stage has a single responsibility.

---

# Pipeline Overview

The complete pipeline consists of eight stages.

```
Client

↓

Transport

↓

Planner

↓

Execution Plan

↓

Runtime

↓

Database Provider

↓

Materializer

↓

Response
```

Each stage communicates using immutable contracts.

---

# Stage 1 — Request

The request originates from a transport.

Examples:

```
GraphQL

REST

gRPC

SignalR

CLI
```

The Runtime does not know which transport initiated the request.

---

# Stage 2 — Planning

The transport converts protocol-specific requests into framework plans.

Example:

```
GraphQL Query

↓

QueryPlan
```

or

```
REST POST

↓

MutationPlan
```

Planning is the final transport responsibility.

---

# Stage 3 — Execution Plan

Execution plans describe **what** should happen.

Examples:

```
QueryPlan

MutationPlan

GraphPlan
```

Plans contain instructions rather than executable behavior.

They are immutable.

---

# Stage 4 — Runtime

Runtime coordinates execution.

Responsibilities include:

- Transaction management
- SQL generation
- Provider coordination
- Materialization
- Graph assembly

Runtime performs no metadata discovery.

---

# Stage 5 — SQL Provider

The SQL provider translates execution plans into database-specific commands.

Example:

```
MutationPlan

↓

PostgreSqlWriter

↓

SQL
```

Different providers produce different SQL while Runtime remains unchanged.

---

# Stage 6 — Database

The database performs execution.

Examples:

```
SELECT

INSERT

UPDATE

DELETE

MERGE

UPSERT
```

Database execution remains outside the framework.

---

# Stage 7 — Materialization

Rows become objects.

```
Rows

↓

Generated Materializer

↓

Objects
```

Materialization uses generated code rather than reflection.

---

# Stage 8 — Response

The transport converts framework objects into protocol responses.

Examples:

```
Objects

↓

JSON
```

```
Objects

↓

GraphQL Response
```

```
Objects

↓

Protocol Buffers
```

Serialization belongs entirely to the transport.

---

# Query Execution

Query execution follows this simplified flow.

```
Query

↓

QueryPlan

↓

SQL

↓

Reader

↓

Materializer

↓

Objects
```

No metadata analysis occurs after planning.

---

# Mutation Execution

Mutation execution is similar.

```
Mutation

↓

MutationPlan

↓

Dependency Resolution

↓

SQL

↓

Execution

↓

Materialization
```

Dependency ordering is determined before execution.

---

# Graph Execution

Graph execution introduces traversal.

```
GraphPlan

↓

Traversal

↓

SQL

↓

Rows

↓

Graph Assembly

↓

Result
```

Traversal instructions are precomputed.

---

# Runtime Components

The Runtime coordinates several specialized services.

```
Execution Coordinator

↓

Transaction Coordinator

↓

SQL Writer

↓

Materializer

↓

Graph Executor
```

Each service owns a single responsibility.

---

# Metadata Usage

Runtime consumes immutable metadata.

```
GeneratedMetadataProvider

↓

Runtime
```

Metadata is never modified during execution.

---

# SQL Generation

Execution plans become SQL.

```
Execution Plan

↓

SQL Writer

↓

Command Text
```

SQL generation is deterministic serialization.

---

# Parameter Generation

Parameters are generated independently.

```
Execution Plan

↓

Parameter Writer

↓

DbParameter[]
```

SQL text and parameters remain separate.

---

# Transactions

Transactions wrap execution.

```
Begin

↓

Execute

↓

Commit
```

Failures trigger rollback.

Transaction policies remain provider-independent.

---

# Error Handling

Exceptions propagate through the pipeline.

```
Provider Exception

↓

Framework Exception

↓

Transport Exception

↓

Client
```

Each layer translates only its own concerns.

---

# Cancellation

Cancellation tokens flow through every stage.

```
Request

↓

Runtime

↓

Provider

↓

Database
```

Cancellation should remain cooperative.

---

# Async Flow

Execution should remain asynchronous.

```
Plan

↓

ExecuteAsync()

↓

Reader

↓

MaterializeAsync()
```

Avoid blocking operations.

---

# Thread Safety

Generated metadata is immutable.

Execution services should avoid shared mutable state.

Each request operates independently.

---

# Allocation Strategy

Execution minimizes allocations by using:

- Immutable metadata
- Generated materializers
- Reusable builders
- Shared registries

Temporary allocations should remain localized.

---

# Logging

Logging should observe execution rather than influence it.

Typical events include:

- Request started
- SQL generated
- Execution completed
- Exception thrown
- Transaction committed

Logging should never alter execution behavior.

---

# Diagnostics

Execution diagnostics should expose:

- Generated SQL
- Execution time
- Materialization time
- Provider activity
- Allocation metrics

Diagnostics should remain optional.

---

# Pipeline Invariants

The pipeline guarantees:

- Immutable plans
- Immutable metadata
- One-way dependencies
- Provider independence
- Transport independence
- Deterministic execution

These invariants should never be violated.

---

# Pipeline Diagram

The complete execution flow is:

```
Client

↓

Transport

↓

Planner

↓

Execution Plan

↓

Runtime

↓

SQL Provider

↓

Database

↓

Rows

↓

Generated Materializer

↓

Objects

↓

Transport

↓

Client
```

Each transition crosses a well-defined architectural boundary.

---

# Performance Characteristics

The pipeline avoids:

- Reflection
- Runtime metadata discovery
- Dynamic SQL generation
- Runtime graph analysis
- Assembly scanning

Performance comes from removing unnecessary work.

---

# Testing

Each stage can be tested independently.

```
Planner

↓

Execution

↓

SQL

↓

Materializer

↓

Transport
```

Isolation simplifies testing and debugging.

---

# Native AOT

The pipeline naturally supports Native AOT because it relies on:

- Generated metadata
- Generated materializers
- Explicit registrations
- Static dependencies

No runtime code generation is required.

---

# Future Evolution

Future enhancements may include:

- Compiled execution plans
- Generated SQL serializers
- Batch execution
- Streaming materialization
- Distributed execution
- Execution tracing

These improvements should preserve the pipeline structure.

---

# Summary

The CoffeeBeanery Execution Pipeline separates communication, planning, execution, provider translation, materialization, and serialization into independent architectural stages connected by immutable contracts.

By executing precomputed plans instead of interpreting runtime metadata, the pipeline remains deterministic, provider-independent, transport-independent, highly testable, allocation-efficient, and fully compatible with Native AOT while providing a clear and extensible execution model for future transports and storage providers.

# Metadata System Architecture

> The Metadata System is the structural foundation of CoffeeBeanery. It represents the application's compile-time knowledge in an immutable, runtime-consumable form. Every planner, SQL writer, graph executor, and materializer depends upon metadata rather than CLR types, allowing Runtime to execute without reflection or runtime discovery.

Metadata answers structural questions.

It never executes behavior.

---

# Philosophy

Metadata represents **knowledge**, not **state**.

A useful rule is:

> **Metadata describes what exists. Runtime decides what happens.**

Metadata should never contain executable logic.

---

# High-Level Architecture

```
Application Models

↓

Source Generator

↓

Metadata

↓

Runtime

↓

Execution
```

The generator produces metadata once.

Runtime consumes it many times.

---

# Why Metadata Exists

Without metadata:

```
Runtime

↓

Reflection

↓

Model Discovery

↓

Execution
```

With metadata:

```
Runtime

↓

Metadata Lookup

↓

Execution
```

Reflection disappears completely.

---

# Compile-Time Knowledge

Metadata captures everything the compiler already knows.

Examples include:

- Entities
- Models
- Properties
- Relationships
- Graphs
- Columns
- Identifiers
- Constraints

This information never changes while the application is running.

---

# Runtime Knowledge

Runtime should never inspect CLR types.

Instead it consumes:

```
IMetadataProvider

↓

EntityMetadata

↓

Execution
```

This preserves transport and provider independence.

---

# Metadata Hierarchy

The complete hierarchy is:

```
MetadataProvider

├── EntityMetadata

├── ModelMetadata

├── GraphMetadata

├── JoinMetadata

└── Lookup Metadata
```

Each metadata object owns one responsibility.

---

# Metadata Provider

The provider is the Runtime entry point.

```csharp
IMetadataProvider
```

Responsibilities include:

- Entity lookup
- Model lookup
- Graph lookup
- Join lookup

The provider performs lookups only.

---

# Entity Metadata

Entity metadata describes persistent entities.

Example:

```csharp
EntityMetadata
{
    Id
    Name
    Schema
    Table
    Columns
    Keys
}
```

This information rarely changes.

---

# Model Metadata

Models describe projections.

Examples include:

```
DTOs

View Models

GraphQL Types

Read Models
```

Models do not necessarily map directly to database tables.

---

# Column Metadata

Each persistent column is represented explicitly.

Example:

```csharp
ColumnMetadata
{
    Id
    Name
    StoreName
    Type
    Nullable
}
```

Column metadata should contain structure rather than behavior.

---

# Relationship Metadata

Relationships connect entities.

Examples:

```
One-to-One

One-to-Many

Many-to-Many

Self References
```

Relationship metadata describes topology rather than traversal.

---

# Join Metadata

Join metadata describes how entities connect.

Example:

```csharp
JoinMetadata
{
    Source

    Target

    Columns

    JoinType
}
```

Runtime uses joins during planning.

---

# Graph Metadata

Graph metadata describes graph structures.

Example:

```
Nodes

Edges

Traversal Rules

Identity Columns
```

Graph metadata should remain immutable.

---

# Lookup Metadata

Lookup metadata describes reusable lookup entities.

Example:

```
Country

Currency

Language

Status
```

Lookup metadata simplifies mutation planning.

---

# Identifier System

Every metadata object receives a stable identifier.

Example:

```
EntityId

ColumnId

JoinId

GraphId
```

Identifiers should be deterministic across builds.

---

# Stable Ordering

Generated metadata should always appear in a deterministic order.

Preferred ordering:

```
Namespace

↓

Entity

↓

Column
```

Avoid dependency upon compiler enumeration order.

---

# Metadata Immutability

Metadata should be immutable.

Example:

```csharp
ImmutableArray<T>
```

Avoid mutable collections.

Immutability provides:

- Thread safety
- Simpler caching
- Easier reasoning

---

# Metadata Lifetime

Metadata exists for the application's lifetime.

```
Application Startup

↓

Singleton Metadata

↓

Execution
```

Metadata should never be rebuilt.

---

# Metadata Lookup

Runtime retrieves metadata through identifiers.

Preferred:

```csharp
_metadata.GetEntity(id)
```

Avoid repeated string comparisons.

---

# Array-Based Storage

Identifiers enable array indexing.

```
EntityId

↓

EntityMetadata[]
```

Array lookups are faster and allocation-free.

---

# Metadata Generation

The source generator produces metadata.

Pipeline:

```
Roslyn

↓

Parser

↓

Internal Model

↓

Metadata

↓

Generated Source
```

Runtime never creates metadata.

---

# Metadata Validation

Generation validates metadata before emission.

Examples:

- Duplicate identifiers
- Invalid keys
- Cyclic joins
- Missing relationships
- Unsupported types

Runtime assumes metadata is valid.

---

# Metadata and Planning

Planning consumes metadata.

```
Metadata

↓

Planner

↓

Execution Plan
```

Metadata should not contain planning decisions.

---

# Metadata and SQL

SQL writers also consume metadata.

Examples:

```
Schema

Table

Column

Primary Key
```

SQL writers should never inspect CLR properties.

---

# Metadata and Materializers

Materializers use metadata only during generation.

Generated code performs direct column access at runtime.

---

# Versioning

Metadata contracts should evolve carefully.

Changing metadata structures affects:

- Runtime
- Generator
- Providers

Backward compatibility should be considered.

---

# Extensibility

New metadata types should follow existing principles:

- Immutable
- Focused
- Deterministic
- Generated
- Strongly typed

Avoid generic metadata bags.

---

# Testing

Metadata should be tested independently.

Recommended tests:

```
Entity Tests

↓

Relationship Tests

↓

Graph Tests

↓

Lookup Tests

↓

Snapshot Tests
```

Generated metadata should remain stable.

---

# Native AOT

Metadata enables Native AOT by replacing:

- Reflection
- Runtime discovery
- Dynamic inspection

with generated immutable objects.

---

# Future Evolution

Potential future metadata includes:

```
Security Metadata

Validation Metadata

Authorization Metadata

Caching Metadata

Provider Metadata

Diagnostics Metadata
```

Each should remain independent.

---

# Metadata Checklist

Before adding metadata, ask:

- Is this structural knowledge?
- Is it immutable?
- Can it be generated?
- Does Runtime need it?
- Is it deterministic?
- Can it be strongly typed?

If not, it probably does not belong in metadata.

---

# Relationship to the Framework

Metadata forms the contract between compilation and execution.

```
Source Code

↓

Generator

↓

Metadata

↓

Runtime

↓

Execution
```

It is the single source of structural truth.

---

# Summary

The Metadata System transforms compile-time knowledge into immutable runtime contracts that describe entities, models, columns, relationships, joins, graphs, and identifiers.

By separating structural knowledge from execution behavior, CoffeeBeanery eliminates reflection, enables deterministic planning, supports Native AOT, simplifies provider implementations, and establishes a stable architectural foundation shared across every layer of the framework.

# Query Planning Architecture

> Query Planning is the process of transforming a transport request into a deterministic, immutable execution plan. The planner analyzes the requested data, resolves metadata, constructs graph traversal instructions, determines joins and projections, and produces a complete `QueryPlan` that Runtime can execute without performing additional structural analysis.

The planner is the last component that reasons about application structure.

Runtime only executes the resulting plan.

---

# Philosophy

The planner exists for one purpose:

> **Convert intent into instructions.**

A request expresses *what* the client wants.

A QueryPlan describes *how* Runtime will obtain it.

---

# High-Level Pipeline

```
Transport Request

↓

Planner

↓

Metadata Resolution

↓

Relationship Resolution

↓

Projection Analysis

↓

Graph Planning

↓

QueryPlan
```

Planning completes before Runtime begins.

---

# Why Planning Exists

Without planning:

```
Request

↓

Runtime

↓

Analyze Metadata

↓

Build SQL

↓

Execute
```

With planning:

```
Request

↓

Planner

↓

QueryPlan

↓

Runtime

↓

Execute
```

Runtime becomes significantly simpler.

---

# Planner Responsibilities

The planner is responsible for:

- Entity resolution
- Relationship resolution
- Projection analysis
- Join planning
- Graph planning
- Filter normalization
- Ordering
- Pagination
- Aggregation planning
- Alias generation

The planner never executes SQL.

---

# Runtime Responsibilities

Runtime receives a completed plan.

Runtime performs:

```
QueryPlan

↓

SQL Generation

↓

Execution

↓

Materialization
```

Runtime never revisits planning decisions.

---

# QueryPlan

The QueryPlan is an immutable contract.

Example:

```text
QueryPlan

├── Root Entity
├── Projection
├── Filters
├── Ordering
├── Pagination
├── Graph
├── Joins
└── Result Shape
```

Everything Runtime needs already exists.

---

# Root Resolution

Planning begins by locating the root entity.

Example:

```
Customer

↓

EntityMetadata
```

Only one root exists per query plan.

---

# Projection Analysis

Projection determines exactly which fields are required.

Example:

```
Customer

↓

Id

Name

Address.City
```

Unused fields should never be planned.

---

# Projection Tree

Internally the planner builds a projection tree.

```
Customer

├── Id

├── Name

└── Address

    ├── City

    └── Country
```

The tree drives SQL generation and materialization.

---

# Metadata Resolution

Every projected member resolves through metadata.

```
Projection

↓

EntityMetadata

↓

ColumnMetadata
```

Reflection is never used.

---

# Relationship Resolution

Relationships become traversal instructions.

```
Customer

↓

Orders

↓

OrderItems
```

Relationships are resolved once during planning.

---

# Join Planning

The planner determines required joins.

```
Customer

↓

INNER JOIN Orders

↓

LEFT JOIN Address
```

Join selection belongs entirely to planning.

---

# Join Ordering

Join order should be deterministic.

The same request should always produce the same join sequence.

Stable ordering simplifies:

- Testing
- SQL snapshots
- Performance analysis

---

# Alias Allocation

The planner allocates aliases.

Example:

```
Customer

↓

c0

Orders

↓

o1

Address

↓

a2
```

Runtime simply emits aliases.

---

# Filter Planning

Filters become immutable expressions.

```
Name == "Bob"

↓

FilterNode
```

SQL writers translate expression nodes.

---

# Ordering

Ordering instructions are normalized.

```
Name ASC

Created DESC
```

Runtime serializes ordering.

---

# Pagination

Pagination is represented structurally.

```
Skip

Take
```

Providers translate pagination syntax.

---

# Aggregation

Aggregations become explicit nodes.

Examples:

```
COUNT

SUM

AVG

MIN

MAX
```

Planning determines semantics.

Providers determine syntax.

---

# Graph Planning

Graph planning builds traversal instructions.

```
Customer

↓

Orders

↓

Items

↓

Products
```

Traversal becomes part of the QueryPlan.

---

# Result Shape

The planner records the expected result shape.

Examples:

```
Single

Collection

Hierarchy

Scalar

Aggregate
```

Materializers use this information.

---

# Immutable Planning

Every planning object should be immutable.

```
ProjectionNode

FilterNode

JoinNode

GraphNode
```

Mutable planners produce immutable plans.

---

# Internal Planning Model

The planner may use mutable builders internally.

Example:

```
Builder

↓

Immutable QueryPlan
```

Mutation ends before Runtime begins.

---

# Validation

Planning validates requests.

Examples:

- Unknown fields
- Invalid joins
- Circular traversals
- Unsupported projections
- Duplicate aliases

Invalid plans should never reach Runtime.

---

# Determinism

Planning must be deterministic.

The same request should always produce:

- Same aliases
- Same joins
- Same ordering
- Same SQL structure

This is critical for snapshot testing.

---

# Performance

Planning occurs once per request.

Optimize for:

- Metadata lookup
- Graph traversal
- Allocation reduction
- Alias generation

Planning should never dominate execution time.

---

# QueryPlan Lifecycle

```
Created

↓

Validated

↓

Frozen

↓

Executed

↓

Discarded
```

Plans should never be modified after construction.

---

# SQL Generation Boundary

Planning ends here:

```
QueryPlan
```

SQL generation begins here:

```
QueryPlan

↓

SqlWriter
```

The SQL writer must never revisit planning decisions.

---

# Materialization Boundary

Materializers consume:

```
Projection

↓

Rows

↓

Objects
```

They do not inspect metadata.

---

# Testing

Planning should be tested independently.

Recommended tests:

```
Projection Tests

↓

Join Tests

↓

Filter Tests

↓

Graph Tests

↓

Snapshot Tests
```

Runtime tests should assume planning is correct.

---

# Native AOT

Planning naturally supports Native AOT because it depends entirely on:

- Generated metadata
- Immutable models
- Static contracts

No runtime discovery is required.

---

# Future Evolution

Potential planner enhancements include:

- Cost estimation
- Provider-aware optimization
- Query normalization
- Plan caching
- Compile-time query validation
- Generated planners

Each enhancement should preserve Runtime simplicity.

---

# Planner Checklist

Before adding planning logic, ask:

- Is this structural?
- Can Runtime avoid this work?
- Is the result immutable?
- Is it deterministic?
- Can providers consume it directly?
- Can it be tested independently?

If not, reconsider where the responsibility belongs.

---

# Relationship to the Framework

The Query Planner forms the boundary between request interpretation and execution.

```
Transport

↓

Planner

↓

QueryPlan

↓

Runtime

↓

Provider

↓

Database
```

Every Runtime component depends upon the planner, while the planner depends upon generated metadata.

---

# Summary

The Query Planning Architecture transforms client requests into immutable execution plans by resolving entities, projections, relationships, joins, filters, graph traversals, and result shapes before execution begins.

This separation removes structural reasoning from Runtime, enabling deterministic execution, provider independence, transport independence, simpler SQL generation, efficient materialization, comprehensive testing, and full Native AOT compatibility.

# Mutation Planning Architecture

> Mutation Planning is responsible for transforming create, update, delete, upsert, connect, disconnect, and graph mutations into a deterministic execution graph. Unlike queries, mutations have ordering constraints, dependencies, transactional semantics, identity propagation, and conflict resolution. The Mutation Planner resolves these concerns before Runtime begins execution.

Runtime executes mutations.

The Mutation Planner understands mutations.

---

# Philosophy

Mutation planning follows one rule:

> **Determine every dependency before execution begins.**

Runtime should never discover ordering.

Runtime should never resolve dependencies.

Everything must already exist in the MutationPlan.

---

# High-Level Pipeline

```
Mutation Request

↓

Planner

↓

Metadata Resolution

↓

Dependency Analysis

↓

Graph Analysis

↓

Ordering

↓

MutationPlan
```

Planning finishes before execution starts.

---

# Why Mutation Planning Exists

Queries are read operations.

Mutations change state.

Changing state introduces additional complexity:

- Ordering
- Transactions
- Identity propagation
- Foreign keys
- Graph dependencies
- Conflict handling

The planner resolves all of these.

---

# Planner Responsibilities

The Mutation Planner is responsible for:

- Entity resolution
- Dependency analysis
- Identity propagation
- Lookup planning
- Upsert planning
- Graph mutation planning
- Conflict analysis
- Execution ordering
- Transaction boundaries

It never executes SQL.

---

# Runtime Responsibilities

Runtime receives a completed MutationPlan.

Runtime performs:

```
MutationPlan

↓

SQL Generation

↓

Execution

↓

Materialization
```

Runtime assumes the plan is valid.

---

# MutationPlan

A MutationPlan is immutable.

Example:

```
MutationPlan

├── Operations

├── Dependencies

├── Graph Operations

├── Lookups

├── Identity References

├── Execution Order

└── Transaction Scope
```

Everything required for execution is already known.

---

# Mutation Operations

Each mutation becomes an operation node.

Examples:

```
Insert

Update

Delete

Upsert

Lookup

Connect

Disconnect
```

Operations become vertices in an execution graph.

---

# Dependency Graph

Mutations naturally form a graph.

Example:

```
Customer

↓

Order

↓

OrderItem
```

OrderItem cannot execute before Order.

Order cannot execute before Customer.

The planner computes this graph.

---

# Dependency Resolution

Dependencies are explicit.

```
Row 0

↓

Row 4

↓

Row 8
```

Runtime never discovers dependency order.

---

# Identity Propagation

Generated identities become dependency references.

Example:

```
Customer.Id

↓

Order.CustomerId
```

Runtime copies values according to the plan.

It never searches for relationships.

---

# Reference Nodes

References are represented explicitly.

```
Reference

Source Row

↓

Target Row

↓

Target Column
```

References remain immutable.

---

# Lookup Planning

Lookups are planned separately.

Example:

```
Country

↓

Lookup

↓

CountryId
```

Runtime receives complete lookup instructions.

---

# Upsert Planning

Upserts require conflict analysis.

Planner determines:

- Conflict columns
- Update columns
- Insert columns
- Identity propagation

Runtime only serializes provider syntax.

---

# Graph Mutation Planning

Graph mutations extend dependency planning.

Example:

```
Customer

↓

Order

↓

OrderItem

↓

Product
```

Traversal order becomes execution order.

---

# Topological Ordering

Execution order is determined through topological sorting.

```
Dependencies

↓

Topological Sort

↓

Execution Sequence
```

Runtime executes sequentially.

---

# Cyclic Detection

Cycles must be detected during planning.

Example:

```
A

↓

B

↓

A
```

Planner reports diagnostics.

Runtime never receives cyclic plans.

---

# Conflict Resolution

Conflict behavior becomes metadata.

Examples:

```
Do Nothing

Update

Replace

Merge
```

Providers translate conflict semantics.

---

# Transaction Planning

Planner determines transactional scope.

```
Entire Mutation

↓

Single Transaction
```

Or

```
Nested Savepoints
```

Runtime coordinates transactions.

---

# Graph Merge Planning

Graph merges become explicit operations.

Example:

```
Customer

↓

CustomerCustomerEdge

↓

Customer
```

Graph operations are independent from SQL generation.

---

# Execution Arms

Independent mutation branches can execute separately.

Example:

```
Customer

↓

Order A

↓

OrderItem A
```

```
Customer

↓

Order B

↓

OrderItem B
```

Planner identifies execution arms.

Future runtimes may parallelize them safely.

---

# Mutation Metadata

Planner consumes:

```
EntityMetadata

MutationMetadata

JoinMetadata

LookupMetadata
```

Runtime never performs metadata analysis.

---

# Alias Allocation

Every mutation node receives a deterministic identifier.

Example:

```
m0

m1

m2

m3
```

Identifiers remain stable.

---

# Parameter Planning

Planner identifies parameter sources.

Examples:

- Literal values
- Generated IDs
- Lookup IDs
- Dependency references

Runtime simply binds values.

---

# Immutable Mutation Graph

Planner builds mutable graphs internally.

Runtime receives immutable graphs.

```
Builder

↓

MutationGraph

↓

MutationPlan
```

Mutation ends before execution begins.

---

# Validation

Planning validates:

- Missing keys
- Invalid references
- Cycles
- Duplicate identities
- Missing lookup values
- Unsupported mutations

Invalid plans are rejected.

---

# Determinism

The same mutation always produces:

- Same node IDs
- Same dependency graph
- Same execution order
- Same SQL structure

Determinism greatly improves testing.

---

# SQL Boundary

Mutation planning ends at:

```
MutationPlan
```

SQL generation begins afterwards.

Providers should never perform dependency analysis.

---

# Runtime Execution

Runtime executes according to the graph.

```
Node

↓

Dependencies Satisfied?

↓

Execute

↓

Propagate Identity

↓

Continue
```

Execution follows the plan exactly.

---

# Materialization

Materialization occurs after execution.

Generated materializers reconstruct:

- Updated entities
- Inserted entities
- Lookup results

No planning occurs.

---

# Testing

Mutation planning should be tested independently.

Recommended tests:

```
Dependency Tests

↓

Identity Tests

↓

Lookup Tests

↓

Topological Order Tests

↓

Snapshot Tests
```

Runtime assumes planner correctness.

---

# Native AOT

Mutation planning naturally supports Native AOT because it relies entirely on generated metadata and immutable models.

No runtime discovery or reflection is required.

---

# Future Evolution

Potential enhancements include:

- Cost-based scheduling
- Parallel execution planning
- Distributed execution
- Generated mutation planners
- Bulk mutation optimization
- Provider-aware planning

Each enhancement should preserve Runtime simplicity.

---

# Mutation Planner Checklist

Before adding mutation logic, ask:

- Is this dependency structural?
- Can it be resolved before execution?
- Is execution order deterministic?
- Is the graph immutable?
- Can Runtime avoid this work?
- Can it be independently tested?

If not, reconsider the design.

---

# Relationship to the Framework

The Mutation Planner forms the boundary between mutation intent and mutation execution.

```
Transport

↓

Mutation Planner

↓

MutationPlan

↓

Runtime

↓

SQL Provider

↓

Database
```

Runtime becomes an execution engine rather than a mutation analyzer.

---

# Summary

The Mutation Planning Architecture transforms mutation requests into immutable dependency graphs by resolving entity relationships, identity propagation, lookup operations, graph traversals, conflict semantics, and execution ordering before Runtime begins.

This design enables deterministic execution, simplified Runtime logic, provider-independent SQL generation, reliable transactional behavior, comprehensive testing, and full Native AOT compatibility while supporting increasingly sophisticated graph mutation scenarios.

# Graph Execution Architecture

> Graph Execution is the subsystem responsible for executing object graphs rather than individual entities. Unlike traditional relational execution, graph execution understands nodes, edges, traversal paths, identity propagation, recursive relationships, and graph mutations. The planner constructs immutable graph plans, while Runtime executes those plans deterministically without rediscovering graph structure.

Graph execution extends relational execution.

It does not replace it.

---

# Philosophy

Graph execution follows one principle:

> **The graph is planned once and traversed many times.**

Traversal decisions belong to planning.

Traversal execution belongs to Runtime.

---

# High-Level Pipeline

```
Request

↓

Graph Planner

↓

GraphPlan

↓

Runtime

↓

Provider

↓

Materialization

↓

Object Graph
```

Graph planning finishes before Runtime begins.

---

# Why Graph Execution?

Relational execution retrieves rows.

Applications consume object graphs.

Example:

```
Customer

↓

Orders

↓

OrderItems

↓

Products
```

The Runtime should understand graph topology without inspecting CLR types.

---

# Graph Concepts

The graph model consists of:

```
Nodes

Edges

Paths

Roots

Traversals
```

These concepts are represented explicitly within the GraphPlan.

---

# GraphPlan

GraphPlan is immutable.

Example:

```
GraphPlan

├── Root

├── Nodes

├── Edges

├── Traversals

├── Identity Map

└── Result Shape
```

Runtime consumes the completed plan.

---

# Root Node

Every graph has exactly one root.

Example:

```
Customer
```

All traversals originate from the root.

---

# Graph Nodes

Nodes represent entities.

Examples:

```
Customer

Order

Invoice

Product
```

Nodes never contain execution behavior.

---

# Graph Edges

Edges describe relationships.

Examples:

```
Customer

↓

Orders
```

```
Order

↓

OrderItems
```

Edges remain immutable.

---

# Traversal Instructions

Traversal becomes explicit.

```
Root

↓

Edge

↓

Node

↓

Edge

↓

Node
```

Runtime follows traversal instructions.

---

# Relationship Resolution

Relationships are resolved during planning.

Example:

```
Customer

↓

Orders

↓

Items
```

Runtime never searches metadata for relationships.

---

# Recursive Graphs

Recursive structures are supported.

Example:

```
Category

↓

Children

↓

Children

↓

Children
```

Planning determines recursion strategy.

---

# Identity Map

Identity resolution prevents duplicate objects.

Example:

```
Customer 42

↓

Existing Instance
```

Instead of creating multiple objects.

The identity map belongs to Runtime.

---

# Graph Materialization

Rows become graph objects.

```
Rows

↓

Materializers

↓

Identity Resolution

↓

Relationship Wiring

↓

Object Graph
```

Materialization follows the GraphPlan.

---

# Node Ordering

Traversal order should be deterministic.

Example:

```
Customer

↓

Orders

↓

OrderItems
```

Stable ordering simplifies testing and caching.

---

# Breadth vs Depth

Traversal strategy is determined during planning.

Examples:

```
Depth First
```

or

```
Breadth First
```

Runtime simply executes.

---

# Graph Projection

Only requested nodes are planned.

Example:

```
Customer

↓

Orders
```

Products are omitted if not requested.

Planning minimizes execution.

---

# Cycles

Cycles must be detected during planning.

Example:

```
Employee

↓

Manager

↓

Employee
```

Runtime never performs cycle detection.

---

# Graph Mutations

Graph mutations use the same topology.

Example:

```
Customer

↓

Order

↓

OrderItem
```

Execution order follows dependency order.

---

# Graph Dependencies

Dependencies become traversal dependencies.

```
Parent

↓

Child
```

Identity propagation follows graph edges.

---

# Parallel Traversal

Independent graph branches may execute separately.

Example:

```
Customer

↓

Orders

↓

Invoices
```

Future runtimes may schedule branches independently.

---

# Graph Metadata

Planning consumes:

```
GraphMetadata

EntityMetadata

JoinMetadata
```

Runtime never performs graph discovery.

---

# Join Planning

Graph joins are resolved during planning.

```
Customer

↓

LEFT JOIN Orders

↓

LEFT JOIN Products
```

Providers simply serialize joins.

---

# Graph Filters

Filters become traversal filters.

Example:

```
Orders

WHERE

Status = Active
```

Filtering is represented structurally.

---

# Graph Aggregates

Aggregations are explicit.

Examples:

```
Order Count

Total Value

Maximum Price
```

Aggregation planning belongs to the planner.

---

# Graph Pagination

Pagination applies to graph collections.

Example:

```
Orders

Take 20
```

Providers implement pagination syntax.

---

# Materialization Boundary

Graph execution ends when rows arrive.

Materializers perform:

```
Identity Resolution

↓

Relationship Assembly

↓

Collection Population
```

Planning never resumes.

---

# Runtime Responsibilities

Runtime performs:

- SQL execution
- Identity management
- Relationship assembly
- Collection population
- Traversal execution

Runtime never analyzes graph structure.

---

# Determinism

The same request always produces:

- Same traversal
- Same aliases
- Same joins
- Same graph layout

Determinism simplifies debugging and snapshot testing.

---

# Performance

Performance depends on:

- Efficient traversal
- Identity reuse
- Allocation reduction
- Generated materializers
- Minimal joins

Graph execution should not repeatedly inspect metadata.

---

# Thread Safety

Graph metadata is immutable.

Execution state remains local to each request.

Identity maps should never be shared across requests.

---

# Testing

Graph execution should be tested separately.

Recommended tests:

```
Traversal Tests

↓

Identity Tests

↓

Relationship Tests

↓

Cycle Tests

↓

Snapshot Tests
```

Each graph scenario should remain deterministic.

---

# Native AOT

Graph execution naturally supports Native AOT because it relies entirely on:

- Generated metadata
- Immutable graph plans
- Generated materializers

No runtime reflection is required.

---

# Future Evolution

Potential future enhancements include:

- Recursive graph optimization
- Graph batching
- Distributed graph execution
- Provider-specific graph strategies
- Parallel traversal scheduling
- Graph caching

These enhancements should preserve the planning/execution separation.

---

# Graph Execution Checklist

Before introducing graph features, ask:

- Is traversal planned?
- Is the graph immutable?
- Can Runtime avoid graph discovery?
- Are identities deterministic?
- Can providers consume the plan directly?
- Can it be independently tested?

If not, reconsider the design.

---

# Relationship to the Framework

Graph execution extends the standard execution pipeline.

```
Transport

↓

Graph Planner

↓

GraphPlan

↓

Runtime

↓

SQL Provider

↓

Database

↓

Materializer

↓

Object Graph
```

Planning understands graphs.

Runtime executes graphs.

---

# Summary

The Graph Execution Architecture transforms graph-oriented requests into immutable traversal plans that describe nodes, edges, relationships, identity propagation, and result shapes before execution begins.

By separating graph analysis from graph execution, CoffeeBeanery delivers deterministic traversal, efficient materialization, provider independence, transport independence, simplified Runtime logic, and full Native AOT compatibility while supporting increasingly sophisticated object graph scenarios.

# SQL Generation Pipeline

> The SQL Generation Pipeline is responsible for translating immutable execution plans into deterministic, provider-specific SQL. Unlike traditional ORMs that build SQL while simultaneously reasoning about models, CoffeeBeanery separates planning from serialization. The SQL generator never analyzes application structure—it serializes an already completed execution plan.

The SQL Writer is a serializer.

It is not a planner.

---

# Philosophy

SQL generation follows one simple rule:

> **Never make a decision that the planner already made.**

Planning decides.

SQL generation serializes.

---

# High-Level Pipeline

```
QueryPlan

↓

SQL Writer

↓

Dialect

↓

Command Text

↓

Parameters
```

Every stage has a single responsibility.

---

# Separation of Concerns

Planning determines:

- Entities
- Joins
- Filters
- Ordering
- Graph traversal
- Aliases

SQL generation determines:

- SQL syntax
- Identifier quoting
- Parameter formatting
- Provider-specific features

---

# Pipeline Overview

```
Execution Plan

↓

Statement Builder

↓

Dialect

↓

Parameter Builder

↓

SQL Command
```

The writer never revisits planning.

---

# Inputs

The SQL Writer receives immutable inputs.

```
QueryPlan

MutationPlan

Metadata

Dialect
```

Nothing else is required.

---

# Outputs

The SQL Writer produces:

```
Command Text

↓

Parameter Collection
```

The output is ready for execution.

---

# SQL Writer

The SQL Writer coordinates serialization.

Responsibilities include:

- Statement construction
- Clause ordering
- Alias emission
- Delegating provider syntax

The writer should never inspect CLR models.

---

# SQL Dialect

The dialect owns provider-specific syntax.

Examples:

```
Identifier Quoting

Parameter Prefixes

Pagination

UPSERT Syntax

Returning Clauses
```

The writer delegates syntax decisions.

---

# Statement Builder

Statements are built incrementally.

```
SELECT

↓

FROM

↓

JOIN

↓

WHERE

↓

GROUP BY

↓

ORDER BY

↓

LIMIT
```

Clause ordering should remain deterministic.

---

# SELECT Generation

Projection nodes become SELECT expressions.

Example:

```
Projection

↓

Column List
```

Only required columns are emitted.

---

# FROM Generation

The planner selects the root table.

The writer emits:

```
FROM Customer
```

No discovery occurs.

---

# JOIN Generation

Join instructions already exist.

```
JoinNode

↓

LEFT JOIN

↓

INNER JOIN
```

The writer serializes join order exactly as planned.

---

# WHERE Generation

Filters become SQL predicates.

```
FilterNode

↓

WHERE
```

The SQL Writer never simplifies expressions.

---

# ORDER BY

Ordering instructions become SQL.

```
OrderingNode

↓

ORDER BY
```

The planner already determined sequence.

---

# Pagination

Pagination nodes become provider syntax.

Examples:

PostgreSQL

```
LIMIT

OFFSET
```

SQL Server

```
OFFSET

FETCH
```

The dialect owns syntax.

---

# Parameter Generation

Parameters are generated separately.

```
Filter

↓

Parameter Writer

↓

DbParameter[]
```

SQL text never embeds values.

---

# Alias Generation

Aliases originate during planning.

Example:

```
Customer

↓

c0
```

The SQL Writer preserves assigned aliases.

---

# Identifier Quoting

Identifier quoting belongs entirely to the dialect.

Examples:

PostgreSQL

```
"Customer"
```

SQL Server

```
[Customer]
```

MySQL

```
`Customer`
```

The writer never hardcodes quoting.

---

# Mutation SQL

Mutations follow the same architecture.

```
MutationPlan

↓

SQL Writer

↓

INSERT

UPDATE

DELETE

UPSERT
```

Execution order already exists.

---

# UPSERT Generation

Conflict handling belongs to the dialect.

Examples:

PostgreSQL

```
ON CONFLICT
```

SQL Server

```
MERGE
```

SQLite

```
INSERT OR REPLACE
```

The planner determines semantics.

---

# Graph SQL

Graph execution generates relational SQL.

Example:

```
GraphPlan

↓

JOIN Tree

↓

SQL
```

Traversal was already planned.

---

# CTE Generation

Common Table Expressions become explicit plan nodes.

```
MutationPlan

↓

CTE

↓

Statement
```

CTEs remain provider-specific.

---

# RETURNING Clauses

Providers supporting returning values may emit:

```
RETURNING *
```

or equivalent syntax.

Runtime consumes returned rows identically.

---

# Bulk Operations

Bulk execution becomes specialized SQL generation.

Example:

```
Rows

↓

VALUES (...)

↓

VALUES (...)

↓

VALUES (...)
```

Planning remains unchanged.

---

# SQL Formatting

Generated SQL should be:

- Deterministic
- Readable
- Predictable
- Stable

Readable SQL greatly simplifies debugging.

---

# Allocation Strategy

The writer should minimize allocations.

Prefer:

- Value builders
- Pooled buffers
- Span-based formatting
- Shared literals

Avoid repeated string concatenation.

---

# Error Handling

SQL generation should fail only for:

- Invalid plans
- Unsupported provider features
- Internal bugs

User validation belongs to planning.

---

# Determinism

The same plan always generates:

- Same SQL
- Same aliases
- Same parameter order
- Same formatting

Deterministic SQL enables snapshot testing.

---

# Snapshot Testing

Every generated statement should be snapshot tested.

```
Execution Plan

↓

SQL

↓

Snapshot
```

Unexpected SQL changes become immediately visible.

---

# Performance

Optimize:

- String generation
- Parameter emission
- Identifier formatting
- Allocation count

Avoid optimizing planning inside the writer.

---

# Native AOT

SQL generation naturally supports Native AOT.

It relies entirely upon:

- Immutable plans
- Generated metadata
- Explicit providers

No reflection is required.

---

# Future Evolution

Potential future improvements include:

- Generated SQL serializers
- Provider-specific optimizers
- SQL caching
- Prepared statement generation
- Bulk execution optimizations
- Streaming SQL generation

Each enhancement should preserve planner independence.

---

# SQL Writer Checklist

Before adding SQL generation logic, ask:

- Was this decision already made by the planner?
- Is the output deterministic?
- Does this belong in the dialect?
- Can providers override it?
- Can it be snapshot tested?
- Is Runtime still unaware of SQL syntax?

If not, reconsider the implementation.

---

# Relationship to the Framework

The SQL Writer forms the boundary between logical execution and physical database execution.

```
Execution Plan

↓

SQL Writer

↓

Dialect

↓

SQL

↓

Database
```

The planner understands the application.

The SQL Writer understands the database.

---

# Summary

The SQL Generation Pipeline converts immutable execution plans into deterministic, provider-specific SQL by serializing precomputed planning decisions through dialect abstractions and parameter writers.

By separating planning from SQL serialization, CoffeeBeanery eliminates redundant runtime analysis, enables provider independence, produces stable and testable SQL output, supports advanced provider optimizations, and maintains full compatibility with Native AOT while keeping Runtime focused exclusively on execution.

# Materialization Pipeline

> The Materialization Pipeline transforms raw database rows into fully connected object graphs. Unlike traditional ORMs that rely on reflection, expression trees, or runtime code generation, CoffeeBeanery generates materializers during compilation. Runtime simply invokes generated methods to construct objects deterministically and efficiently.

Materialization is the final stage of execution.

It converts data into application objects.

---

# Philosophy

Materialization follows one principle:

> **Runtime should never discover how to construct an object.**

The compiler already knows.

Generated code performs construction directly.

---

# High-Level Pipeline

```
Database

↓

DbDataReader

↓

Generated Materializer

↓

Identity Resolution

↓

Relationship Assembly

↓

Objects
```

Each stage has one responsibility.

---

# Why Materialization Exists

Databases return rows.

Applications consume objects.

Materialization bridges the two worlds.

Without materialization:

```
Rows

↓

Application
```

With materialization:

```
Rows

↓

Objects

↓

Application
```

---

# Reflection-Free Design

Traditional ORMs often perform:

```
Reflection

↓

Property Lookup

↓

Object Creation
```

CoffeeBeanery performs:

```
Generated Code

↓

Object Creation
```

Reflection never occurs during execution.

---

# Responsibilities

The Materialization Pipeline is responsible for:

- Object creation
- Property assignment
- Null handling
- Identity resolution
- Relationship wiring
- Collection population

It is **not** responsible for:

- SQL generation
- Query planning
- Metadata discovery
- Graph analysis

---

# Generated Materializers

Each model receives generated code.

Example:

```
CustomerMaterializer

OrderMaterializer

ProductMaterializer
```

Materializers are ordinary C# methods.

---

# Materializer Registry

Generated materializers are registered at compile time.

```
GeneratedMaterializerRegistry

↓

Customer

↓

CustomerMaterializer
```

Runtime performs direct lookups.

---

# Materialization Flow

```
DbDataReader

↓

Read Columns

↓

Construct Object

↓

Assign Properties

↓

Return Object
```

No dynamic behavior exists.

---

# Column Access

Generated code performs direct ordinal access.

Example:

```text
reader.GetInt32(0)

reader.GetString(1)

reader.GetDateTime(2)
```

Column ordinals are determined during planning.

---

# Object Construction

Construction should remain explicit.

Example:

```
new Customer(...)
```

or

```
new Customer();

customer.Name = ...
```

Construction strategy is generated.

---

# Null Handling

Generated materializers emit null checks.

Example:

```
reader.IsDBNull(5)

↓

null

↓

Value
```

Null semantics remain explicit.

---

# Value Conversion

Simple conversions may occur.

Examples:

```
Database Value

↓

CLR Value
```

Conversions should be generated whenever possible.

---

# Identity Resolution

Runtime maintains an identity map.

Example:

```
Customer 42

↓

Existing Instance
```

Instead of creating duplicates.

Identity management belongs to Runtime.

---

# Identity Map

```
Primary Key

↓

Dictionary

↓

Object Instance
```

Identity maps remain request-scoped.

---

# Relationship Assembly

Relationships are connected after construction.

Example:

```
Customer

↓

Orders

↓

OrderItems
```

Relationship topology comes from the GraphPlan.

---

# Collection Population

Collections are populated incrementally.

```
Customer

↓

Orders.Add(...)
```

Collection behavior should remain deterministic.

---

# Graph Materialization

Graph materialization combines:

```
Rows

↓

Objects

↓

Identity Resolution

↓

Relationship Wiring

↓

Graph
```

Traversal decisions were already made during planning.

---

# Duplicate Rows

Joins naturally produce duplicate root rows.

Example:

```
Customer

Order A

Customer

Order B
```

Identity resolution prevents duplicate objects.

---

# Materialization Context

Runtime provides execution context.

Typical services include:

- Identity map
- Collection cache
- Graph state

Generated materializers remain stateless.

---

# Streaming

Materialization should support streaming.

```
Reader

↓

Object

↓

Yield
```

Objects should be produced incrementally whenever possible.

---

# Async Materialization

Execution remains asynchronous.

```
ReadAsync()

↓

Materialize()

↓

Return Object
```

Blocking operations should be avoided.

---

# Error Handling

Materialization failures generally indicate:

- Invalid metadata
- Provider bugs
- Type mismatches
- Corrupt data

Planning should eliminate structural errors beforehand.

---

# Determinism

The same row always produces:

- Same object type
- Same property assignments
- Same graph structure

Materialization should never depend on runtime reflection.

---

# Performance

Optimize:

- Column access
- Object allocation
- Identity lookups
- Collection insertion
- Null handling

Generated methods should outperform reflection-based approaches.

---

# Memory Usage

Materializers should allocate only:

- Object instances
- Required collections

Avoid:

- Reflection caches
- Dynamic dictionaries
- Property lookup tables

---

# Thread Safety

Generated materializers are stateless.

Identity maps remain local to each request.

Shared mutable state should never exist.

---

# Testing

Materialization should be tested independently.

Recommended tests:

```
Primitive Types

↓

Nullable Types

↓

Relationships

↓

Collections

↓

Identity Resolution

↓

Snapshot Tests
```

Every generated materializer should behave deterministically.

---

# Native AOT

Materialization is one of the primary reasons CoffeeBeanery supports Native AOT.

It eliminates:

- Reflection
- Expression trees
- Runtime code generation
- Dynamic emit

Everything is generated during compilation.

---

# Future Evolution

Potential enhancements include:

- Struct materializers
- Span-based readers
- SIMD optimizations
- Zero-allocation collection builders
- Provider-specific readers
- Generated constructor selection

Each enhancement should preserve deterministic execution.

---

# Materializer Checklist

Before adding materialization behavior, ask:

- Can this be generated?
- Is reflection avoided?
- Is the materializer stateless?
- Does Runtime remain simple?
- Can it be tested independently?
- Is Native AOT preserved?

If not, reconsider the implementation.

---

# Relationship to the Framework

The Materialization Pipeline forms the boundary between relational data and application objects.

```
Database

↓

DbDataReader

↓

Generated Materializer

↓

Identity Resolution

↓

Object Graph

↓

Application
```

Planning determines structure.

Execution retrieves rows.

Materialization constructs objects.

---

# Summary

The Materialization Pipeline transforms database rows into fully connected object graphs through generated, reflection-free materializers that perform deterministic object construction, identity resolution, and relationship assembly.

By moving construction logic to compile time, CoffeeBeanery minimizes runtime overhead, simplifies execution, improves performance, enables comprehensive testing, and delivers full Native AOT compatibility while maintaining a clean separation between planning, execution, and object creation.

# Incremental Generator Pipeline

> The Incremental Generator Pipeline is the compile-time engine that transforms application source code into immutable framework artifacts. Rather than repeatedly analyzing the entire solution, the generator incrementally processes only the portions of the codebase that have changed, dramatically reducing compilation cost while maintaining deterministic output.

The pipeline is responsible for converting source code into executable knowledge.

---

# Philosophy

The generator follows one rule:

> **Every stage transforms immutable data into more useful immutable data.**

No stage should perform unnecessary work.

No stage should repeat previous analysis.

---

# Why Incremental Generation?

Traditional generators often perform:

```
Entire Compilation

↓

Analyze Everything

↓

Generate Everything
```

Incremental generators perform:

```
Changed File

↓

Affected Model

↓

Affected Metadata

↓

Affected Output
```

Only changed inputs invalidate downstream stages.

---

# High-Level Pipeline

```
Roslyn Compilation

↓

Syntax Discovery

↓

Semantic Analysis

↓

Internal Models

↓

Validation

↓

Metadata

↓

Planning Models

↓

Emitters

↓

Generated Source
```

Each stage has one responsibility.

---

# Roslyn Integration

CoffeeBeanery uses:

```
IIncrementalGenerator
```

rather than the legacy:

```
ISourceGenerator
```

Incremental generators provide caching, equality tracking, and efficient recomputation.

---

# Pipeline Stages

The complete pipeline consists of:

```
Syntax

↓

Symbols

↓

Internal Models

↓

Validation

↓

Metadata

↓

Planning Models

↓

Code Generation
```

Every stage is deterministic.

---

# Stage 1 — Syntax Discovery

The generator begins by locating relevant syntax.

Examples:

```
Classes

Records

Attributes

Enums

Interfaces
```

Syntax alone does not determine framework behavior.

---

# Stage 2 — Semantic Analysis

Syntax becomes symbols.

```
SyntaxNode

↓

INamedTypeSymbol
```

Semantic analysis resolves:

- Types
- Namespaces
- Attributes
- Generic arguments
- Accessibility

---

# Stage 3 — Internal Models

Roslyn symbols are immediately transformed.

```
INamedTypeSymbol

↓

EntityNode
```

Roslyn APIs should not leak beyond this stage.

---

# Why Internal Models?

Roslyn symbols are:

- Complex
- Expensive
- Compiler-specific
- Difficult to compare

Internal models are:

- Immutable
- Lightweight
- Testable
- Serializable
- Value comparable

---

# Stage 4 — Validation

Validation occurs before generation.

Examples:

- Duplicate entity names
- Missing identifiers
- Invalid relationships
- Unsupported types
- Circular references

Invalid models never reach emitters.

---

# Stage 5 — Metadata Models

Validated models become framework metadata.

Examples:

```
EntityMetadataModel

ColumnMetadataModel

JoinMetadataModel

GraphMetadataModel
```

These remain generator-only models.

---

# Stage 6 — Planning Models

Planning models describe execution structure.

Examples:

```
ProjectionModel

RelationshipModel

TraversalModel

MutationModel
```

These later become generated planners.

---

# Stage 7 — Emitters

Emitters transform models into C#.

```
MetadataEmitter

↓

GeneratedMetadata.cs
```

Each emitter owns one concern.

---

# Stage 8 — Generated Source

Generated source becomes part of the compilation.

```
Application

+

Generated Source

↓

Compilation
```

Consumers treat generated code as normal C#.

---

# Equality

Incremental generation depends upon equality.

Example:

```
Customer

↓

CustomerModel

↓

Equals()
```

If equality succeeds, downstream stages are skipped.

---

# Immutable Data Flow

Every stage should produce immutable values.

Preferred types:

```
record

ImmutableArray<T>

ImmutableDictionary<TKey,TValue>
```

Mutable state should remain local.

---

# Pipeline Dependencies

Stages only depend upon earlier stages.

```
Syntax

↓

Symbols

↓

Models

↓

Metadata
```

Reverse dependencies should never exist.

---

# Caching

Roslyn automatically caches stage outputs.

```
Customer.cs

↓

EntityModel

↓

Cached
```

Unchanged models avoid regeneration.

---

# Invalidations

Only dependent stages rerun.

Example:

```
Customer.cs changed

↓

Customer Metadata

↓

Customer Materializer
```

Order metadata remains cached.

---

# Parallelism

Independent branches execute concurrently.

```
Customer

↓

Metadata
```

```
Order

↓

Metadata
```

Generators should avoid shared mutable state.

---

# Diagnostics

Diagnostics originate during validation.

Examples:

```
CB1001

Duplicate Entity
```

```
CB1002

Missing Primary Key
```

Diagnostics should appear before generation.

---

# Emitters

Recommended emitter organization:

```
Emit/

Metadata

Ids

Materializers

Planners

Registries

DependencyInjection

Diagnostics
```

Each emitter generates one artifact family.

---

# Generated File Layout

Generated files should remain predictable.

Example:

```
Generated/

Metadata/

Planning/

Materializers/

Registries/
```

Stable organization improves debugging.

---

# Deterministic Output

The same source code should always produce:

- Same files
- Same identifiers
- Same ordering
- Same formatting

Deterministic output enables snapshot testing.

---

# Performance

Optimize:

- Equality
- Allocation count
- Symbol traversal
- Incremental invalidation
- String generation

Avoid repeated semantic analysis.

---

# Memory Management

Avoid storing Roslyn symbols after conversion.

Instead store:

```
EntityModel

ColumnModel

RelationshipModel
```

Memory pressure decreases significantly.

---

# Testing

Each stage should be independently testable.

Recommended layers:

```
Parser Tests

↓

Model Tests

↓

Validation Tests

↓

Emitter Tests

↓

Snapshot Tests

↓

Integration Tests
```

Failures become easier to isolate.

---

# Snapshot Testing

Generated source should always be snapshot tested.

```
Input

↓

Generated Source

↓

Snapshot
```

Unexpected changes become immediately visible.

---

# Native AOT

The generator is the foundation of Native AOT support.

It replaces runtime behavior with generated code including:

- Metadata
- Materializers
- Registries
- Dependency Injection
- Planners

Runtime remains static.

---

# Future Evolution

Potential future stages include:

```
Architecture Analysis

↓

Compile-Time SQL

↓

Security Metadata

↓

Authorization Models

↓

OpenAPI Generation

↓

Graph Visualization
```

Each stage should extend the pipeline without changing existing stages.

---

# Pipeline Checklist

Before introducing a new generation stage, ask:

- Is the input immutable?
- Is the output immutable?
- Is equality correctly implemented?
- Can Roslyn cache it?
- Can it be independently tested?
- Does it reduce Runtime complexity?

If not, reconsider the design.

---

# Relationship to the Framework

The Incremental Generator Pipeline connects developer code to Runtime.

```
Source Code

↓

Incremental Pipeline

↓

Generated Artifacts

↓

Runtime

↓

Execution
```

Every Runtime feature ultimately originates from this pipeline.

---

# Summary

The Incremental Generator Pipeline transforms source code into immutable metadata, planners, materializers, registries, diagnostics, and dependency injection code through a deterministic series of incremental compilation stages.

By leveraging Roslyn's incremental infrastructure, immutable internal models, value-based equality, modular emitters, and compile-time validation, CoffeeBeanery minimizes compilation cost, maximizes developer feedback, preserves deterministic output, and enables a lightweight Runtime that is fully compatible with Native AOT.

# Identifier Allocation System

> The Identifier Allocation System is responsible for assigning stable, deterministic identifiers to every generated artifact within CoffeeBeanery. These identifiers replace runtime string lookups with compact numeric values, enabling array indexing, faster execution, deterministic code generation, and stable incremental builds.

Identifiers are part of the framework's ABI (Application Binary Interface).

They should remain stable whenever possible.

---

# Philosophy

The identifier system follows one rule:

> **Everything important receives a deterministic numeric identity.**

Runtime should compare integers.

Not strings.

---

# Why Identifiers?

Without identifiers:

```
Runtime

↓

Dictionary<string,...>

↓

Lookup
```

With identifiers:

```
EntityId

↓

Metadata[]

↓

Direct Access
```

Array indexing replaces dictionary lookups.

---

# Design Goals

Identifiers should be:

- Stable
- Deterministic
- Compact
- Immutable
- Fast to compare
- Fast to serialize

Identifiers should never depend upon runtime state.

---

# What Receives an Identifier?

Nearly every generated artifact.

Examples:

```
Entities

Columns

Relationships

Graphs

Models

Joins

Mutations

Queries
```

Identifiers become the common language of Runtime.

---

# High-Level Architecture

```
Source Code

↓

Generator

↓

Identifier Allocation

↓

Generated Constants

↓

Runtime
```

Runtime never allocates identifiers.

---

# Identifier Types

Recommended identifier families:

```
EntityId

ColumnId

ModelId

JoinId

GraphId

MutationId

ProjectionId
```

Each family should remain independent.

---

# Numeric Representation

Identifiers should use the smallest practical type.

Typical choices:

```
byte

ushort

uint
```

Example:

```csharp
public const ushort Customer = 3;
```

Smaller identifiers improve cache locality.

---

# Generated Constants

Identifiers should be emitted as generated constants.

Example:

```csharp
public static class EntityId
{
    public const ushort Customer = 3;

    public const ushort Order = 4;
}
```

Generated constants improve readability.

---

# Metadata Indexing

Metadata becomes array indexed.

```
EntityId

↓

EntityMetadata[]
```

Instead of:

```
Dictionary<string,EntityMetadata>
```

---

# Relationship Indexing

Relationships also become numeric.

```
JoinId

↓

JoinMetadata[]
```

Runtime performs direct indexing.

---

# Stable Allocation

Identifiers should remain stable across builds whenever possible.

Adding:

```
Invoice
```

should not renumber unrelated entities.

Stable identifiers improve:

- Git history
- Snapshot testing
- Incremental builds
- Binary compatibility

---

# Allocation Strategy

Identifiers should be allocated after validation.

Pipeline:

```
Validated Models

↓

Sorting

↓

Allocation

↓

Generation
```

Validation prevents invalid identifiers.

---

# Deterministic Ordering

Ordering must never depend upon compiler enumeration.

Preferred ordering:

```
Namespace

↓

Entity Name

↓

Property Name
```

Stable ordering guarantees stable identifiers.

---

# Reserved Ranges

Future versions may reserve ranges.

Example:

```
0-999

Framework
```

```
1000+

Application
```

Reserved ranges simplify future expansion.

---

# Sparse vs Dense

Dense identifiers are preferred.

Example:

```
0

1

2

3

4
```

Dense indexing minimizes memory usage.

---

# Identifier Lookup

Runtime APIs should use identifiers.

Preferred:

```csharp
_metadata.GetEntity(EntityId.Customer);
```

Avoid:

```csharp
_metadata["Customer"]
```

Numeric APIs are faster and easier to validate.

---

# String Names

Strings remain useful for:

- Diagnostics
- Logging
- Debugging

Execution should rely upon numeric identifiers.

---

# Generated Registries

Registries use identifiers.

Example:

```
EntityId

↓

Materializer
```

```
EntityId

↓

Planner
```

Lookup becomes O(1).

---

# Dependency References

Mutation dependencies should reference identifiers.

Example:

```
Row 4

↓

EntityId.Customer

↓

ColumnId.Id
```

Identifiers eliminate repeated metadata searches.

---

# Graph References

Graph nodes should also use identifiers.

```
GraphNode

↓

EntityId

↓

JoinId
```

Traversal becomes compact.

---

# Serialization

Identifiers should serialize efficiently.

Example:

```
ushort

↓

2 bytes
```

Compact identifiers reduce memory and bandwidth.

---

# Equality

Identifiers compare by value.

Example:

```csharp
entityId == EntityId.Customer
```

Integer comparison is significantly cheaper than string comparison.

---

# Thread Safety

Generated identifiers are constants.

They require:

- No locks
- No synchronization
- No initialization

Constants are naturally thread-safe.

---

# Incremental Generation

Incremental generators should avoid reallocating identifiers unnecessarily.

Changing:

```
Order.cs
```

should not invalidate:

```
CustomerId
```

Stable allocation improves incremental performance.

---

# Diagnostics

Duplicate identifiers should never occur.

Generator diagnostics should detect:

- Duplicate IDs
- Missing IDs
- Overflow
- Invalid references

Runtime should assume identifier correctness.

---

# Testing

Recommended tests include:

```
Allocation Tests

↓

Ordering Tests

↓

Snapshot Tests

↓

Regression Tests
```

Identifier stability should be verified continuously.

---

# Native AOT

Identifiers are compile-time constants.

They require:

- No reflection
- No runtime initialization
- No dynamic lookup

They integrate naturally with Native AOT.

---

# Future Evolution

Potential future enhancements include:

- Provider-specific identifier maps
- Distributed identifier spaces
- Binary serialization tables
- Compressed metadata layouts
- Generated switch tables

Each enhancement should preserve deterministic allocation.

---

# Identifier Checklist

Before introducing a new identifier type, ask:

- Is it immutable?
- Is allocation deterministic?
- Can Runtime index arrays directly?
- Is it stable across builds?
- Can it be generated?
- Does it eliminate runtime lookup?

If not, reconsider the design.

---

# Relationship to the Framework

The Identifier Allocation System forms the bridge between generated metadata and efficient runtime execution.

```
Source Code

↓

Generator

↓

Identifier Allocation

↓

Generated Constants

↓

Metadata Arrays

↓

Runtime
```

Identifiers provide the common indexing mechanism shared across planners, metadata providers, SQL generation, graph execution, and materialization.

---

# Summary

The Identifier Allocation System assigns deterministic numeric identities to every significant generated artifact, replacing runtime string lookups with compact array indexing and constant-time access.

By generating stable identifiers during compilation, CoffeeBeanery improves execution performance, simplifies runtime APIs, enhances incremental generation, strengthens snapshot testing, and establishes a foundation for efficient metadata access and long-term binary compatibility across the framework.

# Diagnostics & Analyzer Architecture

> The Diagnostics subsystem is responsible for identifying architectural, modeling, and configuration issues during compilation rather than execution. Instead of allowing invalid applications to fail at runtime, CoffeeBeanery reports deterministic compiler diagnostics with actionable guidance, enabling developers to correct problems before the application is ever executed.

Diagnostics are part of the framework.

They are not an afterthought.

---

# Philosophy

Diagnostics follow one rule:

> **Every preventable runtime error should become a compile-time diagnostic.**

Compilation is the best opportunity to improve developer experience.

---

# Why Diagnostics?

Without diagnostics:

```
Compile

↓

Run

↓

Exception

↓

Debug
```

With diagnostics:

```
Compile

↓

Diagnostic

↓

Fix

↓

Run
```

Failures move left.

---

# High-Level Architecture

```
Source Code

↓

Parser

↓

Validation

↓

Diagnostics

↓

Generation
```

Invalid models never reach code generation.

---

# Responsibilities

The diagnostics subsystem is responsible for:

- Model validation
- Architecture validation
- Provider compatibility
- Metadata validation
- Graph validation
- Relationship validation
- Incremental diagnostics

Diagnostics never modify generated output.

---

# Diagnostic Lifecycle

Every diagnostic follows the same lifecycle.

```
Source

↓

Validation

↓

Diagnostic

↓

IDE

↓

Developer
```

Generation continues whenever possible.

---

# Diagnostic Categories

Diagnostics should be grouped by concern.

Examples:

```
Architecture

Metadata

Relationships

Planning

Providers

Generation

Performance
```

Each category should have a distinct identifier range.

---

# Identifier Convention

Diagnostic identifiers should remain stable.

Example:

```
CB1000

Architecture

CB2000

Metadata

CB3000

Relationships

CB4000

Planning

CB5000

Providers

CB9000

Internal Generator
```

Stable identifiers improve documentation and troubleshooting.

---

# Severity Levels

Diagnostics should clearly communicate severity.

```
Info

↓

Warning

↓

Error
```

Errors prevent generation.

Warnings allow generation.

---

# Error Philosophy

Errors indicate invalid applications.

Examples:

- Missing primary key
- Duplicate entity
- Circular dependency
- Invalid graph
- Unsupported mapping

Applications should not compile with structural errors.

---

# Warning Philosophy

Warnings indicate questionable designs.

Examples:

- Unused entity
- Redundant relationship
- Large projection
- Missing index recommendation
- Inefficient graph traversal

Warnings educate developers.

---

# Informational Diagnostics

Information diagnostics improve visibility.

Examples:

- Generated entity count
- Metadata statistics
- Incremental cache usage
- Optimization suggestions

Informational diagnostics should never block compilation.

---

# Validation Stages

Diagnostics may originate from multiple stages.

```
Syntax

↓

Semantic

↓

Model

↓

Metadata

↓

Planning
```

Each stage validates only its own responsibilities.

---

# Syntax Diagnostics

Examples include:

- Missing attributes
- Invalid declarations
- Unsupported modifiers

Syntax diagnostics occur before semantic analysis.

---

# Semantic Diagnostics

Examples:

- Unknown types
- Accessibility issues
- Generic misuse
- Invalid inheritance

Semantic analysis resolves compiler symbols.

---

# Model Diagnostics

Model validation includes:

- Duplicate entities
- Missing identifiers
- Invalid relationships
- Unsupported property types

Internal models should always be valid after this stage.

---

# Metadata Diagnostics

Metadata validation includes:

- Duplicate IDs
- Missing columns
- Invalid joins
- Graph inconsistencies

Runtime assumes metadata correctness.

---

# Planning Diagnostics

Planning validation includes:

- Cycles
- Invalid projections
- Ambiguous joins
- Unsupported filters

Invalid plans should never be generated.

---

# Provider Diagnostics

Providers may report compatibility issues.

Examples:

```
JSON not supported

Recursive CTE unavailable

Unsupported UPSERT strategy
```

Provider diagnostics should remain compile-time whenever possible.

---

# Analyzer Architecture

Analyzers should remain independent from generation.

Recommended structure:

```
Syntax Analyzer

Semantic Analyzer

Architecture Analyzer

Performance Analyzer

Provider Analyzer
```

Each analyzer owns one responsibility.

---

# Code Fixes

Many diagnostics should provide automatic fixes.

Examples:

```
Missing Attribute

↓

Add Attribute
```

```
Duplicate Identifier

↓

Generate New Identifier
```

Code fixes significantly improve developer experience.

---

# Diagnostic Messages

Messages should answer three questions:

1. What is wrong?
2. Why is it wrong?
3. How do I fix it?

Avoid vague diagnostics.

---

# Example Diagnostic

```
CB2004

Duplicate entity identifier.

The entity 'Customer' shares an identifier with
'Supplier'.

Assign unique identifiers or allow automatic
allocation.
```

The fix should be obvious.

---

# Diagnostic Location

Diagnostics should appear at the most relevant location.

Prefer:

```
Entity Declaration
```

Instead of:

```
Generated Code
```

Developers should never debug generated files.

---

# Incremental Diagnostics

Incremental generators should invalidate only affected diagnostics.

Changing:

```
Customer.cs
```

should not recompute diagnostics for unrelated entities.

---

# Performance Diagnostics

Future analyzers may detect:

- N+1 patterns
- Large projections
- Excessive joins
- Redundant graph traversals

Performance guidance belongs in the IDE.

---

# Architecture Diagnostics

Architectural analyzers may validate:

- Dependency direction
- Layer violations
- Provider boundaries
- Runtime dependencies

This helps preserve long-term architecture.

---

# Snapshot Testing

Diagnostics should be snapshot tested.

```
Input

↓

Diagnostics

↓

Snapshot
```

Changes become immediately visible during review.

---

# Documentation

Every diagnostic should have documentation.

Example:

```
CB3007

Relationship Cycle

Description

Example

Resolution

Related Diagnostics
```

Documentation should remain versioned.

---

# IDE Experience

Diagnostics should integrate naturally with:

- Visual Studio
- Rider
- VS Code

Developers should receive feedback while typing.

---

# Thread Safety

Analyzers should remain stateless.

All state should remain local to analysis.

Shared mutable state should be avoided.

---

# Native AOT

Diagnostics exist only during compilation.

They contribute nothing to runtime size or execution cost.

---

# Future Evolution

Potential future analyzers include:

- Security analyzer
- Authorization analyzer
- Migration analyzer
- SQL analyzer
- Query analyzer
- Graph optimization analyzer

Each analyzer should remain modular.

---

# Diagnostic Checklist

Before adding a new diagnostic, ask:

- Is this actionable?
- Can it be detected during compilation?
- Does it explain the fix?
- Is the identifier stable?
- Can it provide a code fix?
- Can it be independently tested?

If not, reconsider the design.

---

# Relationship to the Framework

Diagnostics surround the entire compile-time pipeline.

```
Source Code

↓

Analysis

↓

Diagnostics

↓

Generation

↓

Runtime
```

They improve the framework without increasing runtime complexity.

---

# Summary

The Diagnostics & Analyzer Architecture transforms structural, architectural, provider, and planning errors into clear compile-time diagnostics, allowing developers to correct issues before execution begins.

By combining incremental analyzers, deterministic validation, stable diagnostic identifiers, actionable messages, IDE integration, and optional code fixes, CoffeeBeanery delivers a modern developer experience while preserving a lightweight Runtime and strengthening the architectural integrity of the framework.

# Testing Architecture

> Testing in CoffeeBeanery is not a single activity performed at the end of development. Instead, every architectural layer is designed to be independently verifiable through deterministic tests. The framework emphasizes compile-time validation, immutable models, generated artifacts, and isolated responsibilities, enabling comprehensive testing without relying on complex integration environments.

Testing is part of the architecture.

Not merely validation.

---

# Philosophy

Testing follows one principle:

> **Every architectural layer should be testable in complete isolation.**

A failing test should identify one component.

Not an entire subsystem.

---

# Testing Goals

The testing strategy aims to provide:

- Fast feedback
- Deterministic results
- Layer isolation
- Snapshot stability
- High confidence
- Regression detection

Tests should explain failures rather than merely detect them.

---

# Testing Pyramid

CoffeeBeanery emphasizes many small tests.

```
Integration

──────────────

Component

──────────────

Unit

──────────────

Snapshot
```

Most tests should exist near the bottom.

---

# Architectural Layers

Every layer owns its own tests.

```
Parser

↓

Internal Models

↓

Validation

↓

Metadata

↓

Planning

↓

Generation

↓

Runtime

↓

Providers

↓

Transport
```

No layer should require all other layers to execute.

---

# Compile-Time Tests

Compile-time behavior should be tested extensively.

Examples:

- Model discovery
- Metadata generation
- Diagnostics
- Incremental generation
- Generated source

Compilation should be deterministic.

---

# Parser Tests

Parser tests validate Roslyn interpretation.

Example:

```
Source Code

↓

Parser

↓

EntityModel
```

The parser should never generate metadata.

---

# Model Tests

Internal models should verify:

- Equality
- Immutability
- Ordering
- Serialization
- Validation

Models form the foundation of the pipeline.

---

# Validation Tests

Validation should reject invalid applications.

Examples:

```
Duplicate Entity

Missing Key

Relationship Cycle

Invalid Mapping
```

Validation tests should not depend upon emitters.

---

# Metadata Tests

Metadata tests verify:

- Entity metadata
- Column metadata
- Join metadata
- Graph metadata
- Lookup metadata

Generated metadata should remain deterministic.

---

# Planning Tests

Planning tests verify:

- Projection planning
- Join planning
- Mutation planning
- Graph planning
- Dependency ordering

Planning should never require SQL generation.

---

# SQL Tests

SQL generation should be snapshot tested.

```
QueryPlan

↓

Generated SQL

↓

Snapshot
```

The same plan should always generate identical SQL.

---

# Materializer Tests

Generated materializers should verify:

- Primitive types
- Nullable types
- Collections
- Relationships
- Identity resolution

Materializers should not depend upon reflection.

---

# Runtime Tests

Runtime tests validate orchestration.

Examples:

- Execution pipeline
- Transaction flow
- Identity propagation
- Graph execution

Runtime assumes valid plans.

---

# Provider Tests

Every provider should have independent tests.

Examples:

```
Dialect

↓

SQL Writer

↓

Execution

↓

Integration
```

Providers should not require transport layers.

---

# Transport Tests

Transport providers verify:

- Request mapping
- Planner integration
- Response serialization
- Error translation

Runtime behavior is assumed correct.

---

# Integration Tests

Integration tests verify complete scenarios.

Example:

```
Request

↓

Planner

↓

Runtime

↓

Provider

↓

Database

↓

Materializer

↓

Response
```

Integration tests should remain relatively small.

---

# Snapshot Testing

Snapshot testing is fundamental.

Snapshot candidates include:

- Metadata
- Generated source
- SQL
- Diagnostics
- Execution plans

Unexpected changes become immediately visible.

---

# Deterministic Testing

Every test should produce identical results.

Avoid:

- Random ordering
- Current timestamps
- Machine-specific paths
- Reflection ordering

Deterministic tests improve confidence.

---

# Performance Tests

Performance tests validate architecture.

Measure:

- Compilation time
- SQL generation
- Materialization
- Allocation count
- Throughput

Microbenchmarks should complement functional tests.

---

# Benchmarking

Benchmarks belong outside unit tests.

Recommended scenarios:

```
Large Graph Query

Large Mutation

Metadata Lookup

Materialization

SQL Generation
```

Benchmarks should remain repeatable.

---

# Regression Tests

Every bug should introduce a regression test.

Workflow:

```
Bug

↓

Regression Test

↓

Fix

↓

Permanent Protection
```

The test suite should continuously improve.

---

# Native AOT Tests

Native AOT should have dedicated validation.

Examples:

- Successful publish
- Startup
- Query execution
- Mutation execution
- Materialization

Compatibility should remain continuously verified.

---

# Incremental Generator Tests

Incremental behavior should verify cache correctness.

Example:

```
Modify Customer.cs

↓

Customer Regenerated

↓

Order Cached
```

Incremental correctness is as important as generated correctness.

---

# Analyzer Tests

Diagnostics should verify:

- Identifier
- Severity
- Message
- Location
- Code fix

Diagnostics form part of the public API.

---

# Concurrency Tests

Concurrency tests verify:

- Thread safety
- Immutable metadata
- Parallel execution
- Shared registries

Mutable shared state should never exist.

---

# Stress Tests

Stress tests validate:

- Large schemas
- Deep graphs
- Massive mutations
- High concurrency
- Long-running execution

Architectural weaknesses often appear under stress.

---

# Continuous Integration

CI should execute:

```
Unit Tests

↓

Snapshot Tests

↓

Analyzer Tests

↓

Integration Tests

↓

Native AOT

↓

Benchmarks (optional)
```

Failures should stop releases.

---

# Code Coverage

Coverage is informative.

It should not become the primary quality metric.

Well-designed architectural tests are more valuable than high percentages.

---

# Test Organization

Recommended project layout:

```
tests/

Parser.Tests

Generator.Tests

Metadata.Tests

Planner.Tests

Runtime.Tests

Provider.Tests

Transport.Tests

Integration.Tests

Benchmarks
```

Projects should mirror architecture.

---

# Naming Convention

Test names should describe behavior.

Example:

```
MutationPlanner_Should_Order_Dependencies()

GraphPlanner_Should_Detect_Cycles()

MetadataProvider_Should_Return_Entity()
```

Names should explain failures.

---

# Future Evolution

Future testing improvements may include:

- Mutation fuzzing
- SQL fuzz testing
- Property-based testing
- Generated schema verification
- Cross-provider compatibility suites
- Distributed execution testing

Every addition should strengthen architectural confidence.

---

# Testing Checklist

Before adding a new framework feature, ask:

- Can it be unit tested?
- Can it be snapshot tested?
- Can it be benchmarked?
- Is it deterministic?
- Is it independently verifiable?
- Can regressions be isolated?

If not, reconsider the design.

---

# Relationship to the Framework

Testing surrounds every architectural layer.

```
Generator

↓

Metadata

↓

Planning

↓

Runtime

↓

Providers

↓

Transport
```

Each layer is validated independently while integration tests verify the complete pipeline.

---

# Summary

The CoffeeBeanery Testing Architecture is built around isolated responsibilities, deterministic behavior, immutable models, and comprehensive snapshot testing, ensuring every layer of the framework can be verified independently.

By combining unit tests, planner tests, metadata validation, SQL snapshots, generated code verification, provider integration tests, Native AOT validation, regression testing, and performance benchmarks, CoffeeBeanery achieves a testing strategy that scales with the architecture while preserving long-term reliability, maintainability, and developer confidence.

# Runtime Architecture

> Runtime is the execution engine of CoffeeBeanery. It consumes immutable execution plans, generated metadata, generated materializers, and provider implementations to execute requests deterministically. Runtime does not perform discovery, planning, reflection, or model analysis. Its sole responsibility is to execute instructions produced during compilation and planning.

Runtime is intentionally small.

It should resemble a CPU executing instructions rather than an ORM making decisions.

---

# Philosophy

Runtime follows one architectural principle:

> **Execution should require no architectural reasoning.**

Everything Runtime needs should already exist.

Planning has finished.

Generation has finished.

Validation has finished.

Runtime executes.

---

# High-Level Architecture

```
Transport

↓

Execution Plan

↓

Runtime

↓

Provider

↓

Database

↓

Materializer

↓

Response
```

Every execution follows this pipeline.

---

# Why a Runtime?

Most ORMs mix:

- Discovery
- Reflection
- Planning
- SQL generation
- Execution
- Materialization

CoffeeBeanery separates these concerns.

Runtime becomes significantly smaller.

---

# Runtime Responsibilities

Runtime is responsible for:

- Executing QueryPlans
- Executing MutationPlans
- Managing transactions
- Managing identity maps
- Coordinating providers
- Invoking generated materializers
- Returning results

Runtime never performs planning.

---

# Runtime Does NOT

Runtime should never:

- Inspect CLR types
- Scan assemblies
- Use reflection
- Build SQL semantics
- Discover relationships
- Allocate metadata
- Generate identifiers

Those responsibilities belong elsewhere.

---

# Runtime Inputs

Runtime consumes immutable inputs.

```
QueryPlan

MutationPlan

MetadataProvider

Provider

Materializers
```

Nothing is discovered dynamically.

---

# Runtime Outputs

Runtime produces:

```
Objects

Collections

Scalars

Mutation Results
```

Execution always produces application values.

---

# Internal Components

Runtime is composed of small services.

```
Execution Engine

↓

Transaction Manager

↓

Identity Manager

↓

Provider

↓

Materializers
```

Each service owns one responsibility.

---

# Execution Engine

The execution engine coordinates execution.

Example:

```
Plan

↓

Provider

↓

Reader

↓

Materializer

↓

Result
```

The engine orchestrates rather than analyzes.

---

# Query Execution

Query execution follows:

```
QueryPlan

↓

SQL Writer

↓

Provider

↓

Reader

↓

Materializer

↓

Result
```

Every step is deterministic.

---

# Mutation Execution

Mutation execution follows:

```
MutationPlan

↓

SQL Writer

↓

Transaction

↓

Execution

↓

Identity Propagation

↓

Result
```

Dependencies were already computed.

---

# Provider Coordination

Providers execute commands.

Runtime coordinates them.

```
Runtime

↓

Provider

↓

Database
```

Providers remain replaceable.

---

# Transaction Management

Runtime owns transactions.

Example:

```
Begin

↓

Execute

↓

Commit
```

Or

```
Rollback
```

Planning determines transaction boundaries.

---

# Identity Management

Runtime maintains request-scoped identity maps.

```
Primary Key

↓

Object Instance
```

Identity maps prevent duplicate objects.

---

# Materialization

Runtime invokes generated materializers.

```
Reader

↓

Generated Materializer

↓

Object
```

Runtime never constructs objects itself.

---

# Streaming

Runtime should support streaming execution.

```
Reader

↓

Object

↓

Yield
```

Streaming minimizes memory usage.

---

# Cancellation

Cancellation tokens propagate through execution.

```
Transport

↓

Runtime

↓

Provider

↓

Database
```

Cancellation should be cooperative.

---

# Error Handling

Runtime handles execution failures.

Examples:

- Provider exceptions
- Transaction failures
- Connection failures
- Timeouts

Structural errors should already have been prevented.

---

# Thread Safety

Runtime services should be stateless whenever possible.

Mutable state should remain request-scoped.

Examples:

```
Identity Map

Transaction

Execution Context
```

Singleton runtime components should remain immutable.

---

# Execution Context

Each request owns an execution context.

Example:

```
ExecutionContext

├── Transaction

├── Identity Map

├── Provider

├── Parameters

└── CancellationToken
```

Contexts never cross requests.

---

# Memory Model

Runtime should minimize allocations.

Preferred techniques:

- Immutable plans
- Generated code
- Pooled builders
- Array indexing
- Stack allocation where appropriate

Execution should allocate primarily for application objects.

---

# Performance

Runtime performance depends on:

- Metadata lookup
- SQL execution
- Materialization
- Identity resolution

Runtime should never waste time on planning activities.

---

# Determinism

Given the same:

- Plan
- Parameters
- Database state

Runtime should always perform the same execution sequence.

Deterministic execution simplifies debugging.

---

# Logging

Runtime may expose structured events.

Examples:

```
Query Started

SQL Generated

Execution Finished

Rows Read

Transaction Committed
```

Logging should not affect execution behavior.

---

# Diagnostics

Runtime diagnostics focus on execution.

Examples:

- Slow queries
- Transaction duration
- Materialization time
- Provider latency

Compile-time diagnostics belong to the generator.

---

# Provider Independence

Runtime depends only upon provider contracts.

```
IRuntimeProvider
```

Implementations may include:

- PostgreSQL
- SQL Server
- SQLite
- MySQL

Runtime remains unchanged.

---

# Native AOT

Runtime is designed for Native AOT.

It avoids:

- Reflection
- Dynamic code generation
- Expression trees
- Runtime discovery

Everything is explicit.

---

# Extensibility

Future runtime extensions may include:

- Query caching
- Distributed execution
- Parallel execution
- Metrics
- Tracing
- Resilience policies

Extensions should preserve Runtime simplicity.

---

# Testing

Runtime should be tested independently from:

- Generator
- Planner
- Metadata
- Providers

Mock providers should enable isolated execution tests.

---

# Runtime Checklist

Before adding Runtime functionality, ask:

- Is this execution rather than planning?
- Can it remain stateless?
- Does it avoid reflection?
- Does it preserve provider independence?
- Is it request-scoped if mutable?
- Can it be independently tested?

If not, reconsider whether it belongs in Runtime.

---

# Relationship to the Framework

Runtime is the center of execution.

```
Generator

↓

Metadata

↓

Planner

↓

Runtime

↓

Provider

↓

Database

↓

Materializer

↓

Application
```

Everything above Runtime prepares execution.

Everything below Runtime performs execution.

---

# Summary

The Runtime Architecture is a lightweight execution engine that coordinates providers, transactions, identity management, and generated materializers using immutable execution plans produced earlier in the pipeline.

By eliminating runtime discovery, reflection, and planning, CoffeeBeanery keeps Runtime deterministic, provider-independent, highly testable, Native AOT compatible, and focused exclusively on efficient execution.

# Provider Architecture

> Providers are the abstraction layer between the CoffeeBeanery Runtime and a specific persistence technology. They translate execution plans into provider-specific operations while preserving the semantics established during planning. Providers understand databases, transports, and protocols—but they never understand the application's domain model.

Providers encapsulate infrastructure.

They do not own business logic.

---

# Philosophy

Providers follow one rule:

> **The Runtime understands execution. Providers understand infrastructure.**

Responsibilities should never overlap.

---

# Why Providers?

Without providers:

```
Runtime

↓

PostgreSQL

↓

Execution
```

Supporting another database requires modifying Runtime.

With providers:

```
Runtime

↓

IProvider

↓

PostgreSQL

SQL Server

SQLite

MySQL
```

Runtime never changes.

---

# High-Level Architecture

```
Execution Plan

↓

Runtime

↓

Provider

↓

Infrastructure

↓

Results
```

Providers isolate infrastructure concerns.

---

# Provider Responsibilities

Providers are responsible for:

- SQL serialization
- Connection management
- Command execution
- Parameter binding
- Transaction integration
- Result streaming
- Provider-specific optimizations

Providers never perform planning.

---

# Provider Does NOT

Providers should never:

- Discover entities
- Analyze metadata
- Resolve joins
- Plan queries
- Allocate identifiers
- Build execution graphs
- Materialize objects

Those responsibilities belong elsewhere.

---

# Provider Contract

Runtime depends only upon interfaces.

Example:

```text
IProvider
```

Typical responsibilities include:

```
ExecuteQuery()

ExecuteMutation()

BeginTransaction()

Commit()

Rollback()
```

---

# SQL Providers

Relational providers generally follow:

```
Execution Plan

↓

SQL Writer

↓

Command

↓

Database
```

The provider coordinates execution.

---

# Provider Components

A provider is typically composed of:

```
Dialect

↓

Connection Factory

↓

Command Executor

↓

Parameter Binder

↓

Reader
```

Each component owns one responsibility.

---

# SQL Dialect

The dialect encapsulates syntax.

Examples:

```
Identifier Quoting

UPSERT

Pagination

Returning Clauses

JSON Functions
```

The Runtime remains syntax independent.

---

# Connection Management

Providers manage physical connections.

```
Open

↓

Execute

↓

Close
```

Connection pooling remains provider-specific.

---

# Command Execution

Execution follows a consistent flow.

```
SQL

↓

Parameters

↓

DbCommand

↓

ExecuteReader()
```

Providers expose results to Runtime.

---

# Parameter Binding

Providers bind values safely.

```
Execution Plan

↓

Parameters

↓

DbParameter
```

SQL injection protection belongs here.

---

# Transactions

Runtime requests transactions.

Providers implement them.

```
Runtime

↓

Provider

↓

DbTransaction
```

Nested transactions may map to savepoints.

---

# Result Streaming

Providers should stream rows whenever possible.

```
Reader

↓

Runtime

↓

Materializer
```

Large result sets should avoid buffering.

---

# Async Execution

Providers should support asynchronous APIs.

```
ExecuteReaderAsync()

ExecuteNonQueryAsync()

CommitAsync()
```

Blocking calls should be avoided.

---

# Error Translation

Providers translate infrastructure errors.

Examples:

```
Unique Constraint

↓

Provider Exception

↓

Framework Exception
```

Applications should not depend on database-specific exceptions.

---

# Provider Metadata

Providers may expose capabilities.

Examples:

```
Supports RETURNING

Supports MERGE

Supports JSON

Supports Recursive CTE
```

Planning may use these capabilities.

---

# Capability Negotiation

Capabilities should be explicit.

```
ProviderCapabilities

↓

Planner

↓

Execution Plan
```

Avoid runtime feature discovery.

---

# Performance

Providers optimize:

- Connection reuse
- Command reuse
- Parameter allocation
- Reader performance
- Network usage

Planning optimizations belong elsewhere.

---

# Determinism

Given identical:

- Plan
- Parameters
- Database

Providers should execute identical commands.

Deterministic providers simplify debugging.

---

# Thread Safety

Provider services should generally be stateless.

Mutable resources include:

- Connections
- Transactions
- Readers

These remain request-scoped.

---

# Provider Independence

The framework should never depend upon:

```
NpgsqlConnection

SqlConnection

MySqlConnection
```

Instead it depends upon:

```
IProvider
```

Concrete implementations remain isolated.

---

# Provider Testing

Every provider should pass the same test suite.

Example:

```
Shared Provider Tests

↓

PostgreSQL

SQL Server

SQLite

MySQL
```

Behavior should remain consistent.

---

# Provider Registration

Providers integrate through dependency injection.

```
Runtime

↓

IProvider

↓

Concrete Provider
```

Consumers should never reference provider implementations directly.

---

# Native AOT

Providers should remain compatible with Native AOT.

Avoid:

- Reflection
- Dynamic proxies
- Runtime code generation

Explicit implementations are preferred.

---

# Future Evolution

Future providers may include:

```
PostgreSQL

SQL Server

SQLite

MySQL

Oracle

Cosmos DB

MongoDB

REST

gRPC
```

The provider model should support both relational and non-relational backends.

---

# Provider Checklist

Before implementing a provider, ask:

- Does it avoid planning?
- Does it expose infrastructure only?
- Is it stateless where possible?
- Does it preserve Runtime independence?
- Can it execute immutable plans?
- Can it pass the shared provider test suite?

If not, reconsider the implementation.

---

# Relationship to the Framework

Providers form the infrastructure boundary of CoffeeBeanery.

```
Generator

↓

Metadata

↓

Planner

↓

Runtime

↓

Provider

↓

Infrastructure
```

Runtime speaks in execution plans.

Providers speak in infrastructure protocols.

---

# Summary

The Provider Architecture isolates all infrastructure-specific behavior behind a stable execution contract, allowing Runtime to execute immutable plans without understanding SQL dialects, network protocols, or database implementations.

By separating execution from infrastructure, CoffeeBeanery achieves provider independence, deterministic behavior, improved testability, simplified Runtime design, and long-term extensibility across relational and non-relational data sources.

# Transport Architecture

> The Transport layer is responsible for translating external protocols into execution requests and translating execution results back into protocol-specific responses. Transport adapters expose CoffeeBeanery to the outside world while remaining completely independent from execution, planning, metadata, and persistence.

Transport is the boundary between clients and the framework.

It should never contain business logic.

---

# Philosophy

The Transport layer follows one principle:

> **Translate requests. Never execute them.**

Transport understands protocols.

Runtime understands execution.

---

# Why a Transport Layer?

Without transport separation:

```
HTTP

↓

Business Logic

↓

SQL
```

Protocols become tightly coupled to execution.

With CoffeeBeanery:

```
HTTP

↓

Transport

↓

Runtime

↓

Provider
```

Every layer owns a single responsibility.

---

# High-Level Architecture

```
Client

↓

Transport

↓

Planner

↓

Runtime

↓

Provider

↓

Database
```

Responses travel in the opposite direction.

---

# Responsibilities

Transport is responsible for:

- Request parsing
- Authentication integration
- Authorization integration
- Parameter extraction
- Request validation
- Result serialization
- Error translation

Transport never generates SQL.

---

# Transport Does NOT

Transport should never:

- Plan queries
- Execute queries
- Inspect metadata
- Materialize objects
- Manage transactions
- Resolve relationships

Those responsibilities belong to Runtime.

---

# Request Lifecycle

Every request follows the same flow.

```
Client

↓

Transport

↓

Execution Request

↓

Planner

↓

Runtime

↓

Result

↓

Transport

↓

Response
```

Execution remains protocol independent.

---

# Execution Request

Transport produces a protocol-neutral request.

Example:

```
ExecutionRequest

├── Operation

├── Parameters

├── Context

├── Claims

└── CancellationToken
```

Everything else belongs to Runtime.

---

# Execution Result

Runtime returns protocol-neutral results.

```
ExecutionResult

├── Data

├── Errors

├── Metadata

└── Diagnostics
```

Transport formats the response.

---

# HTTP Transport

HTTP adapters translate:

```
GET

POST

PUT

DELETE
```

into execution requests.

HTTP semantics remain outside Runtime.

---

# GraphQL Transport

GraphQL transport parses:

```
Document

↓

Operation

↓

Variables

↓

ExecutionRequest
```

Planning begins after translation.

---

# REST Transport

REST transport maps:

```
Route

↓

Operation

↓

ExecutionRequest
```

Resource routing belongs entirely to Transport.

---

# gRPC Transport

gRPC adapters convert:

```
Protobuf

↓

ExecutionRequest
```

Serialization remains protocol-specific.

---

# Messaging Transport

Future transports may include:

```
RabbitMQ

Azure Service Bus

Kafka
```

Messages become execution requests.

---

# Authentication

Authentication belongs to Transport.

Example:

```
JWT

↓

ClaimsPrincipal

↓

ExecutionContext
```

Runtime consumes identity information.

---

# Authorization

Transport may invoke authorization policies before execution.

Example:

```
User

↓

Policy

↓

Allow

↓

Execute
```

Execution plans remain unchanged.

---

# Model Binding

Transport binds protocol values.

Examples:

```
JSON

↓

CLR Values

↓

Execution Parameters
```

Runtime receives strongly typed values.

---

# Validation

Transport validates protocol correctness.

Examples:

- Invalid JSON
- Missing route values
- Malformed GraphQL
- Invalid headers

Business validation belongs elsewhere.

---

# Serialization

Transport serializes results.

Examples:

```
Objects

↓

JSON
```

```
Objects

↓

Protobuf
```

Serialization should never affect Runtime.

---

# Error Translation

Runtime errors become protocol responses.

Example:

```
Provider Exception

↓

Execution Error

↓

HTTP 500
```

Protocol semantics remain localized.

---

# Streaming

Streaming protocols should remain supported.

Examples:

```
IAsyncEnumerable

↓

Server Sent Events
```

or

```
gRPC Streams
```

Runtime exposes streaming independently of transport.

---

# Cancellation

Cancellation tokens originate at Transport.

```
HTTP Client

↓

CancellationToken

↓

Runtime
```

Execution should respond cooperatively.

---

# Logging

Transport may log:

- Requests
- Responses
- Duration
- Status codes

Execution logging belongs to Runtime.

---

# Metrics

Transport metrics include:

- Request duration
- Throughput
- Status codes
- Payload size

Execution metrics remain separate.

---

# Thread Safety

Transport services should remain stateless.

Request-specific state belongs to:

```
ExecutionContext
```

Singleton transports should remain immutable.

---

# Versioning

Transport adapters may expose versioning.

Examples:

```
v1

v2

v3
```

Runtime remains unaware of API versions.

---

# Extensibility

Future transport adapters may include:

- GraphQL
- REST
- gRPC
- SignalR
- WebSockets
- CLI
- Background Jobs

Every adapter should target the same Runtime.

---

# Testing

Transport should be tested independently.

Recommended tests:

```
Routing

↓

Binding

↓

Validation

↓

Serialization

↓

Error Translation
```

Execution should be mocked.

---

# Native AOT

Transport should remain compatible with Native AOT.

Avoid:

- Reflection-based model binding
- Dynamic endpoint generation
- Runtime proxy creation

Prefer generated or explicit mappings.

---

# Transport Checklist

Before adding transport functionality, ask:

- Is this protocol-specific?
- Does Runtime remain unaware?
- Can it be independently tested?
- Is execution protocol-neutral?
- Is request translation deterministic?
- Is serialization isolated?

If not, reconsider the implementation.

---

# Relationship to the Framework

Transport is the entry point into CoffeeBeanery.

```
Client

↓

Transport

↓

Planner

↓

Runtime

↓

Provider

↓

Database

↓

Materializer

↓

Transport

↓

Client
```

Transport speaks protocols.

The framework speaks execution.

---

# Summary

The Transport Architecture isolates protocol-specific concerns from execution by translating external requests into protocol-neutral execution requests and formatting execution results back into protocol responses.

By separating transport from planning, runtime, providers, and materialization, CoffeeBeanery enables multiple protocols—including GraphQL, REST, gRPC, messaging, and future transports—to share a single execution engine while remaining deterministic, highly testable, and fully compatible with Native AOT.

# Dependency Injection Architecture

> Dependency Injection (DI) is responsible for composing the CoffeeBeanery framework into a running application. Unlike traditional frameworks that rely heavily on reflection, assembly scanning, or runtime registration, CoffeeBeanery generates deterministic registrations during compilation. Runtime receives fully constructed services through explicit contracts, allowing startup to remain fast, predictable, and Native AOT compatible.

Dependency Injection composes the framework.

It does not implement the framework.

---

# Philosophy

Dependency Injection follows one architectural principle:

> **Construction is separate from execution.**

DI creates objects.

Runtime uses objects.

---

# Why Dependency Injection?

Without DI:

```
Runtime

↓

new SqlWriter()

↓

new Provider()

↓

new Metadata()
```

Every component owns its dependencies.

With DI:

```
Runtime

↓

Interfaces

↓

Container

↓

Implementations
```

Construction becomes centralized.

---

# High-Level Architecture

```
Generated Registrations

↓

Service Collection

↓

Service Provider

↓

Runtime

↓

Execution
```

Composition happens once.

Execution happens many times.

---

# Responsibilities

Dependency Injection is responsible for:

- Service registration
- Lifetime management
- Provider registration
- Runtime composition
- Generated registrations
- Optional application extensions

DI never performs execution.

---

# DI Does NOT

Dependency Injection should never:

- Execute queries
- Plan mutations
- Build metadata
- Generate SQL
- Materialize objects
- Allocate identifiers

Construction ends before execution begins.

---

# Composition Root

Every application should have one composition root.

Example:

```
Program.cs

↓

AddCoffeeBeanery()

↓

Build()
```

Composition should remain centralized.

---

# Generated Registration

The generator produces registrations.

Example:

```
GeneratedServiceRegistration.cs
```

Manual registration should remain minimal.

---

# Registration Pipeline

```
Generator

↓

Generated Registrations

↓

Application Startup

↓

Service Provider
```

No runtime scanning occurs.

---

# Service Categories

Typical service groups include:

```
Metadata

Planning

Runtime

Providers

Materializers

Diagnostics
```

Each category registers independently.

---

# Metadata Registration

Metadata is typically singleton.

```
IMetadataProvider

↓

GeneratedMetadataProvider
```

Metadata is immutable.

---

# Planner Registration

Planners are usually stateless.

Example:

```
IQueryPlanner

↓

QueryPlanner
```

Singleton registration is preferred.

---

# Runtime Registration

Runtime services coordinate execution.

Example:

```
IRuntime

↓

Runtime
```

The Runtime itself should remain stateless.

---

# Provider Registration

Providers are infrastructure services.

Example:

```
IProvider

↓

PostgresProvider
```

Applications choose providers explicitly.

---

# Materializer Registration

Generated materializers register automatically.

```
EntityId

↓

Materializer
```

Developers should never register them manually.

---

# Registry Registration

Generated registries include:

```
Metadata Registry

Materializer Registry

Planner Registry

Identifier Registry
```

Registries become singleton services.

---

# Lifetime Guidelines

Recommended lifetimes:

Singleton

- Metadata
- Registries
- Planners
- SQL Writers
- Dialects

Scoped

- Execution Context
- Transactions
- Identity Maps

Transient

- Rare helper objects only

---

# Request Scope

Each execution receives a scoped context.

```
Request

↓

ExecutionContext

↓

Runtime
```

Execution state never escapes its scope.

---

# Thread Safety

Singleton services should be:

- Immutable
- Stateless
- Lock free

Mutable state belongs to scoped services.

---

# Generated Extensions

Applications consume generated extensions.

Example:

```csharp
services.AddCoffeeBeanery();
```

The generator expands this method.

---

# Provider Extensions

Providers extend registration.

Example:

```csharp
services.AddPostgres();
```

Infrastructure remains modular.

---

# Optional Features

Optional modules register independently.

Examples:

```
Caching

Tracing

Metrics

Authorization
```

Features should never modify Runtime.

---

# Validation

Startup validation may verify:

- Missing providers
- Duplicate registrations
- Invalid configuration
- Missing metadata

Failures should occur before serving requests.

---

# Startup Performance

Startup should perform:

- Registration
- Validation
- Container build

It should never:

- Scan assemblies
- Discover models
- Generate metadata
- Compile expressions

Everything should already exist.

---

# Reflection-Free Registration

Prefer generated code:

```
services.AddSingleton<IMetadataProvider,
GeneratedMetadataProvider>();
```

Avoid:

```
Assembly.GetTypes()
```

Reflection-free startup is essential for Native AOT.

---

# Extensibility

Applications may add:

- Custom providers
- Custom transports
- Custom planners
- Diagnostics
- Middleware

Extensions should depend upon contracts.

---

# Testing

Dependency Injection should be testable.

Recommended tests:

```
Registration Tests

↓

Lifetime Tests

↓

Composition Tests

↓

Startup Validation
```

Execution should remain mocked.

---

# Native AOT

Generated registrations are one of the key reasons CoffeeBeanery supports Native AOT.

They eliminate:

- Assembly scanning
- Reflection registration
- Dynamic activation
- Runtime discovery

Startup becomes deterministic.

---

# Future Evolution

Future registration features may include:

- Generated service graphs
- Startup analyzers
- Compile-time validation
- Provider auto-generation
- Feature packs

Every enhancement should preserve explicit composition.

---

# DI Checklist

Before registering a service, ask:

- Is the lifetime correct?
- Can it remain stateless?
- Can registration be generated?
- Is reflection avoided?
- Does Runtime remain unaware?
- Can it be tested independently?

If not, reconsider the registration.

---

# Relationship to the Framework

Dependency Injection composes every framework subsystem before execution begins.

```
Generator

↓

Generated Registrations

↓

Dependency Injection

↓

Runtime

↓

Provider

↓

Execution
```

Composition happens once.

Execution happens repeatedly.

---

# Summary

The Dependency Injection Architecture composes the CoffeeBeanery framework through generated, reflection-free registrations that construct immutable metadata providers, planners, runtimes, providers, registries, and supporting services before execution begins.

By separating construction from execution and generating deterministic registrations at compile time, CoffeeBeanery achieves fast startup, simplified composition, provider independence, excellent testability, and full Native AOT compatibility without relying on assembly scanning or runtime discovery.

# Framework Architecture

> The Framework Architecture describes how every subsystem within CoffeeBeanery collaborates to transform application source code into executable behavior. Rather than viewing Runtime, Generators, Metadata, Providers, and Transport as independent components, this document explains how they collectively form a layered architecture with strict dependency rules and well-defined responsibilities.

This document is the architectural map of the entire framework.

---

# Philosophy

CoffeeBeanery follows one architectural rule above all others:

> **Knowledge flows downward. Execution flows upward.**

Compile-time knowledge moves toward Runtime.

Execution results move back toward the application.

No layer should violate this direction.

---

# The Complete Architecture

```
                Application

                     │

         Domain Models / Attributes

                     │

──────────────────────────────────────────
          Compile-Time Boundary
──────────────────────────────────────────

        Incremental Generator

                     │

      Internal Models & Validation

                     │

          Metadata Generation

                     │

      Materializer Generation

                     │

        Planner Generation

                     │

     Generated Registrations

                     │

──────────────────────────────────────────
          Runtime Boundary
──────────────────────────────────────────

         Dependency Injection

                     │

              Metadata

                     │

             Query Planner

             Mutation Planner

                     │

               Runtime

                     │

              SQL Provider

                     │

              Database

                     │

          Generated Materializers

                     │

               Application
```

Everything above Runtime prepares execution.

Everything below Runtime performs execution.

---

# Architectural Layers

CoffeeBeanery is divided into six major layers.

```
Application

↓

Compilation

↓

Generated Artifacts

↓

Runtime

↓

Infrastructure

↓

Execution Results
```

Each layer owns exactly one responsibility.

---

# Layer 1 — Application

The application contains:

- Domain entities
- Models
- Attributes
- Configuration
- Business logic

Applications should never reference Runtime internals.

---

# Layer 2 — Compilation

Compilation transforms source code into knowledge.

Primary components include:

```
Roslyn

↓

Parser

↓

Validator

↓

Generator
```

Compilation should contain no execution logic.

---

# Layer 3 — Generated Artifacts

Generated artifacts include:

```
Metadata

Identifiers

Materializers

Planners

Registries

Dependency Injection

Diagnostics
```

Generated code becomes part of the application.

---

# Layer 4 — Runtime

Runtime coordinates execution.

Responsibilities include:

```
Execution

Transactions

Identity Maps

Provider Coordination

Materialization
```

Runtime never performs discovery.

---

# Layer 5 — Infrastructure

Infrastructure includes:

```
SQL Providers

Database Drivers

HTTP

GraphQL

gRPC

Messaging
```

Infrastructure understands protocols.

Not business models.

---

# Layer 6 — Results

Execution ultimately produces:

```
Objects

Collections

Graphs

Mutation Results

Scalars
```

Applications consume these values.

---

# Dependency Direction

Dependencies always move downward.

```
Application

↓

Generator

↓

Metadata

↓

Runtime

↓

Provider
```

Reverse dependencies should never exist.

---

# Compile-Time Boundary

Compile-time is responsible for:

- Discovery
- Validation
- Metadata
- Diagnostics
- Materializers
- Registration

Execution never crosses this boundary.

---

# Runtime Boundary

Runtime begins after startup.

Everything Runtime requires already exists.

```
Metadata

+

Plans

+

Providers

↓

Execution
```

No generation occurs after startup.

---

# Execution Boundary

Execution begins with an immutable plan.

```
QueryPlan

MutationPlan
```

Execution ends when objects are materialized.

---

# Information Flow

Knowledge flows in one direction.

```
Source Code

↓

Metadata

↓

Plans

↓

Execution

↓

Objects
```

Objects never modify metadata.

---

# Responsibility Matrix

| Layer | Responsibility |
|--------|----------------|
| Application | Business logic |
| Generator | Compilation |
| Metadata | Structural knowledge |
| Planner | Execution strategy |
| Runtime | Execution |
| Provider | Infrastructure |
| Materializer | Object construction |

Each layer should remain focused.

---

# Framework Principles

Every subsystem follows the same principles.

- Immutable data
- Deterministic behavior
- Explicit contracts
- Compile-time generation
- Runtime simplicity
- Provider independence

These principles reinforce one another.

---

# Compile-Time vs Runtime

Compile-time asks:

```
What exists?
```

Runtime asks:

```
What should execute?
```

The distinction is fundamental.

---

# Immutable Contracts

Subsystems communicate through immutable contracts.

Examples:

```
EntityMetadata

QueryPlan

MutationPlan

ExecutionContext
```

Mutable shared state should be avoided.

---

# Service Boundaries

Subsystems communicate only through interfaces.

Example:

```
Planner

↓

IRuntime

↓

IProvider
```

Implementations remain replaceable.

---

# Provider Independence

Providers remain isolated.

```
Runtime

↓

IProvider

↓

PostgreSQL
```

or

```
↓

SQL Server
```

or

```
↓

SQLite
```

Runtime never changes.

---

# Transport Independence

Transport adapters remain isolated.

```
GraphQL

↓

Runtime
```

```
REST

↓

Runtime
```

```
gRPC

↓

Runtime
```

Execution remains identical.

---

# Native AOT

Native AOT influences every architectural decision.

Compile-time replaces runtime whenever possible.

Examples:

- Metadata generation
- Materializer generation
- Registrations
- Identifiers
- Planners

Reflection is avoided by design.

---

# Performance Strategy

Performance comes from architecture.

Examples:

- Generated code
- Immutable metadata
- Array indexing
- Deterministic planning
- Reflection elimination
- Provider specialization

Performance should emerge naturally.

---

# Testing Strategy

Every layer is independently testable.

```
Parser

↓

Metadata

↓

Planner

↓

Runtime

↓

Provider
```

Integration tests verify collaboration.

---

# Evolution Strategy

Future framework evolution should preserve:

- Layer independence
- Deterministic generation
- Immutable contracts
- Provider abstraction
- Runtime simplicity

Features should extend architecture rather than complicate it.

---

# Architectural Constraints

The framework intentionally forbids:

- Runtime discovery
- Reflection-based execution
- Circular dependencies
- Hidden service location
- Dynamic metadata
- Mutable singleton state

These constraints improve long-term maintainability.

---

# Complete Lifecycle

The complete lifecycle of a CoffeeBeanery application is:

```
Developer writes code

↓

Incremental Generator

↓

Validation

↓

Metadata

↓

Identifiers

↓

Materializers

↓

Registrations

↓

Compilation

↓

Application Startup

↓

Dependency Injection

↓

Runtime

↓

Provider

↓

Database

↓

Generated Materializers

↓

Application Objects
```

Every subsystem contributes exactly once.

---

# Architectural Checklist

When adding a new subsystem, ask:

- Does it belong at compile-time or runtime?
- Does it preserve layer independence?
- Does it introduce reflection?
- Can it be generated?
- Is the contract immutable?
- Can it be independently tested?
- Does it improve Runtime rather than complicate it?

If not, reconsider its placement.

---

# Relationship to the Entire Framework

The Framework Architecture is the blueprint that connects every subsystem into a coherent whole.

```
Application

↓

Compilation

↓

Generation

↓

Metadata

↓

Planning

↓

Runtime

↓

Providers

↓

Infrastructure

↓

Materialization

↓

Application
```

Every document in this architecture guide describes one portion of this pipeline.

Together they describe the complete lifecycle of CoffeeBeanery.

---

# Summary

The CoffeeBeanery Framework Architecture organizes the system into a series of strictly layered, deterministic subsystems that transform source code into executable behavior through compile-time generation, immutable metadata, explicit planning, lightweight runtime orchestration, provider abstraction, and generated materialization.

By enforcing clear boundaries between compilation, planning, execution, infrastructure, and application code, the framework achieves high performance, excellent testability, provider independence, reflection-free execution, and full Native AOT compatibility while remaining understandable, maintainable, and extensible over time.

# Execution Lifecycle

> The Execution Lifecycle describes the complete journey of a request through the CoffeeBeanery framework, from the moment a client submits a request until fully materialized objects are returned. Every subsystem participates in this lifecycle exactly once, each with a clearly defined responsibility. Understanding this lifecycle provides the best high-level mental model of how the framework operates.

This is the "big picture" document.

---

# Philosophy

CoffeeBeanery follows a simple execution philosophy:

> **Discover once. Plan once. Execute many.**

Everything expensive happens before execution.

Execution itself should be almost mechanical.

---

# The Complete Lifecycle

```
Application Starts

↓

Generated Registrations

↓

Dependency Injection

↓

Runtime Ready

──────────────────────────────

Client Request

↓

Transport

↓

Planner

↓

Execution Plan

↓

Runtime

↓

Provider

↓

Database

↓

Rows

↓

Materializer

↓

Objects

↓

Transport

↓

Client Response
```

Every request follows this same sequence.

---

# Stage 1 — Application Startup

The application begins by constructing the framework.

```
Program.cs

↓

AddCoffeeBeanery()

↓

Build()
```

Startup performs composition only.

No planning occurs.

---

# Stage 2 — Generated Registration

Generated registrations wire together:

- Metadata
- Runtime
- Providers
- Materializers
- Registries

Everything required for execution becomes available.

---

# Stage 3 — Dependency Injection

The container constructs singleton services.

```
Metadata

↓

Planner

↓

Runtime

↓

Provider
```

After startup the framework is ready.

---

# Stage 4 — Client Request

A request enters through a transport.

Examples:

```
GraphQL

REST

gRPC

CLI
```

Transport understands protocols.

---

# Stage 5 — Request Translation

Transport converts protocol data into a framework request.

```
HTTP

↓

ExecutionRequest
```

Runtime never sees HTTP.

---

# Stage 6 — Planning

Planning transforms intent into instructions.

```
ExecutionRequest

↓

Metadata

↓

Execution Plan
```

Planning determines:

- Entities
- Joins
- Filters
- Traversals
- Ordering
- Dependencies

---

# Stage 7 — Plan Validation

Before execution begins the plan is validated.

Examples:

- Unknown entity
- Invalid traversal
- Unsupported mutation

Only valid plans execute.

---

# Stage 8 — Runtime Execution

Runtime receives:

```
Execution Plan
```

Nothing else.

Runtime never discovers application structure.

---

# Stage 9 — SQL Generation

The SQL Writer serializes the plan.

```
Execution Plan

↓

SQL
```

No planning decisions occur here.

---

# Stage 10 — Provider Execution

Providers execute SQL.

```
SQL

↓

DbCommand

↓

Database
```

Infrastructure concerns remain isolated.

---

# Stage 11 — Database Execution

The database performs:

- Filtering
- Joining
- Sorting
- Aggregation
- Mutation

CoffeeBeanery delegates relational execution to the database.

---

# Stage 12 — Result Streaming

Rows stream back through the provider.

```
Database

↓

Reader

↓

Runtime
```

Streaming minimizes memory usage.

---

# Stage 13 — Materialization

Generated materializers transform rows into objects.

```
Rows

↓

Objects
```

No reflection occurs.

---

# Stage 14 — Identity Resolution

Runtime ensures object uniqueness.

```
Primary Key

↓

Existing Instance
```

Duplicate graph nodes are eliminated.

---

# Stage 15 — Relationship Assembly

Collections and references become connected.

```
Customer

↓

Orders

↓

Items
```

The complete graph is assembled.

---

# Stage 16 — Transport Serialization

Transport converts objects into protocol output.

Examples:

```
Objects

↓

JSON
```

or

```
Objects

↓

GraphQL Result
```

Runtime remains protocol-independent.

---

# Stage 17 — Response

The client receives:

```
Response
```

Execution is complete.

---

# Compile-Time Participation

Before any request executes, compilation already produced:

```
Metadata

Identifiers

Materializers

Registries

Diagnostics

Generated Services
```

Runtime depends upon these artifacts.

---

# Runtime Participation

Runtime contributes:

- Execution
- Transactions
- Identity
- Coordination

Runtime never performs generation.

---

# Provider Participation

Providers contribute:

- SQL execution
- Parameter binding
- Connections
- Readers

Providers never perform planning.

---

# Materializer Participation

Materializers contribute:

- Construction
- Assignment
- Collections
- Graph assembly

Generated code performs all object creation.

---

# Responsibility Timeline

```
Compilation

████████████████

Planning

██████

Execution

████████

Materialization

██████

Serialization

███
```

Each subsystem participates briefly.

---

# Information Flow

```
Source Code

↓

Metadata

↓

Execution Plan

↓

SQL

↓

Rows

↓

Objects

↓

Response
```

Information only moves forward.

---

# Object Lifetime

```
Metadata

Application Lifetime
```

```
Execution Plan

Per Request
```

```
Execution Context

Per Request
```

```
Objects

Application Controlled
```

Understanding lifetimes simplifies architecture.

---

# Failure Points

Failures occur at different stages.

Compile-time:

- Diagnostics
- Validation

Runtime:

- Connection failures
- Provider failures
- Timeouts

Applications should rarely experience structural runtime failures.

---

# Thread Safety

Immutable objects are shared.

Mutable objects remain request scoped.

```
Metadata

Shared
```

```
Execution Context

Not Shared
```

---

# Native AOT

The lifecycle naturally supports Native AOT because every expensive operation occurs before execution.

Execution becomes:

```
Plans

↓

SQL

↓

Rows

↓

Objects
```

Nothing dynamic remains.

---

# Complete Lifecycle Diagram

```
Developer

↓

Generator

↓

Metadata

↓

Application Startup

↓

Dependency Injection

↓

Request

↓

Transport

↓

Planner

↓

Execution Plan

↓

Runtime

↓

Provider

↓

Database

↓

Rows

↓

Materializer

↓

Objects

↓

Transport

↓

Client
```

Every request follows this exact sequence.

---

# Lifecycle Checklist

For every new feature ask:

- Which lifecycle stage owns it?
- Can it move earlier?
- Can it become compile-time?
- Does Runtime remain simple?
- Does execution remain deterministic?
- Does it preserve layer boundaries?

If a feature belongs to multiple stages, it probably needs to be redesigned.

---

# Relationship to the Framework

The Execution Lifecycle is the thread that connects every subsystem documented in this architecture guide.

```
Generator

↓

Metadata

↓

Planner

↓

Runtime

↓

Provider

↓

Materializer

↓

Transport
```

Every subsystem contributes once.

No subsystem duplicates another's responsibility.

---

# Summary

The Execution Lifecycle describes the complete end-to-end operation of CoffeeBeanery, beginning with generated compile-time artifacts and ending with fully materialized application objects returned to the client.

By organizing execution into deterministic stages with immutable contracts and clearly defined responsibilities, CoffeeBeanery eliminates redundant work, simplifies Runtime, enables provider independence, supports Native AOT, and provides a framework architecture that remains understandable and scalable as new features are introduced.

# Compile-Time vs Runtime

> One of the defining characteristics of CoffeeBeanery is its strict separation between compile-time and runtime responsibilities. Rather than performing expensive discovery and analysis during execution, the framework shifts as much work as possible into compilation. This architectural boundary simplifies Runtime, improves performance, enables Native AOT, and creates deterministic execution.

This separation is the foundation of the framework.

---

# Philosophy

CoffeeBeanery follows one architectural rule:

> **If something can be known during compilation, Runtime should never discover it.**

Knowledge belongs to compile-time.

Execution belongs to Runtime.

---

# Why Separate Them?

Traditional frameworks often perform:

```
Application Starts

↓

Reflection

↓

Assembly Scanning

↓

Metadata Discovery

↓

Expression Compilation

↓

Execution
```

CoffeeBeanery performs:

```
Compilation

↓

Generated Artifacts

↓

Execution
```

Startup becomes dramatically simpler.

---

# The Boundary

```
Compile-Time

═══════════════════════

Runtime
```

Nothing crosses the boundary except immutable artifacts.

---

# Compile-Time Responsibilities

Compilation owns:

- Parsing
- Semantic analysis
- Validation
- Metadata generation
- Identifier allocation
- Materializer generation
- Planner generation
- Diagnostics
- Dependency Injection generation

Execution does not repeat this work.

---

# Runtime Responsibilities

Runtime owns:

- Executing plans
- Transactions
- Provider coordination
- Identity resolution
- Materialization
- Returning results

Runtime never analyzes application structure.

---

# High-Level Comparison

| Compile-Time | Runtime |
|--------------|----------|
| Discover | Execute |
| Validate | Coordinate |
| Generate | Materialize |
| Analyze | Stream |
| Allocate IDs | Manage Transactions |

Each side has a clearly defined purpose.

---

# Compile-Time Pipeline

```
Source Code

↓

Roslyn

↓

Internal Models

↓

Validation

↓

Metadata

↓

Identifiers

↓

Materializers

↓

Registries

↓

Generated Source
```

Everything becomes immutable.

---

# Runtime Pipeline

```
Execution Plan

↓

Provider

↓

Database

↓

Rows

↓

Materializer

↓

Objects
```

Execution becomes straightforward.

---

# Information Produced

Compile-time produces:

```
Metadata

Plans

Diagnostics

Identifiers

Materializers

Registries
```

Runtime consumes these artifacts.

---

# Information Consumed

Runtime consumes only immutable inputs.

```
Execution Plan

Metadata Provider

Materializers

Provider

Execution Context
```

Runtime never modifies metadata.

---

# Immutable Boundary

Everything crossing the boundary should be immutable.

Examples:

```
EntityMetadata

QueryPlan

MutationPlan

ColumnMetadata

Identifier Tables
```

Mutable compile-time state should never exist.

---

# Reflection

Reflection belongs almost entirely to compilation.

```
Roslyn Symbols

↓

Generated Code
```

Runtime should avoid:

```
Type.GetProperties()

Activator.CreateInstance()

Assembly.GetTypes()
```

Generated code replaces reflection.

---

# Metadata Discovery

Traditional ORMs discover metadata during startup.

CoffeeBeanery performs:

```
Compilation

↓

Generated Metadata
```

Runtime performs only lookups.

---

# Object Construction

Compile-time generates:

```
CustomerMaterializer
```

Runtime simply calls:

```
Materialize(reader)
```

Construction becomes explicit.

---

# SQL Planning

Compile-time prepares structural knowledge.

Runtime planning combines:

- Metadata
- Request
- Provider capabilities

Execution never infers entity relationships.

---

# Validation

Validation belongs entirely to compilation.

Examples:

- Missing identifiers
- Invalid relationships
- Unsupported mappings
- Duplicate entities

Runtime assumes correctness.

---

# Diagnostics

Compile-time reports:

```
CB2004

Duplicate Entity
```

Runtime reports:

```
Connection Timeout
```

Structural failures become compiler diagnostics.

---

# Startup

Application startup performs:

- Registration
- Validation
- Container construction

It should never perform:

- Metadata generation
- Reflection
- Expression compilation

Everything already exists.

---

# Execution

Execution begins only after startup completes.

```
Plan

↓

Runtime

↓

Provider

↓

Database
```

Runtime remains focused.

---

# Memory

Compile-time allocates:

- Internal models
- Roslyn symbols
- Generated strings

These disappear after compilation.

Runtime retains only immutable generated artifacts.

---

# Performance

Compile-time optimizes:

- Analysis
- Validation
- Code generation

Runtime optimizes:

- Execution
- Allocation
- Streaming
- Materialization

Each phase has different goals.

---

# Native AOT

Native AOT strongly benefits from this separation.

Compile-time replaces:

- Reflection
- Dynamic emit
- Runtime discovery

Execution becomes completely static.

---

# Determinism

Compilation should produce identical artifacts from identical source.

Runtime should produce identical execution from identical inputs.

Both phases remain deterministic.

---

# Testing

Compile-time tests verify:

- Metadata
- Diagnostics
- Generated source

Runtime tests verify:

- Execution
- Transactions
- Materialization

The two halves remain independently testable.

---

# Architectural Rules

Compile-time may depend upon:

- Roslyn
- Symbols
- Syntax trees

Runtime must never depend upon these APIs.

Likewise:

Runtime may depend upon:

- Providers
- Database drivers

Compilation should never reference provider implementations.

---

# Common Anti-Patterns

Avoid moving compile-time work into Runtime.

Examples:

```
Reflection

Assembly Scanning

Attribute Discovery

Metadata Construction

Identifier Allocation
```

These belong before execution begins.

---

# Future Evolution

Future compile-time features may include:

- Query precompilation
- Security analysis
- Schema visualization
- Migration generation
- Performance analyzers

Runtime should remain unchanged.

---

# Compile-Time Checklist

Before introducing new functionality, ask:

- Can this be generated?
- Can it become immutable?
- Does Runtime really need to know?
- Can validation occur during compilation?
- Can reflection be eliminated?

If yes, prefer compile-time.

---

# Runtime Checklist

Before adding Runtime functionality, ask:

- Is this execution?
- Does it coordinate rather than analyze?
- Does it avoid reflection?
- Can it consume generated artifacts?
- Does it preserve determinism?

If not, it probably belongs at compile-time.

---

# Relationship to the Framework

The compile-time/runtime boundary divides the entire architecture.

```
Application

↓

Compilation

↓

Generated Artifacts

═══════════════════════

Runtime

↓

Provider

↓

Database

↓

Materializer

↓

Application Objects
```

Everything above the boundary creates knowledge.

Everything below the boundary executes knowledge.

---

# Summary

The Compile-Time vs Runtime architecture is the central design principle of CoffeeBeanery, separating knowledge generation from execution through a strict immutable boundary.

By moving discovery, validation, metadata generation, identifier allocation, diagnostics, and materializer generation into compilation, the framework minimizes runtime complexity, improves startup performance, enables Native AOT, strengthens determinism, and creates a clear architectural model where Runtime focuses exclusively on efficient execution.

# Design Principles

> The Design Principles of CoffeeBeanery define the architectural values that guide every subsystem, every abstraction, and every implementation. These principles are not implementation details—they are the criteria used to evaluate every new feature, API, optimization, and architectural decision. They provide consistency across the entire framework and ensure that the architecture evolves without compromising its core philosophy.

These principles are the constitution of the framework.

They change rarely.

---

# The Fundamental Principle

Everything in CoffeeBeanery is derived from one statement:

> **Move knowledge as early as possible. Execute as late as possible.**

Compilation exists to create knowledge.

Runtime exists to execute knowledge.

---

# Principle 1 — Compile-Time First

If information is available during compilation:

Generate it.

Do not rediscover it later.

Examples:

- Metadata
- Identifiers
- Materializers
- Registrations
- Diagnostics

Compile-time work is preferable to runtime work.

---

# Principle 2 — Runtime Simplicity

Runtime should resemble an execution engine.

Not an analyzer.

Not an ORM.

Not a compiler.

Its responsibility is execution.

Nothing more.

---

# Principle 3 — Determinism

Identical input should always produce identical output.

Examples:

```
Source Code

↓

Generated Code
```

```
Execution Plan

↓

SQL
```

```
Rows

↓

Objects
```

Determinism simplifies testing, debugging, and maintenance.

---

# Principle 4 — Immutable Knowledge

Knowledge should become immutable once created.

Examples:

```
Metadata

Identifiers

Execution Plans

Registries
```

Immutable objects are easier to share, cache, and reason about.

---

# Principle 5 — Explicit Contracts

Subsystems communicate through explicit contracts.

Never hidden conventions.

Examples:

```
IProvider

IMetadataProvider

IQueryPlanner

IRuntime
```

Dependencies should always be visible.

---

# Principle 6 — Separation of Responsibilities

Each subsystem owns one responsibility.

Examples:

| Component | Responsibility |
|-----------|----------------|
| Generator | Knowledge creation |
| Planner | Execution strategy |
| Runtime | Execution |
| Provider | Infrastructure |
| Materializer | Object construction |

Responsibilities should never overlap.

---

# Principle 7 — Reflection Avoidance

Reflection should occur only where unavoidable.

Prefer:

```
Generated Code
```

Instead of:

```
Reflection

Dynamic Invocation

Expression Trees
```

Reflection-free execution enables Native AOT.

---

# Principle 8 — Generated Over Generic

If code can be generated, prefer generation over generic runtime abstractions.

Prefer:

```
GeneratedMaterializer()
```

Instead of:

```
ReflectionMaterializer()
```

Generated code is easier to optimize.

---

# Principle 9 — Layer Independence

Layers communicate downward.

```
Application

↓

Generator

↓

Runtime

↓

Provider
```

Reverse dependencies should never exist.

---

# Principle 10 — Provider Independence

Infrastructure should remain replaceable.

```
Runtime

↓

IProvider

↓

PostgreSQL
```

or

```
↓

SQL Server
```

Business logic should never depend upon a database implementation.

---

# Principle 11 — Protocol Independence

Execution should never depend upon transport.

```
REST

↓

Runtime
```

```
GraphQL

↓

Runtime
```

Execution remains identical.

---

# Principle 12 — Testability

Every subsystem should be independently testable.

Examples:

```
Metadata Tests

Planner Tests

Runtime Tests

Provider Tests
```

Large integration tests should be the exception.

---

# Principle 13 — Incrementality

Work should occur only when necessary.

Compilation:

```
Changed File

↓

Changed Metadata
```

Execution:

```
Requested Graph

↓

Requested Objects
```

Avoid unnecessary computation.

---

# Principle 14 — Native AOT Compatibility

Every architectural decision should consider Native AOT.

Avoid:

- Reflection
- Runtime emit
- Dynamic discovery
- Hidden dependencies

Prefer explicit generation.

---

# Principle 15 — Predictability

Developers should always know:

- What executes
- When it executes
- Why it executes

Hidden behavior should be minimized.

---

# Principle 16 — Performance Through Architecture

Performance should emerge naturally from architecture.

Not from micro-optimizations.

Examples:

- Compile-time generation
- Immutable metadata
- Array indexing
- Generated materializers

Well-designed architecture reduces optimization work.

---

# Principle 17 — Fail Early

Errors should appear as soon as possible.

Preferred order:

```
Compilation

↓

Startup

↓

Execution
```

Compile-time failures are the most valuable.

---

# Principle 18 — Small Focused Components

Large classes become difficult to understand.

Prefer:

```
Planner

Runtime

Provider

Materializer
```

Instead of:

```
MegaOrmEngine
```

Composition is preferable to accumulation.

---

# Principle 19 — Generated Code Is Public Architecture

Generated code should be:

- Readable
- Predictable
- Stable
- Debuggable

Developers should never fear generated files.

---

# Principle 20 — Evolution Without Breakage

The architecture should evolve through extension.

Not modification.

New features should preserve:

- Existing contracts
- Existing boundaries
- Existing responsibilities

Evolution should remain incremental.

---

# Principle Relationships

The principles reinforce one another.

```
Compile-Time

↓

Generation

↓

Immutability

↓

Determinism

↓

Simple Runtime

↓

Performance
```

Removing one principle weakens the others.

---

# Decision Framework

When evaluating a new feature, ask:

1. Can it move to compile-time?
2. Can it become immutable?
3. Does it preserve Runtime simplicity?
4. Can it be generated?
5. Does it introduce reflection?
6. Does it violate layer boundaries?
7. Can it be independently tested?
8. Does it preserve Native AOT?

If several answers are "no", reconsider the design.

---

# Architectural Trade-Offs

CoffeeBeanery intentionally prefers:

| Prefer | Instead Of |
|---------|------------|
| Generation | Reflection |
| Immutable models | Mutable state |
| Explicit APIs | Hidden conventions |
| Compilation | Runtime discovery |
| Composition | Inheritance |
| Determinism | Implicit behavior |
| Simplicity | Cleverness |

These trade-offs are deliberate.

---

# Relationship to the Framework

Every architectural document in CoffeeBeanery derives from these principles.

```
Design Principles

↓

Generator

↓

Metadata

↓

Planning

↓

Runtime

↓

Provider

↓

Materializer

↓

Transport
```

The principles guide every subsystem equally.

---

# Summary

The Design Principles define the architectural philosophy of CoffeeBeanery by emphasizing compile-time generation, immutable knowledge, deterministic execution, explicit contracts, provider independence, reflection-free runtime behavior, and strict separation of responsibilities.

Together, these principles create a framework that is performant, predictable, testable, maintainable, extensible, and fully compatible with Native AOT, ensuring that future evolution strengthens rather than compromises the architecture.

# Architectural Decision Records (ADRs)

> Architectural Decision Records (ADRs) capture the significant technical decisions made during the evolution of CoffeeBeanery. Unlike API documentation or implementation guides, ADRs explain *why* the framework is designed the way it is, what alternatives were considered, what trade-offs were accepted, and how those decisions influence future development.

Architecture is a series of decisions.

ADRs preserve the reasoning behind those decisions.

---

# Philosophy

Every long-lived framework accumulates technical decisions.

Without documentation:

```
Decision

↓

Time

↓

Forgotten Reason

↓

Accidental Regression
```

With ADRs:

```
Decision

↓

Recorded

↓

Preserved

↓

Future Contributors
```

Knowledge survives implementation.

---

# Why ADRs?

Source code explains:

> **How the framework works.**

ADRs explain:

> **Why the framework works that way.**

Both are equally important.

---

# What Belongs in an ADR?

An ADR documents decisions that significantly affect the architecture.

Examples include:

- Compile-time generation
- Runtime simplicity
- Provider abstraction
- Metadata model
- Materializer generation
- Native AOT strategy
- Dependency Injection design
- SQL planning strategy

Minor implementation details generally do not require ADRs.

---

# ADR Lifecycle

Every decision follows the same lifecycle.

```
Problem

↓

Alternatives

↓

Decision

↓

Consequences

↓

Implementation
```

Future developers should understand every stage.

---

# Recommended Structure

Every ADR should contain:

```
Title

Status

Context

Problem

Alternatives

Decision

Consequences

Related ADRs
```

Consistency improves readability.

---

# ADR Status

Typical statuses include:

```
Proposed

Accepted

Superseded

Deprecated

Rejected
```

Historical decisions should remain available.

---

# Context

The context explains:

- Existing architecture
- Technical constraints
- Business requirements
- Performance goals

Readers should understand the environment in which the decision was made.

---

# Problem Statement

Clearly define the problem.

Example:

```
Reflection-based metadata
creates startup overhead and
prevents Native AOT.
```

The problem should be objective.

---

# Alternatives

Document realistic alternatives.

Example:

```
Reflection

Expression Trees

Generated Metadata

Hybrid Model
```

Every option should be evaluated fairly.

---

# Decision

State the chosen solution explicitly.

Example:

```
Metadata will be generated
during compilation.
```

Avoid ambiguous language.

---

# Consequences

Every decision has trade-offs.

Positive:

- Faster startup
- Native AOT
- Simpler Runtime

Negative:

- Larger generated code
- More generator complexity

Architectural honesty builds trust.

---

# Relationships

ADRs often depend upon earlier decisions.

Example:

```
ADR-001

↓

ADR-007

↓

ADR-014
```

Decision history forms an architectural narrative.

---

# Example Timeline

```
ADR-001

Compile-Time Metadata
```

↓

```
ADR-004

Generated Materializers
```

↓

```
ADR-009

Runtime Simplification
```

↓

```
ADR-015

Provider Abstraction
```

Architecture evolves incrementally.

---

# Typical CoffeeBeanery ADRs

Examples might include:

```
ADR-001

Compile-Time Metadata
```

```
ADR-002

Generated Identifiers
```

```
ADR-003

Planner Architecture
```

```
ADR-004

Materializer Generation
```

```
ADR-005

Reflection-Free Runtime
```

```
ADR-006

Provider Model
```

---

# Decision Quality

A good ADR explains:

- Why now?
- Why this?
- Why not the alternatives?

Readers should rarely need additional context.

---

# Decision Stability

ADRs are historical records.

They should not be rewritten to match the current implementation.

Instead:

```
Old ADR

↓

Superseded ADR
```

History remains preserved.

---

# Implementation Independence

ADRs describe architecture.

They should avoid:

- Class names
- Method names
- Temporary implementation details

Implementation evolves faster than architecture.

---

# Architecture Governance

Large changes should introduce:

```
New Feature

↓

ADR

↓

Implementation
```

Architecture evolves deliberately.

---

# Code Reviews

Major pull requests should reference relevant ADRs.

Example:

```
Implements ADR-012
```

Reviewers immediately understand architectural intent.

---

# Documentation Relationships

ADRs complement:

- Architecture guides
- API documentation
- Design principles
- Contributor guides

Each serves a different purpose.

---

# Testing Decisions

Architectural decisions should influence tests.

Example:

```
ADR

↓

Requirement

↓

Regression Test
```

Tests protect architectural intent.

---

# Native AOT

Native AOT decisions deserve explicit ADRs.

Examples:

- Reflection avoidance
- Generated registrations
- Metadata generation
- Materializer generation

Future contributors should understand these constraints.

---

# Evolution

Future decisions may include:

- Distributed execution
- Query caching
- Provider capabilities
- Security model
- Schema evolution

Every significant decision deserves documentation.

---

# ADR Checklist

Before creating an ADR, ask:

- Does this affect architecture?
- Will future contributors ask "why"?
- Are multiple alternatives possible?
- Does it influence multiple components?
- Could reversing this decision be expensive?

If so, record it.

---

# Relationship to the Framework

ADRs document the evolution of CoffeeBeanery itself.

```
Design Principles

↓

Architectural Decisions

↓

Implementation

↓

Future Decisions
```

Architecture becomes a documented conversation rather than institutional knowledge.

---

# Summary

Architectural Decision Records preserve the reasoning behind the major design choices that shape CoffeeBeanery, documenting the context, alternatives, trade-offs, and long-term consequences of architectural evolution.

By treating architecture as a sequence of explicit, versioned decisions rather than undocumented intuition, CoffeeBeanery becomes easier to maintain, easier to evolve, and far more approachable for future contributors who need to understand not only *how* the framework works, but *why* it was built that way.

# Core Architectural Patterns

> CoffeeBeanery is built upon a small number of recurring architectural patterns. These patterns appear throughout the Generator, Metadata, Planner, Runtime, Providers, Materializers, and Transport layers. Rather than introducing unique mechanisms for every subsystem, the framework intentionally repeats the same architectural solutions whenever possible. This consistency makes the framework easier to understand, maintain, optimize, and extend.

Patterns are the vocabulary of the architecture.

Once understood, every subsystem becomes easier to reason about.

---

# Philosophy

CoffeeBeanery follows one guiding principle:

> **Prefer a small number of consistent patterns over many specialized solutions.**

Architectural consistency is more valuable than cleverness.

---

# Pattern 1 — Pipeline

Nearly every subsystem is implemented as a pipeline.

```
Input

↓

Transformation

↓

Output
```

Examples:

```
Source

↓

Metadata

↓

Generated Code
```

```
Execution Request

↓

Execution Plan

↓

SQL
```

Pipelines separate responsibilities naturally.

---

# Pattern 2 — Immutable Models

Information is represented using immutable models.

```
EntityModel

ColumnModel

JoinModel

QueryPlan
```

Immutable models eliminate synchronization problems and simplify testing.

---

# Pattern 3 — Compile-Time Generation

Whenever knowledge can be generated:

Generate it.

Examples:

```
Metadata

Identifiers

Materializers

Registries

Dependency Injection
```

Generation replaces runtime discovery.

---

# Pattern 4 — Explicit Registries

Runtime performs explicit lookups.

```
EntityId

↓

Metadata Registry

↓

Metadata
```

Avoid runtime discovery.

---

# Pattern 5 — Numeric Indexing

Strings are converted into numeric identifiers.

```
Customer

↓

EntityId

↓

Metadata[3]
```

Arrays replace dictionaries whenever practical.

---

# Pattern 6 — Strategy

Provider-specific behavior uses strategy interfaces.

```
Runtime

↓

IProvider

↓

PostgreSQL
```

The execution engine remains unchanged.

---

# Pattern 7 — Composition

Subsystems collaborate through composition.

```
Runtime

↓

Provider

↓

SQL Writer
```

Composition is preferred over inheritance.

---

# Pattern 8 — Stateless Services

Framework services should be stateless whenever possible.

Examples:

- Planners
- SQL Writers
- Metadata Providers
- Registries

State belongs to execution contexts.

---

# Pattern 9 — Request Scope

Mutable state remains request-scoped.

Examples:

```
Execution Context

Identity Map

Transaction

Parameters
```

Shared mutable state should never exist.

---

# Pattern 10 — Value Objects

Configuration and metadata should behave as value objects.

Examples:

```
ColumnReference

EntityReference

JoinReference
```

Equality should depend upon values.

---

# Pattern 11 — Layered Architecture

Every subsystem belongs to one architectural layer.

```
Generator

↓

Planner

↓

Runtime

↓

Provider
```

Dependencies move downward only.

---

# Pattern 12 — Dependency Inversion

Runtime depends upon abstractions.

```
IRuntimeProvider

IMetadataProvider

IPlanner
```

Concrete implementations remain replaceable.

---

# Pattern 13 — Separation of Read and Write

Queries and mutations are planned independently.

```
Query

↓

QueryPlan
```

```
Mutation

↓

MutationPlan
```

Each execution path remains optimized.

---

# Pattern 14 — Streaming

Large datasets should stream through the framework.

```
Reader

↓

Materializer

↓

Consumer
```

Buffering should be minimized.

---

# Pattern 15 — Builder

Complex immutable models may be created through builders.

```
Builder

↓

Immutable Model
```

Builders simplify construction while preserving immutability.

---

# Pattern 16 — Visitor

Tree-like structures may use visitors.

Examples:

```
Expression

↓

Visitor

↓

SQL
```

Visitors isolate traversal algorithms.

---

# Pattern 17 — Adapter

Transport layers adapt external protocols.

```
HTTP

↓

ExecutionRequest
```

```
GraphQL

↓

ExecutionRequest
```

Runtime remains protocol-independent.

---

# Pattern 18 — Factory

Factories create provider-specific infrastructure.

Examples:

```
Connection Factory

Command Factory

Parameter Factory
```

Construction remains centralized.

---

# Pattern 19 — Registry

Generated artifacts are organized into registries.

Examples:

```
Metadata Registry

Materializer Registry

Planner Registry
```

Registries avoid runtime discovery.

---

# Pattern 20 — Template Method

Execution pipelines follow fixed orchestration steps.

```
Plan

↓

Execute

↓

Materialize

↓

Return
```

Providers customize only infrastructure-specific operations.

---

# Pattern Relationships

These patterns reinforce one another.

```
Generation

↓

Registries

↓

Numeric IDs

↓

Array Lookup

↓

Fast Runtime
```

Architectural consistency compounds benefits.

---

# Pattern Selection

When introducing new functionality, prefer an existing pattern.

Ask:

- Can this become a pipeline?
- Can it be immutable?
- Can it be generated?
- Can it use a registry?
- Can it remain stateless?

New patterns should be introduced rarely.

---

# Anti-Patterns

Avoid introducing:

- Service locators
- Reflection-heavy execution
- Mutable global state
- Runtime assembly scanning
- God objects
- Circular dependencies
- Dynamic metadata

These conflict with the framework philosophy.

---

# Performance Implications

Most performance gains arise naturally from these patterns.

Examples:

- Immutable metadata improves caching.
- Numeric indexing removes dictionary lookups.
- Generation eliminates reflection.
- Stateless services improve scalability.
- Streaming reduces memory pressure.

Performance is a consequence of architecture.

---

# Native AOT

These patterns naturally support Native AOT.

Specifically:

- Explicit registries
- Generated code
- Immutable metadata
- Dependency inversion
- Reflection avoidance

No special runtime behavior is required.

---

# Future Evolution

Future framework features should reuse these patterns whenever possible.

Examples:

- Distributed execution
- Caching
- Security
- Schema evolution
- Event sourcing

Consistency should take precedence over novelty.

---

# Pattern Checklist

Before introducing a new architectural mechanism, ask:

- Does an existing pattern already solve this?
- Does it preserve immutability?
- Does it avoid reflection?
- Can it be generated?
- Does it fit an existing layer?
- Can it be independently tested?

If not, reconsider the design.

---

# Relationship to the Framework

These patterns appear throughout every CoffeeBeanery subsystem.

```
Generator

↓

Metadata

↓

Planner

↓

Runtime

↓

Provider

↓

Materializer

↓

Transport
```

Understanding these patterns makes the entire architecture easier to understand.

---

# Summary

The Core Architectural Patterns define the recurring design solutions used throughout CoffeeBeanery, including pipelines, immutable models, compile-time generation, explicit registries, numeric indexing, stateless services, layered architecture, dependency inversion, streaming, and provider abstraction.

By consistently applying a small set of well-defined patterns across every subsystem, CoffeeBeanery achieves a framework that is predictable, performant, maintainable, extensible, highly testable, and fully compatible with Native AOT while remaining conceptually simple despite its sophisticated capabilities.

# Internal Model Architecture

> Internal Models are the canonical representation of application structure inside the CoffeeBeanery Incremental Generator. They form the boundary between Roslyn and the remainder of the framework, allowing every subsequent stage—validation, metadata generation, planning, diagnostics, and code generation—to operate without depending on compiler APIs. Internal Models are immutable, deterministic, lightweight, and independent of Roslyn.

This layer is the heart of the Generator.

Everything after Roslyn depends on Internal Models.

Nothing after this layer should depend on Roslyn.

---

# Philosophy

Internal Models follow one rule:

> **Roslyn is an input. Internal Models are the architecture.**

Compiler symbols are transient.

Internal Models are the framework's language.

---

# Why Internal Models?

Roslyn provides an excellent compiler API.

It is not an application model.

Using Roslyn everywhere causes:

- Large object graphs
- Difficult testing
- Poor equality
- Generator coupling
- Memory pressure

Instead:

```
Roslyn

↓

Internal Models

↓

Everything Else
```

Roslyn is isolated.

---

# High-Level Architecture

```
Source Code

↓

Roslyn Symbols

↓

Internal Models

↓

Validation

↓

Metadata

↓

Planning

↓

Generation
```

Only the first stage understands Roslyn.

---

# Responsibilities

Internal Models are responsible for representing:

- Entities
- Models
- Properties
- Relationships
- Graphs
- Attributes
- Keys
- Configuration

They never generate code.

---

# Internal Models Do NOT

Internal Models should never:

- Emit source
- Write SQL
- Execute queries
- Allocate identifiers
- Perform reflection
- Depend upon Runtime

They are pure data.

---

# Roslyn Boundary

Roslyn ends here.

```
INamedTypeSymbol

↓

EntityModel
```

Later stages never reference compiler symbols.

---

# Why This Boundary Exists

Roslyn symbols are:

- Expensive
- Context dependent
- Difficult to compare
- Difficult to serialize

Internal Models are:

- Immutable
- Lightweight
- Value comparable
- Easily tested

---

# Example Transformation

```
public class Customer
{
    public int Id { get; set; }

    public string Name { get; set; }
}
```

becomes:

```
EntityModel

↓

Name

↓

Properties

↓

Attributes

↓

Relationships
```

The original syntax is no longer required.

---

# Typical Models

Examples include:

```
EntityModel

PropertyModel

RelationshipModel

GraphModel

JoinModel

LookupModel

ProjectionModel
```

Every model represents one concept.

---

# Immutability

Internal Models should be immutable.

Preferred implementation:

```
record
```

or

```
sealed record
```

Mutable models complicate incremental generation.

---

# Equality

Equality is fundamental.

```
EntityModel.Equals()
```

Incremental generation depends upon value equality.

Incorrect equality leads to unnecessary regeneration.

---

# Identity

Models should have stable identities.

Examples:

```
Namespace

Name

Qualified Name
```

Avoid compiler object identity.

---

# Validation

Validation consumes Internal Models.

```
EntityModel

↓

Validation

↓

Diagnostic
```

Validation never depends upon Roslyn.

---

# Metadata Generation

Metadata is generated from Internal Models.

```
EntityModel

↓

EntityMetadata
```

Metadata generation becomes straightforward.

---

# Planner Generation

Planning also consumes Internal Models.

```
RelationshipModel

↓

Join Planner

↓

Execution Plan
```

Planning remains compiler independent.

---

# Code Generation

Emitters consume Internal Models.

```
EntityModel

↓

MaterializerEmitter

↓

Generated Source
```

The emitter never examines syntax trees.

---

# Model Hierarchy

Typical hierarchy:

```
Solution

↓

Assembly

↓

Namespace

↓

Entity

↓

Property
```

Hierarchy should mirror the logical application.

---

# Relationships

Relationships should use references.

Example:

```
Customer

↓

Orders
```

Avoid embedding duplicate entity information.

---

# References

References should remain lightweight.

Examples:

```
EntityReference

PropertyReference

JoinReference
```

Large object graphs should be avoided.

---

# Collections

Prefer immutable collections.

Examples:

```
ImmutableArray<T>

ImmutableDictionary<TKey,TValue>
```

Avoid mutable lists.

---

# Ordering

Collections should be deterministic.

Preferred ordering:

```
Namespace

↓

Entity

↓

Property
```

Ordering affects generated output.

---

# Serialization

Internal Models should be serializable.

Serialization enables:

- Snapshot testing
- Debugging
- Diagnostics
- Future tooling

Models should not contain compiler state.

---

# Memory

Models should remain compact.

Avoid storing:

- Syntax trees
- Symbols
- Semantic models

Store only required information.

---

# Thread Safety

Immutable models are naturally thread-safe.

Incremental generators may safely process them in parallel.

---

# Testing

Internal Models should have dedicated tests.

Recommended tests:

```
Equality

↓

Construction

↓

Ordering

↓

Serialization

↓

Validation
```

Models should be verified independently.

---

# Snapshot Testing

Internal Models make excellent snapshot candidates.

```
Source

↓

EntityModel

↓

Snapshot
```

Changes become immediately visible.

---

# Native AOT

Internal Models exist only during compilation.

They contribute nothing to Runtime size.

They help eliminate reflection by enabling generation.

---

# Future Evolution

Future Internal Models may include:

- SecurityModel
- CacheModel
- EventModel
- AuthorizationModel
- ProjectionOptimizationModel
- ProviderCapabilityModel

Every model should remain immutable.

---

# Internal Model Checklist

Before introducing a new Internal Model, ask:

- Is it immutable?
- Does it avoid Roslyn dependencies?
- Does it support value equality?
- Can it be snapshot tested?
- Can multiple stages consume it?
- Does it represent a single concept?

If not, redesign it.

---

# Relationship to the Framework

Internal Models form the bridge between the compiler and the framework.

```
Roslyn

↓

Internal Models

↓

Validation

↓

Metadata

↓

Planning

↓

Generation
```

Every compile-time subsystem depends upon them.

Runtime never sees them.

---

# Summary

The Internal Model Architecture establishes a compiler-independent, immutable representation of application structure that serves as the foundation for validation, metadata generation, planning, diagnostics, and code generation.

By isolating Roslyn behind a stable set of value-based models, CoffeeBeanery improves incremental generation, reduces memory usage, simplifies testing, enables deterministic code generation, and creates a clean architectural boundary between compiler analysis and framework implementation.

# Metadata Architecture

> Metadata is the immutable structural knowledge generated by the CoffeeBeanery compiler that describes the application's domain model. It is the single source of truth for Runtime, planners, providers, and generated materializers. Metadata replaces runtime reflection with compile-time generated information, allowing the framework to execute efficiently, deterministically, and without inspecting CLR types.

Metadata is knowledge.

Runtime consumes it.

The Generator creates it.

---

# Philosophy

Metadata follows one architectural principle:

> **Structure should be described once and reused everywhere.**

Runtime should never rediscover application structure.

---

# Why Metadata?

Traditional ORMs often perform:

```
CLR Type

↓

Reflection

↓

Metadata

↓

Execution
```

CoffeeBeanery performs:

```
Compilation

↓

Generated Metadata

↓

Execution
```

Reflection disappears.

---

# Metadata Is Knowledge

Metadata answers questions like:

- What entities exist?
- Which properties are keys?
- Which columns exist?
- How are entities related?
- Which joins are valid?
- Which graphs are available?

Runtime should never infer these answers.

---

# High-Level Architecture

```
Source Code

↓

Internal Models

↓

Metadata Generation

↓

Generated Metadata

↓

Runtime
```

Metadata is generated once.

---

# Responsibilities

Metadata describes:

- Entities
- Models
- Properties
- Columns
- Relationships
- Graphs
- Identifiers
- Constraints

Metadata never executes behavior.

---

# Metadata Does NOT

Metadata should never:

- Execute SQL
- Materialize objects
- Plan queries
- Open connections
- Manage transactions

Metadata is descriptive.

---

# Metadata Hierarchy

Typical hierarchy:

```
Metadata

├── Entity Metadata

├── Column Metadata

├── Join Metadata

├── Graph Metadata

├── Lookup Metadata

└── Model Metadata
```

Each metadata type owns one concern.

---

# Entity Metadata

Entity metadata describes:

```
Customer

↓

Properties

↓

Relationships

↓

Keys
```

It is the primary runtime lookup.

---

# Column Metadata

Column metadata describes:

- Column name
- CLR type
- Database type
- Nullability
- Key participation

It contains no execution logic.

---

# Relationship Metadata

Relationships describe:

```
Customer

↓

Orders
```

or

```
Order

↓

Customer
```

Traversal becomes deterministic.

---

# Graph Metadata

Graph metadata describes:

- Reachable nodes
- Navigation paths
- Traversal rules

Graph execution consumes this information.

---

# Join Metadata

Join metadata defines:

```
Left Entity

↓

Right Entity

↓

Join Columns
```

Runtime never discovers joins.

---

# Lookup Metadata

Lookup metadata supports:

- Unique constraints
- Alternate keys
- Lookup operations

Planning consumes lookup metadata.

---

# Model Metadata

Model metadata describes projections.

Example:

```
CustomerSummary

↓

Fields

↓

Entity Mapping
```

Models remain independent of entities.

---

# Identifier Integration

Every metadata object references identifiers.

```
EntityId

↓

EntityMetadata
```

Numeric indexing replaces string lookups.

---

# Registry Architecture

Generated metadata is organized into registries.

```
Metadata Registry

↓

EntityId

↓

Metadata
```

Registries remain immutable.

---

# Runtime Lookup

Runtime performs:

```
EntityId

↓

MetadataRegistry

↓

EntityMetadata
```

Lookup becomes O(1).

---

# Immutability

Metadata should be immutable.

Preferred implementation:

```
sealed record
```

or

```
readonly struct
```

Mutation should never occur.

---

# Equality

Metadata should support value equality.

Equality enables:

- Snapshot testing
- Incremental generation
- Regression detection

Identity should not depend on object references.

---

# Generation

Metadata is generated during compilation.

```
Internal Models

↓

Metadata

↓

Generated Source
```

Runtime never builds metadata.

---

# Serialization

Metadata should be serializable.

Benefits include:

- Tooling
- Debugging
- Diagnostics
- Future visualization

Serialization should remain deterministic.

---

# Thread Safety

Immutable metadata is naturally thread-safe.

Singleton registration becomes trivial.

No synchronization is required.

---

# Memory Layout

Metadata should remain compact.

Avoid:

- Reflection information
- Compiler symbols
- Runtime delegates

Store only structural knowledge.

---

# Metadata Provider

Runtime consumes metadata through contracts.

Example:

```
IMetadataProvider
```

Implementations are generated.

---

# Provider Independence

Providers consume metadata without understanding domain models.

```
Metadata

↓

Provider

↓

SQL
```

Metadata abstracts application structure.

---

# Planner Integration

Planning relies heavily on metadata.

```
Metadata

↓

Query Planner

↓

Execution Plan
```

Planning performs no reflection.

---

# Materializer Integration

Generated materializers use metadata for:

- Column mapping
- Property ordering
- Identity information

Construction logic remains generated.

---

# Diagnostics

Metadata validation occurs before generation.

Examples:

- Duplicate columns
- Missing identifiers
- Invalid relationships

Runtime assumes metadata correctness.

---

# Performance

Metadata improves performance by eliminating:

- Reflection
- Dictionary discovery
- Attribute scanning
- Dynamic property lookup

Runtime performs only direct indexing.

---

# Native AOT

Metadata generation is one of the primary enablers of Native AOT.

Generated metadata removes the need for:

- Reflection
- Runtime scanning
- Dynamic metadata construction

Execution remains static.

---

# Testing

Metadata should be tested independently.

Recommended tests:

```
Entity Metadata

↓

Column Metadata

↓

Relationship Metadata

↓

Graph Metadata

↓

Snapshot Tests
```

Generated metadata should always remain deterministic.

---

# Future Evolution

Future metadata may include:

- Authorization metadata
- Cache metadata
- Event metadata
- Security metadata
- Provider capability metadata
- Query optimization metadata

Each addition should remain immutable.

---

# Metadata Checklist

Before introducing new metadata, ask:

- Is it immutable?
- Is it descriptive rather than executable?
- Can it be generated?
- Does it avoid reflection?
- Can Runtime consume it directly?
- Can it be independently tested?

If not, reconsider the design.

---

# Relationship to the Framework

Metadata forms the central knowledge layer of CoffeeBeanery.

```
Source Code

↓

Internal Models

↓

Metadata

↓

Planning

↓

Runtime

↓

Provider

↓

Materializer
```

Every subsystem depends upon metadata.

Metadata depends only upon compilation.

---

# Summary

The Metadata Architecture provides the immutable structural knowledge that drives every stage of execution within CoffeeBeanery, describing entities, columns, relationships, graphs, identifiers, and models without containing execution behavior.

By generating metadata during compilation and exposing it through deterministic registries and provider-independent contracts, CoffeeBeanery eliminates runtime reflection, simplifies planning, accelerates execution, enables Native AOT, and establishes a single authoritative representation of application structure throughout the framework.

# Planning Architecture

> Planning is the process of transforming a high-level execution request into an immutable execution strategy. Rather than executing requests directly, CoffeeBeanery first analyzes metadata, relationships, projections, filters, provider capabilities, and execution requirements to produce a deterministic execution plan. Runtime executes plans exactly as produced without performing additional analysis.

Planning answers one question:

> **How should this request execute?**

Runtime answers:

> **Execute it.**

---

# Philosophy

Planning follows one architectural rule:

> **Analyze once. Execute many.**

Planning is analytical.

Runtime is mechanical.

---

# Why Planning?

Without planning:

```
Request

↓

Runtime Analysis

↓

Execution
```

Runtime becomes large and difficult to optimize.

With planning:

```
Request

↓

Planner

↓

Execution Plan

↓

Runtime
```

Execution becomes deterministic.

---

# High-Level Architecture

```
Execution Request

↓

Metadata

↓

Planner

↓

Execution Plan

↓

Runtime
```

Planning separates decision making from execution.

---

# Responsibilities

Planning is responsible for:

- Entity resolution
- Relationship traversal
- Join construction
- Projection analysis
- Filter normalization
- Ordering
- Dependency analysis
- Provider optimization

Planning never executes SQL.

---

# Planning Does NOT

Planning should never:

- Open connections
- Execute commands
- Materialize objects
- Manage transactions
- Serialize responses

Execution belongs to Runtime.

---

# Execution Plans

Planning produces immutable plans.

Examples:

```
QueryPlan
```

```
MutationPlan
```

Plans completely describe execution.

---

# Query Planning

Query planning determines:

```
Requested Entity

↓

Relationships

↓

Filters

↓

Ordering

↓

Projection

↓

Execution Plan
```

No SQL is generated yet.

---

# Mutation Planning

Mutation planning determines:

- Insert order
- Update order
- Dependency graph
- Lookup strategy
- Conflict handling
- Returning requirements

Execution remains provider-independent.

---

# Plan Immutability

Plans should never change after creation.

```
Planner

↓

Immutable Plan

↓

Runtime
```

Runtime relies on this guarantee.

---

# Metadata Consumption

Planning consumes metadata extensively.

```
Metadata

↓

Planner

↓

Execution Plan
```

Planning performs no reflection.

---

# Entity Resolution

Planning identifies:

```
Customer

↓

Entity Metadata
```

Resolution becomes deterministic.

---

# Relationship Resolution

Relationships become traversal instructions.

```
Customer

↓

Orders

↓

OrderItems
```

Traversal is planned before execution.

---

# Join Planning

Join metadata becomes join operations.

```
Join Metadata

↓

Join Plan
```

Providers later serialize these joins.

---

# Projection Planning

Requested fields become projection metadata.

```
Customer

↓

Id

Name

Email
```

Only required data is selected.

---

# Filter Planning

Planning normalizes filters.

```
Age > 18

↓

Normalized Filter Tree
```

Providers later serialize filters.

---

# Ordering Planning

Ordering becomes explicit.

```
Name ASC

↓

OrderPlan
```

Runtime simply follows instructions.

---

# Pagination Planning

Pagination is represented explicitly.

```
Skip

Take

↓

PaginationPlan
```

Providers translate into SQL.

---

# Graph Planning

Graph traversal becomes deterministic.

```
Root

↓

Children

↓

Descendants
```

Graph expansion never occurs during execution.

---

# Dependency Planning

Mutations construct dependency graphs.

```
Customer

↓

Order

↓

OrderItem
```

Execution order becomes explicit.

---

# Provider Capabilities

Planning considers provider features.

Examples:

- RETURNING
- MERGE
- Recursive CTE
- JSON support

Capability decisions occur before execution.

---

# Optimization

Planning performs structural optimization.

Examples:

- Join elimination
- Projection reduction
- Predicate normalization
- Lookup optimization

Execution performs no optimization.

---

# Validation

Planning validates requests.

Examples:

- Unknown entities
- Invalid traversals
- Unsupported projections

Invalid plans never reach Runtime.

---

# Diagnostics

Planning may produce diagnostics before execution.

Examples:

- Ambiguous relationships
- Unsupported mutations
- Invalid lookups

Failures occur early.

---

# Determinism

Identical requests produce identical plans.

```
Request A

↓

Plan A
```

Repeated planning remains stable.

---

# Thread Safety

Planners should remain stateless.

Mutable execution state belongs to Runtime.

Singleton planners are preferred.

---

# Testing

Planning should be tested independently.

Recommended tests:

```
Entity Resolution

↓

Join Planning

↓

Projection Planning

↓

Mutation Planning

↓

Snapshot Tests
```

Execution should not be required.

---

# Snapshot Testing

Execution plans are ideal snapshot artifacts.

```
Request

↓

Plan

↓

Snapshot
```

Architectural regressions become visible immediately.

---

# Native AOT

Planning is fully compatible with Native AOT because it consumes generated metadata rather than runtime reflection.

No dynamic code generation is required.

---

# Future Evolution

Future planning features may include:

- Cost estimation
- Query rewriting
- Adaptive provider strategies
- Distributed planning
- Plan caching
- Incremental planning

The Runtime contract should remain unchanged.

---

# Planning Checklist

Before adding planning functionality, ask:

- Does it make an execution decision?
- Can it occur before Runtime?
- Does it consume metadata rather than reflection?
- Does it produce immutable output?
- Can Runtime execute it without reinterpretation?
- Can it be snapshot tested?

If not, reconsider its placement.

---

# Relationship to the Framework

Planning bridges metadata and execution.

```
Metadata

↓

Planner

↓

Execution Plan

↓

Runtime

↓

Provider
```

Planning understands structure.

Runtime understands execution.

---

# Summary

The Planning Architecture transforms execution requests into immutable execution plans by analyzing metadata, relationships, projections, filters, dependencies, and provider capabilities before execution begins.

By separating decision making from execution, CoffeeBeanery creates a deterministic, provider-independent execution pipeline that simplifies Runtime, improves testability, enables structural optimization, eliminates reflection, and supports Native AOT while preserving a clear architectural boundary between analysis and execution.

# Runtime Architecture

> The Runtime is the execution engine of CoffeeBeanery. Its responsibility is to execute immutable execution plans produced by the Planner using generated metadata, providers, and materializers. Runtime performs no discovery, no planning, and no reflection. It coordinates execution while remaining lightweight, deterministic, provider-independent, and optimized for Native AOT.

Runtime is the heart of execution.

It does not decide what to execute.

It executes what has already been decided.

---

# Philosophy

Runtime follows one architectural rule:

> **Execution without interpretation.**

Planning already made the decisions.

Runtime simply carries them out.

---

# Why a Runtime?

Without a dedicated Runtime:

```
Request

↓

Planner

↓

Provider

↓

Materializer
```

Responsibilities become blurred.

With Runtime:

```
Planner

↓

Runtime

↓

Provider

↓

Materializer
```

Execution becomes centralized.

---

# High-Level Architecture

```
Execution Plan

↓

Runtime

↓

Provider

↓

Database

↓

Materializer

↓

Objects
```

Runtime coordinates the pipeline.

---

# Responsibilities

Runtime is responsible for:

- Executing plans
- Coordinating providers
- Managing execution contexts
- Managing transactions
- Streaming results
- Identity resolution
- Graph assembly
- Invoking materializers

Runtime never analyzes application structure.

---

# Runtime Does NOT

Runtime should never:

- Discover metadata
- Build execution plans
- Parse requests
- Inspect attributes
- Generate SQL strategies
- Perform reflection

Those responsibilities belong elsewhere.

---

# Execution Flow

Every execution follows the same sequence.

```
Execution Plan

↓

Execution Context

↓

Provider

↓

Rows

↓

Materializer

↓

Objects
```

Runtime orchestrates each stage.

---

# Execution Context

Each execution receives an immutable plan and a mutable execution context.

```
ExecutionPlan

+

ExecutionContext

↓

Runtime
```

The plan defines behavior.

The context stores state.

---

# Execution Context Responsibilities

Execution Context contains:

- Parameters
- Cancellation token
- Identity map
- Transaction
- User context
- Diagnostics

It is never shared across requests.

---

# Runtime Coordination

Runtime delegates work.

```
Runtime

↓

Provider

↓

Materializer

↓

Identity Map
```

Runtime owns orchestration.

Not implementation.

---

# Provider Interaction

Runtime communicates through provider interfaces.

```
Runtime

↓

IProvider

↓

Database
```

Providers remain interchangeable.

---

# SQL Execution

Runtime does not generate SQL.

Instead:

```
Execution Plan

↓

Provider SQL Writer

↓

SQL
```

Execution begins after SQL generation.

---

# Streaming

Providers stream rows.

```
Database

↓

Reader

↓

Runtime
```

Streaming minimizes allocations.

---

# Materialization

Runtime delegates construction.

```
Rows

↓

Generated Materializer

↓

Objects
```

Runtime never sets properties directly.

---

# Identity Resolution

Runtime guarantees object identity.

```
Primary Key

↓

Identity Map

↓

Existing Object
```

Duplicate graph nodes become shared references.

---

# Graph Assembly

Relationships are assembled during execution.

```
Customer

↓

Orders

↓

OrderItems
```

Materializers create objects.

Runtime connects them.

---

# Transactions

Runtime coordinates transactions.

```
Begin

↓

Execute

↓

Commit
```

or

```
Rollback
```

Providers implement transaction mechanics.

---

# Cancellation

Execution should honor cancellation.

```
CancellationToken

↓

Provider

↓

Execution Stops
```

Cancellation originates from Transport.

---

# Error Handling

Runtime converts infrastructure failures into execution failures.

Examples:

- Connection failure
- Timeout
- Constraint violation

Planning failures should never reach Runtime.

---

# Diagnostics

Runtime records execution diagnostics.

Examples:

- Duration
- Rows read
- Materialized objects
- SQL execution time

Diagnostics never modify execution.

---

# Thread Safety

Runtime services should remain stateless.

Mutable execution state belongs exclusively to:

```
ExecutionContext
```

Singleton runtimes become naturally thread-safe.

---

# Memory

Runtime minimizes allocations.

Preferred patterns:

- Streaming
- Reused buffers
- Immutable metadata
- Array indexing

Execution should avoid unnecessary object creation.

---

# Provider Independence

Runtime understands only provider contracts.

```
Runtime

↓

IProvider
```

It never references provider implementations.

---

# Materializer Independence

Runtime knows only:

```
IMaterializer
```

Generated implementations remain replaceable.

---

# Metadata Consumption

Runtime performs metadata lookups.

```
EntityId

↓

Metadata

↓

Execution
```

Metadata remains immutable.

---

# Plan Consumption

Runtime trusts execution plans completely.

It does not:

- Reorder joins
- Rewrite filters
- Optimize projections

Planning already completed these tasks.

---

# Performance

Runtime performance comes from:

- Immutable plans
- Immutable metadata
- Generated materializers
- Numeric identifiers
- Streaming
- Provider specialization

Execution becomes almost mechanical.

---

# Native AOT

Runtime is designed for Native AOT.

It avoids:

- Reflection
- Runtime code generation
- Dynamic discovery
- Expression compilation

Everything required already exists.

---

# Testing

Runtime should be tested independently.

Recommended tests:

```
Execution

↓

Transactions

↓

Identity Resolution

↓

Streaming

↓

Graph Assembly
```

Providers may be mocked.

---

# Future Evolution

Future Runtime enhancements may include:

- Parallel execution
- Distributed execution
- Plan caching
- Adaptive batching
- Execution metrics
- Cooperative scheduling

The Runtime contract should remain stable.

---

# Runtime Checklist

Before adding Runtime functionality, ask:

- Is this execution rather than planning?
- Can it avoid reflection?
- Can it consume immutable metadata?
- Can it remain provider-independent?
- Can it remain stateless?
- Can it be tested independently?

If not, reconsider its placement.

---

# Relationship to the Framework

Runtime connects planning to infrastructure.

```
Planner

↓

Execution Plan

↓

Runtime

↓

Provider

↓

Materializer

↓

Application Objects
```

Planning creates instructions.

Runtime executes them.

---

# Summary

The Runtime Architecture defines the deterministic execution engine of CoffeeBeanery, coordinating providers, execution contexts, transactions, identity resolution, graph assembly, and generated materializers while consuming immutable execution plans and metadata.

By separating execution from planning, generation, and infrastructure concerns, the Runtime remains lightweight, provider-independent, reflection-free, highly testable, performant, and fully compatible with Native AOT, serving as the central orchestrator of the framework's execution pipeline.

# Runtime Architecture

> The Runtime is the execution engine of CoffeeBeanery. Its responsibility is to execute immutable execution plans produced by the Planner using generated metadata, providers, and materializers. Runtime performs no discovery, no planning, and no reflection. It coordinates execution while remaining lightweight, deterministic, provider-independent, and optimized for Native AOT.

Runtime is the heart of execution.

It does not decide what to execute.

It executes what has already been decided.

---

# Philosophy

Runtime follows one architectural rule:

> **Execution without interpretation.**

Planning already made the decisions.

Runtime simply carries them out.

---

# Why a Runtime?

Without a dedicated Runtime:

```
Request

↓

Planner

↓

Provider

↓

Materializer
```

Responsibilities become blurred.

With Runtime:

```
Planner

↓

Runtime

↓

Provider

↓

Materializer
```

Execution becomes centralized.

---

# High-Level Architecture

```
Execution Plan

↓

Runtime

↓

Provider

↓

Database

↓

Materializer

↓

Objects
```

Runtime coordinates the pipeline.

---

# Responsibilities

Runtime is responsible for:

- Executing plans
- Coordinating providers
- Managing execution contexts
- Managing transactions
- Streaming results
- Identity resolution
- Graph assembly
- Invoking materializers

Runtime never analyzes application structure.

---

# Runtime Does NOT

Runtime should never:

- Discover metadata
- Build execution plans
- Parse requests
- Inspect attributes
- Generate SQL strategies
- Perform reflection

Those responsibilities belong elsewhere.

---

# Execution Flow

Every execution follows the same sequence.

```
Execution Plan

↓

Execution Context

↓

Provider

↓

Rows

↓

Materializer

↓

Objects
```

Runtime orchestrates each stage.

---

# Execution Context

Each execution receives an immutable plan and a mutable execution context.

```
ExecutionPlan

+

ExecutionContext

↓

Runtime
```

The plan defines behavior.

The context stores state.

---

# Execution Context Responsibilities

Execution Context contains:

- Parameters
- Cancellation token
- Identity map
- Transaction
- User context
- Diagnostics

It is never shared across requests.

---

# Runtime Coordination

Runtime delegates work.

```
Runtime

↓

Provider

↓

Materializer

↓

Identity Map
```

Runtime owns orchestration.

Not implementation.

---

# Provider Interaction

Runtime communicates through provider interfaces.

```
Runtime

↓

IProvider

↓

Database
```

Providers remain interchangeable.

---

# SQL Execution

Runtime does not generate SQL.

Instead:

```
Execution Plan

↓

Provider SQL Writer

↓

SQL
```

Execution begins after SQL generation.

---

# Streaming

Providers stream rows.

```
Database

↓

Reader

↓

Runtime
```

Streaming minimizes allocations.

---

# Materialization

Runtime delegates construction.

```
Rows

↓

Generated Materializer

↓

Objects
```

Runtime never sets properties directly.

---

# Identity Resolution

Runtime guarantees object identity.

```
Primary Key

↓

Identity Map

↓

Existing Object
```

Duplicate graph nodes become shared references.

---

# Graph Assembly

Relationships are assembled during execution.

```
Customer

↓

Orders

↓

OrderItems
```

Materializers create objects.

Runtime connects them.

---

# Transactions

Runtime coordinates transactions.

```
Begin

↓

Execute

↓

Commit
```

or

```
Rollback
```

Providers implement transaction mechanics.

---

# Cancellation

Execution should honor cancellation.

```
CancellationToken

↓

Provider

↓

Execution Stops
```

Cancellation originates from Transport.

---

# Error Handling

Runtime converts infrastructure failures into execution failures.

Examples:

- Connection failure
- Timeout
- Constraint violation

Planning failures should never reach Runtime.

---

# Diagnostics

Runtime records execution diagnostics.

Examples:

- Duration
- Rows read
- Materialized objects
- SQL execution time

Diagnostics never modify execution.

---

# Thread Safety

Runtime services should remain stateless.

Mutable execution state belongs exclusively to:

```
ExecutionContext
```

Singleton runtimes become naturally thread-safe.

---

# Memory

Runtime minimizes allocations.

Preferred patterns:

- Streaming
- Reused buffers
- Immutable metadata
- Array indexing

Execution should avoid unnecessary object creation.

---

# Provider Independence

Runtime understands only provider contracts.

```
Runtime

↓

IProvider
```

It never references provider implementations.

---

# Materializer Independence

Runtime knows only:

```
IMaterializer
```

Generated implementations remain replaceable.

---

# Metadata Consumption

Runtime performs metadata lookups.

```
EntityId

↓

Metadata

↓

Execution
```

Metadata remains immutable.

---

# Plan Consumption

Runtime trusts execution plans completely.

It does not:

- Reorder joins
- Rewrite filters
- Optimize projections

Planning already completed these tasks.

---

# Performance

Runtime performance comes from:

- Immutable plans
- Immutable metadata
- Generated materializers
- Numeric identifiers
- Streaming
- Provider specialization

Execution becomes almost mechanical.

---

# Native AOT

Runtime is designed for Native AOT.

It avoids:

- Reflection
- Runtime code generation
- Dynamic discovery
- Expression compilation

Everything required already exists.

---

# Testing

Runtime should be tested independently.

Recommended tests:

```
Execution

↓

Transactions

↓

Identity Resolution

↓

Streaming

↓

Graph Assembly
```

Providers may be mocked.

---

# Future Evolution

Future Runtime enhancements may include:

- Parallel execution
- Distributed execution
- Plan caching
- Adaptive batching
- Execution metrics
- Cooperative scheduling

The Runtime contract should remain stable.

---

# Runtime Checklist

Before adding Runtime functionality, ask:

- Is this execution rather than planning?
- Can it avoid reflection?
- Can it consume immutable metadata?
- Can it remain provider-independent?
- Can it remain stateless?
- Can it be tested independently?

If not, reconsider its placement.

---

# Relationship to the Framework

Runtime connects planning to infrastructure.

```
Planner

↓

Execution Plan

↓

Runtime

↓

Provider

↓

Materializer

↓

Application Objects
```

Planning creates instructions.

Runtime executes them.

---

# Summary

The Runtime Architecture defines the deterministic execution engine of CoffeeBeanery, coordinating providers, execution contexts, transactions, identity resolution, graph assembly, and generated materializers while consuming immutable execution plans and metadata.

By separating execution from planning, generation, and infrastructure concerns, the Runtime remains lightweight, provider-independent, reflection-free, highly testable, performant, and fully compatible with Native AOT, serving as the central orchestrator of the framework's execution pipeline.

# Provider Architecture

> Providers are the infrastructure adapters that translate execution plans into operations for a specific database engine. They isolate database-specific behavior from the Runtime, allowing the execution engine to remain completely independent of SQL dialects, drivers, connection management, and vendor-specific capabilities. Providers are responsible for execution—not planning—and expose a consistent contract regardless of the underlying database.

Providers connect CoffeeBeanery to storage.

They never define framework behavior.

---

# Philosophy

Providers follow one architectural principle:

> **Infrastructure should be replaceable without affecting execution.**

Runtime executes plans.

Providers execute infrastructure.

---

# Why Providers?

Without providers:

```
Runtime

↓

PostgreSQL APIs

↓

Database
```

Runtime becomes database-specific.

With providers:

```
Runtime

↓

IProvider

↓

PostgreSQL

SQL Server

SQLite

MySQL
```

Execution remains portable.

---

# High-Level Architecture

```
Execution Plan

↓

Runtime

↓

IProvider

↓

Database Driver

↓

Database
```

Providers isolate infrastructure.

---

# Responsibilities

Providers are responsible for:

- SQL generation
- Parameter binding
- Connection management
- Command execution
- Transaction integration
- Result streaming
- Database capability reporting

Providers never perform planning.

---

# Providers Do NOT

Providers should never:

- Discover metadata
- Build execution plans
- Resolve relationships
- Materialize objects
- Parse requests
- Perform reflection

Execution decisions belong to Runtime and Planner.

---

# Provider Contract

Runtime communicates through a single abstraction.

```
IProvider
```

Every database implements the same contract.

---

# SQL Generation

Providers serialize execution plans.

```
Execution Plan

↓

SQL Writer

↓

SQL
```

The SQL Writer belongs to the provider.

---

# SQL Dialects

Every provider owns its SQL dialect.

Examples:

```
PostgreSQL
```

```
SQL Server
```

```
SQLite
```

```
MySQL
```

Dialect differences never leak into Runtime.

---

# Command Execution

Providers transform SQL into executable commands.

```
SQL

↓

DbCommand

↓

Database
```

Runtime remains database agnostic.

---

# Parameter Binding

Providers bind parameters safely.

```
Execution Parameters

↓

DbParameter

↓

Command
```

SQL injection protection belongs here.

---

# Connection Management

Providers manage:

- Opening connections
- Closing connections
- Pool integration
- Retry policies

Runtime simply requests execution.

---

# Transactions

Providers integrate with transactions.

```
Runtime

↓

Provider

↓

DbTransaction
```

Transaction semantics remain provider-specific.

---

# Streaming

Providers stream rows efficiently.

```
Database

↓

Reader

↓

Runtime
```

Large datasets should never require buffering.

---

# Result Readers

Providers expose row readers.

```
IDataReader

↓

Generated Materializer
```

Providers never construct domain objects.

---

# Provider Capabilities

Each provider reports supported features.

Examples:

- RETURNING
- MERGE
- Recursive CTE
- Window Functions
- JSON Columns
- UPSERT

Planning consumes capability information.

---

# Capability Abstraction

Capabilities are explicit.

```
ProviderCapabilities

↓

Planner

↓

Execution Strategy
```

Runtime remains unchanged.

---

# SQL Writers

SQL generation is delegated.

```
Execution Plan

↓

SqlWriter

↓

SQL
```

Different providers implement different writers.

---

# Schema Translation

Providers map metadata into physical objects.

```
Entity Metadata

↓

Schema

↓

Table

↓

Column
```

Naming conventions belong here.

---

# Identifier Quoting

Providers own identifier syntax.

Examples:

```
"Customer"
```

```
[Customer]
```

```
`Customer`
```

Runtime never formats identifiers.

---

# Literal Formatting

Providers serialize literals.

Examples:

- Dates
- GUIDs
- Booleans
- JSON
- Binary

Formatting rules remain provider-specific.

---

# Type Mapping

Providers map CLR types to database types.

```
string

↓

varchar
```

```
DateTime

↓

timestamp
```

Mappings remain isolated.

---

# Error Translation

Database exceptions become provider exceptions.

Examples:

- Unique constraint
- Deadlock
- Timeout
- Connection failure

Runtime receives provider-neutral failures.

---

# Diagnostics

Providers collect execution metrics.

Examples:

- SQL duration
- Rows affected
- Command count
- Retry attempts

Diagnostics never alter execution.

---

# Thread Safety

Providers should be stateless.

Mutable execution state belongs to:

- Connections
- Transactions
- Readers

Provider services themselves should be singleton-safe.

---

# Performance

Providers optimize:

- SQL generation
- Parameter reuse
- Streaming
- Batching
- Command preparation

Runtime should require no provider-specific optimizations.

---

# Native AOT

Providers should remain Native AOT compatible.

Avoid:

- Reflection
- Dynamic emit
- Runtime SQL compilation
- Expression generation

Prefer explicit serializers.

---

# Testing

Providers should be tested independently.

Recommended tests:

```
SQL Generation

↓

Parameter Binding

↓

Transactions

↓

Capability Detection

↓

Execution
```

Runtime may be mocked.

---

# Extensibility

New providers implement:

```
IProvider
```

Nothing else in the framework should require modification.

Examples:

- PostgreSQL
- SQL Server
- Oracle
- MySQL
- SQLite
- CockroachDB

Provider expansion should be incremental.

---

# Provider Checklist

Before implementing a provider, ask:

- Does it expose the standard contract?
- Is SQL generation isolated?
- Are capabilities explicit?
- Is Runtime unaffected?
- Is reflection avoided?
- Can it be tested independently?

If not, redesign the provider.

---

# Relationship to the Framework

Providers isolate infrastructure from execution.

```
Planner

↓

Execution Plan

↓

Runtime

↓

Provider

↓

Database

↓

Rows

↓

Materializer
```

Runtime coordinates.

Providers communicate with databases.

---

# Summary

The Provider Architecture defines the infrastructure layer of CoffeeBeanery, translating immutable execution plans into efficient database operations while isolating SQL dialects, connection management, transactions, parameter binding, and database capabilities behind a stable provider contract.

By separating infrastructure from execution logic, providers enable Runtime to remain provider-independent, deterministic, reflection-free, highly testable, extensible, and fully compatible with Native AOT while allowing each database implementation to optimize for its own capabilities without affecting the rest of the framework.

# Materialization Architecture

> Materialization is the process of transforming raw database rows into fully constructed application objects. In CoffeeBeanery, materialization is entirely generated at compile-time, eliminating reflection, expression compilation, dynamic property lookup, and runtime mapping. Generated materializers provide deterministic, high-performance object construction while allowing Runtime to remain focused solely on execution orchestration.

Materialization is the final stage of execution.

Execution produces rows.

Materialization produces objects.

---

# Philosophy

Materialization follows one principle:

> **Objects should be constructed directly, not discovered dynamically.**

Compile-time generation replaces runtime mapping.

---

# Why Materialization Exists

Databases return:

```
Rows
```

Applications require:

```
Objects
```

Materialization bridges that gap.

---

# Traditional Materialization

Many ORMs perform:

```
Reader

↓

Reflection

↓

Property Discovery

↓

Assignment

↓

Object
```

CoffeeBeanery performs:

```
Reader

↓

Generated Materializer

↓

Object
```

Reflection disappears.

---

# High-Level Architecture

```
Database

↓

Provider

↓

Reader

↓

Generated Materializer

↓

Application Object
```

Runtime coordinates the process.

---

# Responsibilities

Materializers are responsible for:

- Object creation
- Constructor invocation
- Property assignment
- Collection creation
- Value conversion
- Identity assignment

They never execute SQL.

---

# Materializers Do NOT

Materializers should never:

- Build execution plans
- Open connections
- Resolve metadata
- Execute queries
- Manage transactions
- Perform reflection

Construction is their only concern.

---

# Compile-Time Generation

Materializers are generated during compilation.

```
EntityModel

↓

Materializer Generator

↓

Generated Materializer
```

No runtime generation occurs.

---

# Generated Code

Instead of:

```
SetValue(...)
```

Generated code becomes:

```csharp
customer.Id = reader.GetInt32(0);
customer.Name = reader.GetString(1);
```

The generated code is straightforward.

---

# Constructor Support

Materializers may use:

```
Default Constructor
```

or

```
Parameterized Constructor
```

Construction strategy is determined during generation.

---

# Property Assignment

Generated assignments are explicit.

```
reader.GetString(2)

↓

Customer.Name
```

No lookup tables are required.

---

# Nullable Values

Generated code performs efficient null checks.

```
IsDBNull

↓

Default

or

↓

Value
```

Behavior is deterministic.

---

# Type Conversion

Materializers perform only required conversions.

Examples:

```
int

↓

long
```

```
timestamp

↓

DateTime
```

Conversion rules are generated.

---

# Collections

Collections are created directly.

```
Customer

↓

Orders

↓

List<Order>
```

Runtime later assembles relationships.

---

# Identity Resolution

Materializers cooperate with Runtime.

```
Primary Key

↓

Identity Map

↓

Existing Object
```

Materializers never manage identity themselves.

---

# Graph Construction

Materializers construct nodes.

Runtime connects nodes.

```
Rows

↓

Customer

↓

Runtime

↓

Orders
```

Responsibilities remain separate.

---

# Metadata Consumption

Generated materializers consume metadata produced during compilation.

```
Metadata

↓

Materializer Generation

↓

Generated Code
```

Runtime lookups are unnecessary.

---

# Reader Access

Materializers read values directly.

Examples:

```csharp
reader.GetInt32(0)

reader.GetString(1)

reader.GetGuid(2)
```

Column ordinals are generated.

---

# Ordinal Stability

Ordinals are determined during planning.

```
Projection

↓

Column Order

↓

Generated Reader Access
```

Runtime performs no name lookups.

---

# Value Conversion

Generated conversions remain explicit.

```
Database Value

↓

CLR Value

↓

Assignment
```

Reflection is never required.

---

# Performance

Generated materializers eliminate:

- Reflection
- Property discovery
- Expression trees
- Delegate compilation
- Dictionary lookups

Construction becomes nearly identical to handwritten code.

---

# Thread Safety

Materializers should remain stateless.

Each invocation creates or populates a single object.

Generated classes are naturally thread-safe.

---

# Memory

Materializers allocate only:

- Target objects
- Required collections

Intermediate allocations should be avoided.

---

# Error Handling

Materializers should report:

- Invalid conversions
- Missing required columns
- Unexpected null values

Structural errors should have been caught during compilation.

---

# Diagnostics

Generation diagnostics may include:

- Unsupported constructors
- Duplicate mappings
- Missing assignments

Runtime diagnostics focus only on execution failures.

---

# Native AOT

Generated materializers are a major contributor to Native AOT compatibility.

They avoid:

- Reflection
- Dynamic emit
- Expression compilation
- Runtime code generation

Everything is ordinary compiled C#.

---

# Testing

Materializers should be tested independently.

Recommended tests:

```
Construction

↓

Assignments

↓

Nullable Handling

↓

Conversions

↓

Snapshot Tests
```

No database should be required.

---

# Snapshot Testing

Generated materializers are excellent snapshot candidates.

```
Entity

↓

Generated Code

↓

Snapshot
```

Regressions become obvious.

---

# Future Evolution

Future materialization features may include:

- Immutable record support
- Constructor optimization
- Span-based readers
- Source-generated converters
- Custom value converters
- Collection pooling

The public Runtime contract should remain unchanged.

---

# Materializer Checklist

Before implementing a materializer, ask:

- Is it generated?
- Does it avoid reflection?
- Are assignments explicit?
- Are column ordinals deterministic?
- Is it stateless?
- Can it be snapshot tested?

If not, reconsider the implementation.

---

# Relationship to the Framework

Materialization is the final transformation stage.

```
Planner

↓

Execution Plan

↓

Runtime

↓

Provider

↓

Rows

↓

Generated Materializer

↓

Application Objects
```

Execution produces rows.

Materialization produces the object graph.

---

# Summary

The Materialization Architecture defines the compile-time generated object construction system used by CoffeeBeanery to transform database rows into fully initialized application objects through explicit assignments, deterministic column access, and reflection-free generated code.

By generating materializers during compilation rather than relying on runtime mapping, CoffeeBeanery achieves performance comparable to handwritten object construction while improving testability, simplifying Runtime, reducing allocations, enabling Native AOT, and maintaining a clear separation between execution, infrastructure, and object creation.