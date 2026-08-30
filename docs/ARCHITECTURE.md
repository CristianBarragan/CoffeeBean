# Architecture

Foundgine is a semantic execution layer between application intent and physical execution.

The central architectural rule is:

> **Callers describe intent. The semantic model defines meaning. Authorization determines authority. Planning defines logical execution. Providers define physical execution.**

## Canonical pipeline

```text
Caller / transport
       ↓
Intent
       ↓
Semantic model
       ↓
Resolve
       ↓
Validate
       ↓
Normalize
       ↓
Authorize
       ↓
Security-preserving plan optimization
       ↓
Execution plan / ExecutionIR
       ↓
Provider compilation
       ↓
Provider execution
       ↓
Result + evidence
```

The layers are deliberately separate.

## Semantic Operation Graph → Authorization → Plan Binding → Execution

The semantic operation graph is the canonical security object for a resolved request. It makes the requested topology explicit before planning and gives authorization one complete object to evaluate rather than a collection of transport-specific fragments.

The lifecycle is deliberately monotonic:

```text
Caller / transport
        ↓
Intent
        ↓
Semantic resolution
        ↓
Semantic Operation Graph
        ↓
Graph validation + resource limits
        ↓
Authorization against the immutable semantic contract
        ↓
Authorized Operation Graph + Authorization Evidence
        ↓
Provider-independent Semantic Plan
        │
        └── AuthorizationBinding
              ├── ContractFingerprint
              └── AuthorizationFingerprint
        ↓
Security-preserving rewrites / optimization
        ↓
ExecutionIR
        ↓
Provider plan + provider security proof
        ↓
Final execution gate
        ↓
Provider execution
        ↓
Result + evidence
```

### The graph is the security unit

`SemanticOperationGraph` represents the complete resolved operation topology: root and child nodes, fields, relationships/connections and semantic query constraints. Graph validation and resource limits run before expensive planning or provider work.

Authorization evaluates the complete graph against the trusted immutable `SemanticContractSnapshot`. For graph authorization, the provider is absent from the decision boundary. A successful decision produces both an authorized graph and immutable authorization evidence.

```text
SemanticContractSnapshot + SemanticOperationGraph
                         ↓
                  authorization
                         ↓
       AuthorizedGraph + AuthorizationEvidence
```

Retrieval strategies such as fuzzy search, full-text search, BM25 or Apache AGE may help resolve ambiguous references, but they only produce candidates and evidence. They never become the authority over which graph nodes may be exercised.

### Plan binding is provenance, not a second authorization system

When an authorized operation is planned, the resulting `SemanticPlan` carries a `SemanticPlanAuthorizationBinding`. The binding records the fingerprints of the exact semantic contract and authorization decision that produced the plan.

```text
SemanticPlan
   │
   └── AuthorizationBinding
          ├── contract fingerprint
          └── authorization fingerprint
```

Planner rewrites are required to preserve this binding. A rewrite that adds, removes, or changes the authorization provenance is rejected rather than silently producing an authorization-free plan.

This means optimization can change execution shape without changing the authority under which that shape was created.

### ExecutionIR is the next trust boundary

`ExecutionIR` is produced only from a plan carrying authorization provenance. Before it is accepted for provider compilation, the binding can be checked against the same semantic contract and authorization evidence.

The provider plan then inherits the same binding. The final execution gate additionally requires a provider security proof bound to the exact provider plan and exact `ExecutionIR`.

```text
authorized semantic plan
        ↓
 authorization binding
        ↓
    ExecutionIR
        ↓
 provider plan + security proof
        ↓
  exact-plan execution gate
        ↓
      execute
```

The important invariant is:

> **An executable provider artifact must remain traceably bound to the semantic contract and authorization decision that produced it.**

Changing the contract, authorization evidence, execution IR, provider, or security proof breaks that chain and causes execution to fail closed.

### Reads and mutations share the boundary model

Reads use `SemanticOperationGraph` → `SemanticPlan` → `ExecutionIR`. Mutations use `SemanticMutationOperationGraph` → mutation planning → execution security/conformance, with the same principle: semantic meaning is resolved and authorized before provider-specific work, and execution artifacts retain security provenance.

This is why GraphQL, MCP, JSON, AI tools and direct C# callers do not need separate authorization architectures. They converge before the security-sensitive planning boundary.


## Layer 1 — Intent

Intent is what the caller wants.

Supported entry surfaces include:

- typed fluent C#;
- dynamic fluent C#;
- JSON;
- GraphQL adapters;
- MCP;
- Microsoft.Extensions.AI tool integration;
- semantic mutation builders.

Intent is untrusted input.

## Layer 2 — Semantics

`Foundgine.Semantics` defines application meaning:

```text
Entity
 ├── fields
 ├── identity
 └── relationships
```

It also defines request graphs, filters, ordering, pagination, logical traversals, mutation semantics, capability descriptions, and security context contracts.

The semantic model is not the database schema.

## Layer 3 — Metadata

`Foundgine.Metadata` describes structural facts:

```text
entities
fields
primary keys
columns
direct relationships
model mappings
connections
conversions
```

Metadata can be generated by `Foundgine.Aot.Generator`.

The important distinction is:

```text
Metadata = what structurally exists
Semantics = what the application means/exposes
```

## Layer 4 — Authorization

Authorization is applied to resolved semantic meaning.

The policy can constrain:

- entities;
- fields;
- relationships;
- read/write operations;
- conditional resource predicates.

Authorization is not a GraphQL concern, SQL concern, or AI concern.

A transport can help construct intent but cannot grant authority.

## Layer 5 — Planning

`Foundgine.Planning` turns authorized semantic operations into a provider-independent logical plan.

A read plan contains topology such as:

```text
Scan
  ↓
Traverse
  ↓
TraverseConnection
```

and semantic clauses such as filtering, ordering and pagination.

The plan must not contain SQL.

## Layer 6 — Rewriting and optimization

The planner can apply conservative rewrites.

A rewrite must preserve:

```text
semantic meaning
+
authorization
+
required security invariants
```

Where aggregate semantics or provider capabilities matter, explicit proof/capability gates are used.

Provider cost estimates are advisory only.

## Layer 7 — Execution

`Foundgine.Execution` is the physical execution boundary.

It provides:

- `ExecutionIR`;
- provider compiler contracts;
- provider execution contracts;
- result materialization;
- execution evidence;
- provider security conformance;
- execution-time authorization revalidation;
- mutation execution coordination.

## Layer 8 — Providers

Current providers include:

```text
Foundgine.Sql
Foundgine.InMemory
```

SQL lowers the plan into parameterized SQL and executes through ADO.NET.

InMemory executes a deliberately limited subset over CLR-backed rows.

The existence of both providers is an architectural test: the logical plan cannot depend on SQL-specific concepts.

### Retrieval strategies inside the SQL provider

Semantic resolution sometimes needs ranked candidates for an ambiguous reference — a name that doesn't exactly match, a fuzzy search term, or a "find things related to this" request. `Foundgine.Sql` answers that through PostgreSQL mechanisms selected per request, all behind the same provider-neutral `RetrievalStrategy` contract:

```text
Semantic candidate request
          ↓
   RetrievalStrategy
          ↓
┌─────────┬──────────┬─────────┬────────────────┬──────────┐
│  Fuzzy  │ FullText │ Search  │ GraphSimilarity │  Vector  │
│ pg_trgm │ tsvector │pg_search│  Apache AGE     │(reserved)│
│         │          │ / BM25  │   (Cypher)      │for future│
│         │          │(optional)│   (optional)   │ pgvector │
└─────────┴──────────┴─────────┴────────────────┴──────────┘
          ↓
  Ranked candidates + provenance
          ↓
   Semantic resolution / authorization (unchanged)
```

`Fuzzy` and `FullText` use PostgreSQL's built-in `pg_trgm` and `tsvector`/`websearch_to_tsquery`. `Search` and `GraphSimilarity` are optional and require the `pg_search` and Apache AGE extensions respectively — `GraphSimilarity` runs a Cypher query through AGE over a semantic relationship (for example, finding suppliers similar to a given one by shared purchase-order neighbors) and returns ranked candidates, the same shape as any other strategy. `Vector` is intentionally reserved for a future `pgvector` provider rather than implemented today.

Retrieval only ever produces candidates and evidence. It does not bypass semantic resolution, authorization, or planning — a candidate still has to resolve to a real semantic entity and pass authorization before it can appear in a plan.

## Transport adapters

Transport packages remain thin:

```text
GraphQL → semantic request
JSON    → semantic request
MCP     → semantic request
AI      → semantic tool calls / semantic request
```

They do not become alternate planners.

## Security context

Authority is host-owned.

```text
Authentication / trusted host
            ↓
SecurityExecutionContext
            ↓
semantic execution
```

GraphQL variables, JSON properties, MCP arguments, and model-generated tool arguments must not be treated as authoritative identity/tenant/warrant material.

## Logical traversal

A semantic traversal can hide intermediate edges:

```text
Customer
  → CustomerRelationship
  → Contract
  → Transaction
```

as:

```text
Customer.transactions
```

Resolution expands the path before authorization.

This prevents a shortcut from bypassing a denied intermediate entity or relationship.

## Mutations

Mutation semantics have their own algebra because writes require dependency, generated-value, approval, and security handling.

```text
Semantic mutation graph
       ↓
Mutation plan
       ↓
dependency levels
       ↓
security/conformance gate
       ↓
provider execution
```

GraphQL mutation translation, MCP mutation tools, and direct mutation authoring all converge on this boundary.

## AOT

The AOT architecture moves stable topology into compilation:

```text
Foundgine.Aot declarations
        ↓
Foundgine.Aot.Generator
        ↓
generated metadata
        ↓
metadata/semantic discovery
        ↓
runtime
```

This reduces runtime discovery work and supports Native AOT-friendly metadata handling.

It does not make arbitrary provider/application dependencies automatically Native-AOT compatible.

## Optional authority recovery

`Foundgine.Security.Authority` is deliberately outside the core.

```text
authority/recovery subsystem
            ↓
validated authority context
            ↓
Foundgine semantic execution
```

Applications that do not need a distributed authorization authority/recovery control plane do not need this package.

## Dependency direction

The intended package structure is:

```text
                  Foundgine.Abstractions
                         ▲
                         │
             ┌───────────┼────────────┐
             │           │            │
          Metadata    Semantics     AOT
             │           │
             └──────┬────┘
                    ▼
                Planning
                    │
                    ▼
                Execution
                 /     \
                ▼       ▼
              SQL     InMemory

Adapters
  ├── JSON
  ├── GraphQL
  ├── MCP
  └── AI
        │
        ▼
     Foundgine
```

The exact project-reference graph contains additional supporting dependencies, but this is the architectural direction.

## What Foundgine is not

Foundgine is not intended to be:

- an ORM;
- a GraphQL server;
- an AI agent framework;
- an authorization server;
- a workflow engine;
- a database engine;
- a generic SQL builder.

Its purpose is the boundary between semantic intent and controlled execution.

---

Next: [Metadata → Semantics](METADATA-TO-SEMANTICS.md)
